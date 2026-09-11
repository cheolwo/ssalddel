using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점운영배차활동원장Tests
{
    [Fact]
    public async Task 주문등록과음식점수락은_멱등한공통활동사건으로저장된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var register = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            new NoOpPublisher(),
            database.Context);
        var request = CreateOrder();

        var order = await register.Handle(new 음식주문등록Command(request), default);
        await register.Handle(new 음식주문등록Command(request), default);
        var accept = new 음식점주문수락CommandHandler(
            store,
            new NoOpPublisher(),
            database.Context);
        var command = new 음식점주문수락Command(
            order.주문번호,
            new 음식점주문수락요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                음식점명 = "공통 식당",
                조리예상분 = 20
            },
            "restaurant-user");

        await accept.Handle(command, default);
        await accept.Handle(command, default);

        var events = await database.Context.운영배차활동사건
            .AsNoTracking()
            .OrderBy(x => x.발생시각Utc)
            .ToArrayAsync();
        Assert.Equal(2, events.Length);
        Assert.Equal(
            [운영배차사건유형Code.제안전달, 운영배차사건유형Code.수락],
            events.Select(x => x.사건유형Code).ToArray());
        Assert.All(events, x => Assert.Equal("restaurant:42", x.주체Id));
    }

    [Fact]
    public async Task 재고부족거절은_원장에는남지만_거절률분모와분자에서제외된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var register = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            new NoOpPublisher(),
            database.Context);
        var order = await register.Handle(new 음식주문등록Command(CreateOrder()), default);
        var reject = new 음식점주문진행변경CommandHandler(
            store,
            new NoOpPublisher(),
            database.Context);
        var command = new 음식점주문진행변경Command(
            order.주문번호,
            new 음식점주문진행변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                작업 = 음식점주문진행작업코드.거절,
                사유Code = 운영배차음식점거절사유Code.재고부족,
                사유 = "주문 직전 재고 소진"
            },
            "restaurant-user");

        await reject.Handle(command, default);
        await reject.Handle(command, default);

        var ledger = new Ef운영배차활동원장Store(database.Context);
        var events = await ledger.기간조회Async(
            "restaurant:42",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var metrics = new 운영배차단기지표Calculator().계산(
            events,
            DateTimeOffset.UtcNow,
            운영배차단기지표Calculator.대한민국시간대조회());

        Assert.Equal(2, events.Count);
        var rejection = Assert.Single(events, x => x.사건유형Code == 운영배차사건유형Code.거절);
        Assert.Equal(운영배차제안유효성Code.조건부적합, rejection.제안유효성Code);
        Assert.Equal(0, metrics.오늘.유효제안수);
        Assert.Equal(0, metrics.오늘.거절수);
        Assert.Null(metrics.오늘.거절률);
    }

    [Fact]
    public async Task 주문자취소는_주문자책임과음식점제안철회를_같은트랜잭션에남긴다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var register = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            new NoOpPublisher(),
            database.Context);
        var order = await register.Handle(new 음식주문등록Command(CreateOrder()), default);
        var cancel = new 주문자음식주문취소CommandHandler(
            store,
            new NoOpPublisher(),
            database.Context);
        var command = new 주문자음식주문취소Command(
            order.주문번호,
            new 주문자음식주문취소요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = order.Revision,
                사유Code = 운영배차주문자취소사유Code.단순변심,
                사유 = "수령 일정 변경"
            },
            "orderer-1");

        var result = await cancel.Handle(command, default);
        await cancel.Handle(command, default);

        Assert.Equal(음식주문상태코드.취소, result?.상태);
        var events = await database.Context.운영배차활동사건.AsNoTracking().ToArrayAsync();
        Assert.Equal(3, events.Length);
        Assert.Single(events, x =>
            x.주체역할Code == 운영배차주체역할Code.주문자
            && x.책임Code == 운영배차책임Code.주문자);
        var withdrawal = Assert.Single(events, x =>
            x.주체역할Code == 운영배차주체역할Code.음식점
            && x.사건유형Code == 운영배차사건유형Code.취소);
        Assert.Equal(운영배차제안유효성Code.보호사유, withdrawal.제안유효성Code);

        var ledger = new Ef운영배차활동원장Store(database.Context);
        var restaurantEvents = await ledger.기간조회Async(
            "restaurant:42",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var metrics = new 운영배차단기지표Calculator().계산(
            restaurantEvents,
            DateTimeOffset.UtcNow,
            운영배차단기지표Calculator.대한민국시간대조회());
        Assert.Equal(0, metrics.오늘.유효제안수);
        Assert.Equal(0, metrics.오늘.거절수);
    }

    private static 음식주문등록요청 CreateOrder() => new()
    {
        클라이언트요청Id = Guid.NewGuid(),
        음식점Id = 42,
        주문자UserId = "orderer-1",
        수령인정보 = new 음식주문수령인정보Dto
        {
            수령인명 = "주문자",
            연락처 = "010-0000-0000",
            주소 = "서울특별시 중구 세종대로 1"
        },
        상품목록 =
        [
            new 음식주문상품Dto { 메뉴Id = 1001, 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }
        ]
    };

    private sealed class PassThroughMenuValidationService : I음식주문메뉴검증Service
    {
        public Task<음식주문등록요청> 서버기준요청생성Async(
            음식주문등록요청 request,
            CancellationToken cancellationToken)
            => Task.FromResult(request);
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, SsalddelContext context)
        {
            _connection = connection;
            Context = context;
        }

        public SsalddelContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new SsalddelContext(
                new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,
                new DummyEncryption());
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
