using Ssalddel.Contracts.Common.Warehouse;
using OutboundStockCandidate = Ssalddel.Services.LogisticsProcessing.Warehouse.OutboundBatchEngine.OutboundStockCandidate;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

// HEAD의 이관 전 순수 계산만 동결한 비교 기준. 운영 코드나 신규 규칙으로 사용하지 않는다.
internal static class 기존창고배분참조
{
    internal static OutboundBatchPlanResult CreatePlan(
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        var allocations = TryCreateSingleWarehousePlan(lines, candidateMap);
        var unallocated = new List<OutboundBatchUnallocatedLine>();

        if (allocations.Count == 0)
        {
            var remainingStock = CreateRemainingStock(candidateMap);
            foreach (var line in lines)
            {
                allocations.AddRange(CreateLineAllocations(
                    line,
                    candidateMap[line.LineKey],
                    remainingStock,
                    unallocated));
            }
        }

        foreach (var line in lines)
        {
            var plannedQuantity = allocations
                .Where(x => string.Equals(x.LineKey, line.LineKey, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Quantity);

            if (plannedQuantity < line.Quantity && unallocated.All(x => !string.Equals(x.LineKey, line.LineKey, StringComparison.OrdinalIgnoreCase)))
            {
                unallocated.Add(new OutboundBatchUnallocatedLine
                {
                    LineKey = line.LineKey,
                    Sku = line.Sku,
                    ProductName = line.ProductName,
                    RequestedQuantity = line.Quantity,
                    PlannedQuantity = plannedQuantity,
                    Reason = "출고 가능한 창고 재고가 부족합니다."
                });
            }
        }

        var warehouseCount = allocations.Select(x => x.WarehouseId).Distinct().Count();
        return new OutboundBatchPlanResult
        {
            IsComplete = unallocated.Count == 0 && allocations.Count > 0,
            RequiresSplitShipment = warehouseCount > 1,
            Message = CreateMessage(allocations, unallocated),
            Allocations = allocations,
            UnallocatedLines = unallocated
        };
    }


    private static List<OutboundBatchAllocation> TryCreateSingleWarehousePlan(
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        if (lines.Count <= 1)
        {
            return [];
        }

        var warehouseIds = candidateMap.Values
            .SelectMany(x => x.Select(candidate => candidate.WarehouseId))
            .Distinct()
            .ToArray();

        var bestPlan = warehouseIds
            .Select(warehouseId => TryCreateSingleWarehousePlanForWarehouse(
                warehouseId,
                lines,
                candidateMap))
            .OfType<SingleWarehousePlan>()
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestPlan is null)
        {
            return [];
        }

        return bestPlan.Allocations;
    }

    private static SingleWarehousePlan? TryCreateSingleWarehousePlanForWarehouse(
        long warehouseId,
        IReadOnlyList<OutboundBatchPlanLineRequest> lines,
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        var remainingStock = CreateRemainingStock(candidateMap);
        var allocations = new List<OutboundBatchAllocation>();
        var selectedCandidates = new List<OutboundStockCandidate>();

        foreach (var line in lines)
        {
            var candidate = candidateMap[line.LineKey]
                .Where(x => x.WarehouseId == warehouseId
                            && remainingStock.GetValueOrDefault(x.InboundProductId) >= line.Quantity)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (candidate is null)
            {
                return null;
            }

            remainingStock[candidate.InboundProductId] -= line.Quantity;
            selectedCandidates.Add(candidate);
            allocations.Add(CreateAllocation(line, candidate, line.Quantity));
        }

        var score = selectedCandidates.Sum(candidate => candidate.Score)
                    + (selectedCandidates.Any(candidate => candidate.IsServiceAreaMatched) ? 100m : 0m);
        return new SingleWarehousePlan(allocations, score);
    }

    private static Dictionary<long, int> CreateRemainingStock(
        IReadOnlyDictionary<string, IReadOnlyList<OutboundStockCandidate>> candidateMap)
    {
        return candidateMap.Values
            .SelectMany(x => x)
            .GroupBy(x => x.InboundProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Min(candidate => candidate.AvailableQuantity));
    }

    private static List<OutboundBatchAllocation> CreateLineAllocations(
        OutboundBatchPlanLineRequest line,
        IReadOnlyList<OutboundStockCandidate> candidates,
        Dictionary<long, int> remainingStock,
        List<OutboundBatchUnallocatedLine> unallocated)
    {
        var remaining = line.Quantity;
        var allocations = new List<OutboundBatchAllocation>();

        foreach (var candidate in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var availableQuantity = remainingStock.GetValueOrDefault(candidate.InboundProductId);
            var quantity = Math.Min(availableQuantity, remaining);
            if (quantity <= 0)
            {
                continue;
            }

            allocations.Add(CreateAllocation(line, candidate, quantity));
            remainingStock[candidate.InboundProductId] = availableQuantity - quantity;
            remaining -= quantity;
        }

        if (remaining > 0)
        {
            unallocated.Add(new OutboundBatchUnallocatedLine
            {
                LineKey = line.LineKey,
                Sku = line.Sku,
                ProductName = line.ProductName,
                RequestedQuantity = line.Quantity,
                PlannedQuantity = line.Quantity - remaining,
                Reason = candidates.Count == 0
                    ? "출고 가능한 창고 후보가 없습니다."
                    : "후보 창고 재고 합계가 요청 수량보다 부족합니다."
            });
        }

        return allocations;
    }

    private static OutboundBatchAllocation CreateAllocation(
        OutboundBatchPlanLineRequest line,
        OutboundStockCandidate candidate,
        int quantity)
    {
        return new OutboundBatchAllocation
        {
            LineKey = line.LineKey,
            SalesProductId = line.SalesProductId,
            InboundProductId = candidate.InboundProductId,
            WarehouseId = candidate.WarehouseId,
            WarehouseName = candidate.WarehouseName,
            Sku = candidate.Sku,
            ProductName = candidate.ProductName,
            Quantity = quantity,
            IsServiceAreaMatched = candidate.IsServiceAreaMatched,
            EstimatedDistanceKm = candidate.EstimatedDistanceKm,
            EstimatedTransportCost = candidate.EstimatedTransportCost,
            SelectionScore = candidate.Score,
            SelectionReason = candidate.SelectionReason
        };
    }


    private static string CreateMessage(
        IReadOnlyList<OutboundBatchAllocation> allocations,
        IReadOnlyList<OutboundBatchUnallocatedLine> unallocated)
    {
        if (allocations.Count == 0)
        {
            return "출고 배치 계획을 만들 수 없습니다.";
        }

        if (unallocated.Count > 0)
        {
            return "일부 상품은 출고 배치 계획을 만들지 못했습니다.";
        }

        var warehouseCount = allocations.Select(x => x.WarehouseId).Distinct().Count();
        return warehouseCount > 1
            ? "복수 창고 분할 출고 계획이 생성되었습니다."
            : "단일 창고 출고 배치 계획이 생성되었습니다.";
    }

    private sealed record SingleWarehousePlan(
        List<OutboundBatchAllocation> Allocations,
        decimal Score);


}
