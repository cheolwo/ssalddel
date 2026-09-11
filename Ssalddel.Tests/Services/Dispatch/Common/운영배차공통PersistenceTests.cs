using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Ssalddel.Controllers.Driver.Settings05;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Infrastructure.Persistence;
using Ssalddel.Infrastructure.Storage.Memory;
using 살뜰.Infrastructure.Storage.Redis;
using Ssalddel.Extensions;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Services.Dispatch.Common;

public sealed class 운영배차공통PersistenceTests
{
    [Fact]
    public async Task 기사_ON은_원장에_멱등저장되고_빈_투영에서_재구성된다()
    {
        using var db = CreateContext();
        var ledger = new Ef운영배차활동원장Store(db);
        var now = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var request = new 운영배차수신의사변경요청
        {
            클라이언트요청Id = Guid.Parse("2fcb6427-4597-4be0-a892-5040c0d37fc8"),
            수신의사Code = 운영배차수신의사Code.On
        };
        var useCase = CreateUseCase(ledger, new InMemory운영배차판정ProjectionStore(), now);

        var first = await useCase.기사의사변경Async("driver-1", request);
        var retry = await useCase.기사의사변경Async("driver-1", request);
        var rebuilt = await CreateUseCase(
                ledger,
                new InMemory운영배차판정ProjectionStore(),
                now.AddMinutes(1))
            .수신상태조회Async("driver-1");

        Assert.Equal(운영배차수신의사Code.On, first.수신의사Code);
        Assert.Equal(운영배차수신의사Code.On, retry.수신의사Code);
        Assert.Equal(운영배차수신의사Code.On, rebuilt.수신의사Code);
        Assert.Single(db.운영배차활동사건);
    }

    [Fact]
    public async Task 연결확인불가는_영속원장과_재구성뒤에도_ON을_바꾸지_않는다()
    {
        using var db = CreateContext();
        var ledger = new Ef운영배차활동원장Store(db);
        var now = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var useCase = CreateUseCase(ledger, new InMemory운영배차판정ProjectionStore(), now);
        await useCase.기사의사변경Async("driver-1", new 운영배차수신의사변경요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            수신의사Code = 운영배차수신의사Code.On
        });

        var unavailable = await useCase.서버실효상태변경Async(
            "driver-1",
            Guid.NewGuid(),
            운영배차실효상태Code.연결확인불가,
            "HeartbeatUnavailable");
        var rebuilt = await CreateUseCase(
                ledger,
                new InMemory운영배차판정ProjectionStore(),
                now.AddMinutes(1))
            .수신상태조회Async("driver-1");

