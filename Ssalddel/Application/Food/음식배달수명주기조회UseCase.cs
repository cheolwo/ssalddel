using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.Services.Versioning;

namespace Ssalddel.Application.Food;

public interface I음식배달수명주기조회UseCase
{
    Task<음식배달수명주기Snapshot?> 상세Async(
        string orderNo,
        string? ordererUserId,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelUseCase(
    "음식 배달 수명주기 상태 사본 조회",
    Summary = "운영 서버가 소유한 음식 주문·배차 판본을 Unity와 웹이 소비할 읽기 전용 상태 사본으로 제공합니다.")]
public sealed class 음식배달수명주기조회UseCase(
    SsalddelContext db,
    ISsalddelFoodOrderStore orderStore) : I음식배달수명주기조회UseCase
{
    public async Task<음식배달수명주기Snapshot?> 상세Async(
        string orderNo,
        string? ordererUserId,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNo = Clean(orderNo);
        var normalizedUserId = Clean(ordererUserId);
        if (normalizedOrderNo is null || normalizedUserId is null) return null;

        var owned = await db.음식주문.AsNoTracking().AnyAsync(
            item => item.주문번호 == normalizedOrderNo && item.주문자UserId == normalizedUserId,
            cancellationToken);
        if (!owned) return null;

        var order = orderStore.GetOrder(normalizedOrderNo);
        if (order is null) return null;
        var transport = await db.운송원장.AsNoTracking()
            .Where(item => item.배차업무유형 == 상태값.배차업무유형.음식배달
                           && (item.원본의뢰Id == normalizedOrderNo || item.의뢰Id == normalizedOrderNo))
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var sourceRevision = $"food-order:{order.Revision}:{order.최근변경시각Utc?.Ticks ?? order.CreatedAt.Ticks}";
        return 음식배달수명주기SnapshotFactory.FromOperationalOrder(
            order,
            sourceRevision,
            transport?.확정기사Id);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
