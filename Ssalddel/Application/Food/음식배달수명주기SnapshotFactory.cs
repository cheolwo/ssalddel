using Ssalddel.Contracts.Food;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Application.Food;

public static class 음식배달수명주기SnapshotFactory
{
    public static 음식배달수명주기Snapshot FromOperationalOrder(
        음식주문응답 order,
        string sourceRevision,
        string? driverStableId = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new 음식배달수명주기Snapshot
        {
            SourceCode = 음식배달상태원천코드.OperationalServer,
            SourceRevision = sourceRevision ?? string.Empty,
            OrderStableId = order.주문번호,
            OrderRevision = order.Revision,
            OrderStateCode = order.상태,
            DispatchStateCode = order.배차상태,
            RestaurantStableId = $"restaurant:{order.음식점Id}",
            OrdererStableId = order.주문자UserId,
            DriverStableId = driverStableId?.Trim() ?? string.Empty,
            AcceptedAtUtc = order.음식점수락시각Utc,
            ReadyForPickupAtUtc = order.픽업준비시각Utc,
            DispatchRequestedAtUtc = order.배차요청시각Utc,
            PickedUpAtUtc = TransitionAt(order, 음식주문상태코드.픽업완료),
            DeliveredAtUtc = TransitionAt(order, 음식주문상태코드.전달완료),
            ReceiptConfirmedAtUtc = TransitionAt(order, 음식주문상태코드.수령확인),
            SourceRefs = ["api/v1/food-orders/" + order.주문번호]
        };
    }

    private static DateTime? TransitionAt(음식주문응답 order, string state)
        => order.상태이력
            .Where(x => x.다음상태 == state)
            .OrderBy(x => x.전이시각Utc)
            .Select(x => (DateTime?)x.전이시각Utc)
            .FirstOrDefault();
}
