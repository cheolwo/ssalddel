using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class OutboundBatchEngineTests
{
    [Fact]
    public void 공통배분은_동결한기존엔진과_동일입력에서_전체결과가같다()
    {
        for (var seed = 0; seed < 150; seed++)
        {
            var random = new Random(seed);
            var lines = Enumerable.Range(0, 3).Select(index => Line($"line-{index}", random.Next(1, 9))).ToArray();
            var map = new Dictionary<string, IReadOnlyList<OutboundBatchEngine.OutboundStockCandidate>>();
            // 사전 삽입 순서와 라인 순서가 달라도 기존 동점 처리를 보존한다.
            foreach (var line in lines.Reverse())
            {
                map.Add(line.LineKey, Enumerable.Range(1, random.Next(0, 5)).Select(index =>
                    new OutboundBatchEngine.OutboundStockCandidate(line.LineKey, null,
                        index, index % 2, "모의 창고", "", "SKU-1", "모의 상품",
                        random.Next(0, 12), random.Next(2) == 0, null, null, random.Next(0, 3), "모의 후보"))
                    .OrderByDescending(value => value.Score).ToArray());
            }
            var expected = 기존창고배분참조.CreatePlan(lines, map);
            var actual = OutboundBatchEngine.CreatePlan(lines, map);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected),
                System.Text.Json.JsonSerializer.Serialize(actual));
        }
    }

    [Fact]
    public void CreatePlan_consumes_shared_inbound_stock_across_duplicate_sku_lines()
    {
        var lines = new[]
        {
            Line("line-1", quantity: 3),
            Line("line-2", quantity: 3)
        };
        var candidates = CandidateMap(lines, inboundProductId: 101, availableQuantity: 5);

        var result = OutboundBatchEngine.CreatePlan(lines, candidates);

        Assert.False(result.IsComplete);
        Assert.Equal(5, result.Allocations.Sum(x => x.Quantity));
        Assert.All(result.Allocations, allocation => Assert.Equal(101, allocation.InboundProductId));
        var unallocated = Assert.Single(result.UnallocatedLines);
        Assert.Equal("line-2", unallocated.LineKey);
        Assert.Equal(3, unallocated.RequestedQuantity);
        Assert.Equal(2, unallocated.PlannedQuantity);
    }

    [Fact]
    public void CreatePlan_completes_single_warehouse_plan_when_shared_stock_covers_aggregate_quantity()
    {
        var lines = new[]
        {
            Line("line-1", quantity: 3),
            Line("line-2", quantity: 2)
        };
        var candidates = CandidateMap(lines, inboundProductId: 101, availableQuantity: 5);

        var result = OutboundBatchEngine.CreatePlan(lines, candidates);

        Assert.True(result.IsComplete);
        Assert.Empty(result.UnallocatedLines);
        Assert.Equal(5, result.Allocations.Sum(x => x.Quantity));
        Assert.Single(result.Allocations.Select(x => x.WarehouseId).Distinct());
    }

    [Fact]
    public void GetAllocatableQuantity_does_not_subtract_reserved_quantity_twice()
    {
        var item = new 입고상품
        {
            가용수량 = 6,
            예약수량 = 4
        };

        var quantity = OutboundBatchEngine.GetAllocatableQuantity(item);

        Assert.Equal(6, quantity);
    }

    private static OutboundBatchPlanLineRequest Line(string lineKey, int quantity)
        => new()
        {
            LineKey = lineKey,
            SalesProductId = 201,
            PreferredInboundProductId = 101,
            Sku = "SKU-1",
            ProductName = "테스트 상품",
            Quantity = quantity
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<OutboundBatchEngine.OutboundStockCandidate>> CandidateMap(
        IEnumerable<OutboundBatchPlanLineRequest> lines,
        long inboundProductId,
        int availableQuantity)
    {
        return lines.ToDictionary(
            line => line.LineKey,
            line => (IReadOnlyList<OutboundBatchEngine.OutboundStockCandidate>)
            [
                new OutboundBatchEngine.OutboundStockCandidate(
                    line.LineKey,
                    line.SalesProductId,
                    inboundProductId,
                    WarehouseId: 10,
                    WarehouseName: "테스트 창고",
                    WarehouseAddress: "서울특별시 송파구",
                    Sku: line.Sku,
                    ProductName: line.ProductName,
                    AvailableQuantity: availableQuantity,
                    IsServiceAreaMatched: true,
                    EstimatedDistanceKm: 1m,
                    EstimatedTransportCost: 3000m,
                    Score: 1500m,
                    SelectionReason: "테스트 후보")
            ],
            StringComparer.OrdinalIgnoreCase);
    }
}
