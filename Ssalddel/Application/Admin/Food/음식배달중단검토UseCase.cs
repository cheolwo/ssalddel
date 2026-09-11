using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Admin.Food;

public interface I음식배달중단검토UseCase
{
    Task<음식배달시도운영응답?> 검토Async(
        string 시도StableId,
        음식배달중단검토요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 음식배달중단검토UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    I운영배차공통UseCase operationalDispatch,
    ILogger<음식배달중단검토UseCase> logger) : I음식배달중단검토UseCase
{
    public async Task<음식배달시도운영응답?> 검토Async(
        string 시도StableId,
        음식배달중단검토요청 request,
        CancellationToken cancellationToken = default)
    {
        var attemptId = 시도StableId?.Trim();
        if (string.IsNullOrWhiteSpace(attemptId))
            throw new ArgumentException("배달 시도 식별자가 필요합니다.", nameof(시도StableId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.클라이언트요청Id == Guid.Empty)
            throw new ArgumentException("중단 검토 클라이언트 요청 ID가 필요합니다.", nameof(request));
        var reason = request.판정사유?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("중단 검토 판정 사유가 필요합니다.", nameof(request));
        if (reason.Length > 1000)
            throw new ArgumentException("중단 검토 판정 사유는 1000자 이하여야 합니다.", nameof(request));
        var responsibility = 책임Code(request.판정Code);
        var reviewerId = currentUser.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(reviewerId))
            throw new InvalidOperationException("검토 운영자 식별자를 확인할 수 없습니다.");

        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var attempt = await db.음식배달시도
                .SingleOrDefaultAsync(x => x.시도StableId == attemptId, cancellationToken);
            if (attempt is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            if (attempt.검토요청Id == request.클라이언트요청Id)
            {
                if (attempt.책임Code != responsibility
                    || attempt.악용확정여부 != request.악용확정여부
                    || !string.Equals(attempt.검토사유, reason, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("같은 검토 요청 ID를 다른 판정 내용으로 다시 사용할 수 없습니다.");
                }

                await transaction.RollbackAsync(cancellationToken);
                return attempt;
            }

            if (attempt.상태Code != 음식배달시도상태Code.중단)
                throw new InvalidOperationException("중단된 배달 시도만 책임을 검토할 수 있습니다.");
            if (request.예상Revision.HasValue && request.예상Revision.Value != attempt.Revision)
                throw new DbUpdateConcurrencyException("배달 중단 검토 대상이 다른 요청에서 먼저 변경됐습니다.");
            if (responsibility == 운영배차책임Code.기사
                && attempt.책임Code == 운영배차책임Code.보호대상
                && !request.악용확정여부)
            {
                throw new InvalidOperationException("보호 중단을 기사 책임으로 바꾸려면 사람 검토로 악용을 확정해야 합니다.");
            }

            var now = DateTime.UtcNow;
            attempt.책임Code = responsibility;
            attempt.악용확정여부 = request.악용확정여부;
            attempt.검토요청Id = request.클라이언트요청Id;
            attempt.검토UserId = reviewerId;
            attempt.검토사유 = reason;
            attempt.Revision++;
            attempt.UpdatedAtUtc = now;
            db.운영배차활동사건.Add(운영배차활동사건Factory.책임판정(
                attempt.기사Id,
                attempt.제안Id,
                attempt.주문번호,
                attempt.추천라운드,
                now,
                attempt.시도StableId,
                responsibility,
                $"HumanReview:{request.판정Code.Trim()}:{(request.악용확정여부 ? "AbuseConfirmed" : "NoAbuse")}",
                request.클라이언트요청Id));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return attempt;
        });

        if (result is null)
            return null;

        try
        {
            await operationalDispatch.단기지표조회Async(result.기사Id, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "중단 책임 검토 뒤 Redis 단기 지표 재구성에 실패했습니다. 영속 사건으로 다시 만들 수 있습니다. AttemptId={AttemptId}",
                result.시도StableId);
        }

        return ToResponse(result);
    }

    private static string 책임Code(string? reviewCode)
        => reviewCode?.Trim() switch
        {
            음식배달중단검토판정Code.보호 => 운영배차책임Code.보호대상,
            음식배달중단검토판정Code.기사책임 => 운영배차책임Code.기사,
            음식배달중단검토판정Code.음식점책임 => 운영배차책임Code.음식점,
            음식배달중단검토판정Code.플랫폼책임 => 운영배차책임Code.플랫폼,
            _ => throw new ArgumentException("지원하지 않는 중단 검토 판정입니다.", nameof(reviewCode))
        };

    internal static 음식배달시도운영응답 ToResponse(음식배달시도 x)
        => new()
        {
            시도StableId = x.시도StableId,
            제안Id = x.제안Id,
            기사Id = x.기사Id,
            시도순번 = x.시도순번,
            Revision = x.Revision,
            상태Code = x.상태Code,
            수락시각Utc = x.수락시각Utc,
            표시준비예정시각Utc = x.표시준비예정시각Utc,
            가게도착시각Utc = x.가게도착시각Utc,
            픽업완료시각Utc = x.픽업완료시각Utc,
            중단시각Utc = x.중단시각Utc,
            전달완료시각Utc = x.전달완료시각Utc,
            현장대기초 = x.현장대기초,
            중단사유Code = x.중단사유Code ?? string.Empty,
            책임Code = x.책임Code ?? string.Empty,
            조리지연재배차여부 = x.조리지연재배차여부,
            재조리요청StableId = x.재조리요청StableId ?? string.Empty,
            재조리요청시각Utc = x.재조리요청시각Utc,
            유산추정여부 = x.유산추정여부,
            악용확정여부 = x.악용확정여부,
            검토사유 = x.검토사유 ?? string.Empty
        };
}
