using MediatR;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Application.Food;

public sealed class 주문자음식주문취소CommandHandlerTests
{
    [Fact]
    public async Task 음식점수락전에는_주문자본인취소를_멱등처리한다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var publisher = new RecordingPublisher();
        var handler = new 주문자음식주문취소CommandHandler(store, publisher);
        var order = store.AddOrder(CreateOrder());
        var command = new 주문자음식주문취소Command(
            order.주문번호,
            new 주문자음식주문취소요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                사유Code = 운영배차주문자취소사유Code.중복주문,
                사유 = "같은 주문을 두 번 접수함"
            },
            "orderer-1");

        var first = await handler.Handle(command, default);
        var retry = await handler.Handle(command, default);

        Assert.Equal(음식주문상태코드.취소, first?.상태);
        Assert.Equal(first?.Revision, retry?.Revision);
        var notification = Assert.IsType<주문자음식주문취소됨Event>(
            Assert.Single(publisher.Notifications));
        Assert.Equal(운영배차주문자취소사유Code.중복주문, notification.사유Code);
    }

    [Fact]
    public async Task 다른주문자이거나_음식점수락후이면_직접취소하지않는다()
    {
        var store = new InMemorySsalddelFoodOrderStore();
        var handler = new 주문자음식주문취소CommandHandler(store, new RecordingPublisher());
        var order = store.AddOrder(CreateOrder());
        var payload = new 주문자음식주문취소요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            사유Code = 운영배차주문자취소사유Code.단순변심
        };

        Assert.Null(await handler.Handle(
            new 주문자음식주문취소Command(order.주문번호, payload, "other-orderer"),
            default));

        store.음식점수락(
            order.주문번호,
            new 음식점주문수락요청 { 음식점명 = "공통 식당", 조리예상분 = 20 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new 주문자음식주문취소Command(
                order.주문번호,
                new 주문자음식주문취소요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    사유Code = 운영배차주문자취소사유Code.단순변심
                },
                "orderer-1"),
            default));
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

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
