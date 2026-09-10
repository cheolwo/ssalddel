using MediatR;
using Ssalddel.Application.Food.Events;
using Ssalddel.Services.Food;

namespace Ssalddel.Application.Food.Handlers;

/// <summary>
/// 수락 트랜잭션에 이미 기록된 배차 요청을 즉시 한 번 처리합니다.
/// 실패해도 요청은 Outbox에 남으며 배경 작업이 재시도합니다.
/// </summary>
public sealed class 음식점수락후배차대기생성EventHandler(
    I음식배차요청OutboxService dispatchOutbox,
    ILogger<음식점수락후배차대기생성EventHandler> logger) : INotificationHandler<음식점주문수락됨Event>
{
    public async Task Handle(음식점주문수락됨Event notification, CancellationToken cancellationToken)
    {
        try
        {
            await dispatchOutbox.즉시처리Async(notification.EventId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "음식 배차 요청 즉시 처리에 실패했습니다. Outbox 재시도를 기다립니다. EventId={EventId}, OrderNo={OrderNo}",
                notification.EventId,
                notification.주문.주문번호);
        }
    }
}
