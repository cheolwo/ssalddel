using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Application.Food.Handlers;

public sealed class 주문자음식주문취소CommandHandler(
    ISsalddelFoodOrderStore orderStore,
    IPublisher publisher,
    SsalddelContext? db = null)
    : IRequestHandler<주문자음식주문취소Command, 음식주문응답?>
{
    public async Task<음식주문응답?> Handle(
        주문자음식주문취소Command request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var changed = await 취소와활동기록Async(request, cancellationToken);
        if (changed is null)
        {
            return null;
        }

        if (changed.새로변경됨)
        {
            await publisher.Publish(
                new 주문자음식주문취소됨Event(
                    changed.주문,
                    request.주문자UserId.Trim(),
                    운영배차주문자취소사유Code.정규화(request.Payload.사유Code),
                    DateTime.UtcNow,
                    Guid.NewGuid().ToString("N")),
                cancellationToken);
        }

        return orderStore.GetOrder(request.주문번호) ?? changed.주문;
    }

    private async Task<음식주문변경결과?> 취소와활동기록Async(
        주문자음식주문취소Command request,
        CancellationToken cancellationToken)
    {
        if (db is null)
        {
            return 취소(request);
        }

        if (!db.Database.IsRelational())
        {
            var changed = 취소(request);
            if (changed?.새로변경됨 == true)
            {
                await 활동추가Async(changed.주문, request, cancellationToken);
            }
            return changed;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var changed = 취소(request);
            if (changed?.새로변경됨 == true)
            {
                await 활동추가Async(changed.주문, request, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return changed;
        });
    }

    private 음식주문변경결과? 취소(주문자음식주문취소Command request)
        => orderStore.주문자취소(
            request.주문번호,
            request.Payload,
            request.주문자UserId.Trim());

    private async Task 활동추가Async(
        음식주문응답 order,
        주문자음식주문취소Command request,
        CancellationToken cancellationToken)
    {
        var occurredAt = order.상태이력
            .Where(x => x.클라이언트요청Id == request.Payload.클라이언트요청Id)
            .OrderByDescending(x => x.전이시각Utc)
            .Select(x => x.전이시각Utc)
            .FirstOrDefault();
        var time = occurredAt == default ? DateTime.UtcNow : occurredAt;
        db!.운영배차활동사건.AddRange(
            운영배차활동사건Factory.주문자취소(
                request.주문자UserId,
                order.주문번호,
                time,
                request.Payload.사유Code),
            운영배차활동사건Factory.음식점제안철회(
                order.음식점Id,
                order.주문번호,
                time,
                request.Payload.사유Code));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(주문자음식주문취소Command command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.주문번호);
        ArgumentNullException.ThrowIfNull(command.Payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.주문자UserId);

        if (command.Payload.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("주문자 취소의 클라이언트 요청 ID가 필요합니다.");
        }
        _ = 운영배차주문자취소사유Code.정규화(command.Payload.사유Code);
        if (command.Payload.사유?.Length > 500)
        {
            throw new ArgumentException("주문 취소 사유는 500자 이내로 입력해 주세요.");
        }
    }
}