        Assert.Equal(운영배차수신의사Code.On, unavailable.수신의사Code);
        Assert.Equal(운영배차수신의사Code.On, rebuilt.수신의사Code);
        Assert.Equal(운영배차실효상태Code.연결확인불가, rebuilt.실효상태Code);
    }

    [Fact]
    public async Task Redis_투영이_실패해도_원장에_ON을_기록하고_응답한다()
    {
        using var db = CreateContext();
        var ledger = new Ef운영배차활동원장Store(db);
        var useCase = CreateUseCase(
            ledger,
            new 실패운영배차판정ProjectionStore(),
            new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero));

        var result = await useCase.기사의사변경Async("driver-1", new 운영배차수신의사변경요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            수신의사Code = 운영배차수신의사Code.On
        });

        Assert.Equal(운영배차수신의사Code.On, result.수신의사Code);
        Assert.Single(db.운영배차활동사건);
    }

    [Fact]
    public async Task 활동사건을_원장에_기록하면_오늘_유효거절률을_다시_투영한다()
    {
        using var db = CreateContext();
        var ledger = new Ef운영배차활동원장Store(db);
        var now = new DateTimeOffset(2026, 9, 10, 3, 0, 0, TimeSpan.Zero);
        var useCase = CreateUseCase(ledger, new InMemory운영배차판정ProjectionStore(), now);

        await useCase.활동사건기록Async(Event("offer-1", 운영배차사건유형Code.제안전달, now.AddMinutes(-5)));
        await useCase.활동사건기록Async(Event("reject-1", 운영배차사건유형Code.거절, now.AddMinutes(-4)));
        await useCase.활동사건기록Async(Event(
            "invalid-reject",
            운영배차사건유형Code.거절,
            now.AddMinutes(-3),
            운영배차제안유효성Code.조건부적합));

        var metrics = await useCase.단기지표조회Async("driver-1");

        Assert.Equal(1, metrics.오늘.유효제안수);
        Assert.Equal(1, metrics.오늘.거절수);
        Assert.Equal(1m, metrics.오늘.거절률);
        Assert.Equal(3, await db.운영배차활동사건.CountAsync());
    }

    [Fact]
    public async Task 업무트랜잭션이_직접추가한사건은_기존투영보다_원장을우선해_다시계산한다()
    {
        using var db = CreateContext();
        var ledger = new Ef운영배차활동원장Store(db);
        var projection = new InMemory운영배차판정ProjectionStore();
        var now = new DateTimeOffset(2026, 9, 10, 3, 0, 0, TimeSpan.Zero);
        await projection.단기지표저장Async("driver-1", new 운영배차단기지표Dto
        {
            생성시각Utc = now.AddMinutes(-10),
            오늘 = new 운영배차일별지표Dto
            {
                운영시장날짜 = "2026-09-10",
                유효제안수 = 99,
                거절수 = 99,
                거절률 = 1m
            }
        });
        db.운영배차활동사건.Add(운영배차활동사건Factory.유효제안(
            "driver-1",
            "offer-live",
            1,
            now.AddMinutes(-2).UtcDateTime));
        await db.SaveChangesAsync();

        var metrics = await CreateUseCase(ledger, projection, now)
            .단기지표조회Async("driver-1");

        Assert.Equal(1, metrics.오늘.유효제안수);
        Assert.Equal(0, metrics.오늘.거절수);
        Assert.Equal(0m, metrics.오늘.거절률);
    }

    [Fact]
    public void 기사_API는_본인_상태와_지표만_노출하고_기사역할을_요구한다()
    {
        var type = typeof(기사운영배차공통Controller);

        Assert.Equal("api/v1/driver/operational-dispatch", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal(역할명.기사, type.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.NotNull(type.GetMethod(nameof(기사운영배차공통Controller.수신상태조회))
            ?.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(type.GetMethod(nameof(기사운영배차공통Controller.수신의사변경))
            ?.GetCustomAttribute<HttpPutAttribute>());
        Assert.NotNull(type.GetMethod(nameof(기사운영배차공통Controller.단기지표조회))
            ?.GetCustomAttribute<HttpGetAttribute>());
        Assert.DoesNotContain(type.GetMethods(), method => method.Name.Contains("사건기록", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Memory", typeof(InMemory운영배차판정ProjectionStore))]
    [InlineData("Redis", typeof(Redis운영배차판정ProjectionStore))]
    public void 임시상태_Provider에_맞는_운영배차_투영을_등록한다(
        string provider,
        Type expectedImplementation)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test;User=test;Password=test",
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:Database"] = "test",
                ["TransientState:Provider"] = provider,
                ["Redis:ConnectionString"] = "localhost:16379"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSsalddelPersistence(configuration);

        var descriptor = Assert.Single(services, x => x.ServiceType == typeof(I운영배차판정ProjectionStore));
        Assert.Equal(expectedImplementation, descriptor.ImplementationType);
        Assert.Contains(services, x => x.ServiceType == typeof(I운영배차활동원장Store)
                                       && x.ImplementationType == typeof(Ef운영배차활동원장Store));
        Assert.Contains(services, x => x.ServiceType == typeof(I운영배차공통UseCase)
                                       && x.ImplementationType == typeof(운영배차공통UseCase));
    }

    private static 운영배차활동사건Dto Event(
        string id,
        string type,
        DateTimeOffset occurredAt,
        string validity = 운영배차제안유효성Code.유효)
        => new()
        {
            사건StableId = id,
            발생시각Utc = occurredAt,
            주체Id = "driver-1",
            주체역할Code = 운영배차주체역할Code.기사,
            사건유형Code = type,
            제안유효성Code = validity,
            책임Code = 운영배차책임Code.해당없음,
            사유Code = "test"
        };

    private static 운영배차공통UseCase CreateUseCase(
        I운영배차활동원장Store ledger,
        I운영배차판정ProjectionStore projection,
        DateTimeOffset now)
        => new(
            ledger,
            projection,
            new 운영배차수신상태Policy(),
            new 운영배차단기지표Calculator(),
            new FixedTimeProvider(now),
            NullLogger<운영배차공통UseCase>.Instance);

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"operational-dispatch-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryptionService());

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class 실패운영배차판정ProjectionStore : I운영배차판정ProjectionStore
    {
        public ValueTask<운영배차수신상태Dto?> 수신상태조회Async(string 주체Id, CancellationToken cancellationToken = default)
            => ValueTask.FromException<운영배차수신상태Dto?>(new InvalidOperationException("redis unavailable"));

        public ValueTask 수신상태저장Async(운영배차수신상태Dto 상태, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new InvalidOperationException("redis unavailable"));

        public ValueTask<운영배차단기지표Dto?> 단기지표조회Async(string 주체Id, CancellationToken cancellationToken = default)
            => ValueTask.FromException<운영배차단기지표Dto?>(new InvalidOperationException("redis unavailable"));

        public ValueTask 단기지표저장Async(string 주체Id, 운영배차단기지표Dto 지표, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new InvalidOperationException("redis unavailable"));
    }
}
