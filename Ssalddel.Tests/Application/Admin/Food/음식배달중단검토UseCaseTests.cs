using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class 음식배달중단검토UseCaseTests
{
    [Fact]
    public async Task 보호중단을_기사책임으로바꾸려면_사람의악용확정이필요하다()
    {
        await using var db = CreateContext();
        db.음식배달시도.Add(InterruptedAttempt());
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db, new OperationalDispatchStub());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.검토Async(
            "attempt-1",
            new 음식배달중단검토요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = 2,
                판정Code = 음식배달중단검토판정Code.기사책임,
                악용확정여부 = false,
                판정사유 = "자동 판정 변경 시도"
            }));

        Assert.Contains("악용을 확정", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 악용을확정한_운영자검토는_원장과지표재구성을남긴다()
    {
        await using var db = CreateContext();
        db.음식배달시도.Add(InterruptedAttempt());
        await db.SaveChangesAsync();
        var dispatch = new OperationalDispatchStub();
        var useCase = CreateUseCase(db, dispatch);
        var requestId = Guid.NewGuid();

        var result = await useCase.검토Async(
            "attempt-1",
            new 음식배달중단검토요청
            {
                클라이언트요청Id = requestId,
                예상Revision = 2,
                판정Code = 음식배달중단검토판정Code.기사책임,
                악용확정여부 = true,
                판정사유 = "사람 검토로 고의적인 허위 사고 신고 확인"
            });

        Assert.NotNull(result);
        Assert.Equal(운영배차책임Code.기사, result.책임Code);
        Assert.True(result.악용확정여부);
        Assert.Equal(3, result.Revision);
        Assert.Equal("driver-1", dispatch.LastMetricSubjectId);
        var activity = Assert.Single(db.운영배차활동사건);
        Assert.Equal($"dispatch-responsibility-review:{requestId:N}", activity.사건StableId);
        Assert.Equal("attempt-1", activity.업무시도Id);
    }

    private static 음식배달중단검토UseCase CreateUseCase(
        SsalddelContext db,
        OperationalDispatchStub dispatch)
        => new(
            db,
            new CurrentUserStub(),
            dispatch,
            NullLogger<음식배달중단검토UseCase>.Instance);

    private static 음식배달시도 InterruptedAttempt()
        => new()
        {
            시도StableId = "attempt-1",
            주문번호 = "order-1",
            제안Id = "offer-1",
            기사Id = "driver-1",
            추천라운드 = 1,
            시도순번 = 1,
            상태Code = 음식배달시도상태Code.중단,
            Revision = 2,
            수락시각Utc = DateTime.UtcNow.AddMinutes(-10),
            중단시각Utc = DateTime.UtcNow.AddMinutes(-1),
            중단사유Code = 음식배달중단사유Code.사고,
            책임Code = 운영배차책임Code.보호대상,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-interruption-review-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class CurrentUserStub : ICurrentUserAccessor
    {
        public string? UserId => "admin-1";
        public string? Role => "Admin";
    }

    private sealed class OperationalDispatchStub : I운영배차공통UseCase
    {
        public string? LastMetricSubjectId { get; private set; }

        public Task<운영배차단기지표Dto> 단기지표조회Async(string 주체Id, CancellationToken cancellationToken = default)
        {
            LastMetricSubjectId = 주체Id;
            return Task.FromResult(new 운영배차단기지표Dto());
        }

        public Task<운영배차수신상태Dto> 수신상태조회Async(string 주체Id, CancellationToken cancellationToken = default)
            => Task.FromResult(new 운영배차수신상태Dto());
        public Task<운영배차수신상태Dto> 기사의사변경Async(string 기사Id, 운영배차수신의사변경요청 요청, CancellationToken cancellationToken = default)
            => Task.FromResult(new 운영배차수신상태Dto());
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string 주체Id, Guid 요청Id, string 실효상태Code, string 실효사유Code, CancellationToken cancellationToken = default)
            => Task.FromResult(new 운영배차수신상태Dto());
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto 사건, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
