using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 운영배차단기지표CalculatorTests
{
    private readonly 운영배차단기지표Calculator _calculator = new();
    private readonly TimeZoneInfo _marketTimeZone = 운영배차단기지표Calculator.대한민국시간대조회();

    [Fact]
    public void 운영시장달력기준으로_오늘어제그제를_나눈다()
    {
        var now = Utc(2026, 9, 9, 15, 30); // 2026-09-10 00:30 KST
        var result = _calculator.계산(
            [
                Event("today", Utc(2026, 9, 9, 15, 0), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효),
                Event("yesterday", Utc(2026, 9, 9, 14, 59), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효),
                Event("day-before", Utc(2026, 9, 8, 14, 59), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효),
                Event("older", Utc(2026, 9, 7, 14, 59), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효)
            ],
            now,
            _marketTimeZone);

        Assert.Equal("2026-09-10", result.오늘.운영시장날짜);
        Assert.Equal("2026-09-09", result.어제.운영시장날짜);
        Assert.Equal("2026-09-08", result.그제.운영시장날짜);
        Assert.Equal(1, result.오늘.유효제안수);
        Assert.Equal(1, result.어제.유효제안수);
        Assert.Equal(1, result.그제.유효제안수);
    }

    [Fact]
    public void 유효제안만_거절률분모와거절수에_포함한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event("offer-valid-1", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효),
                Event("offer-valid-2", Utc(2026, 9, 10, 1, 1), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효),
                Event("offer-invalid", Utc(2026, 9, 10, 1, 2), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.조건부적합),
                Event("reject-valid", Utc(2026, 9, 10, 1, 3), 운영배차사건유형Code.거절, 운영배차제안유효성Code.유효),
                Event("reject-delivery-failed", Utc(2026, 9, 10, 1, 4), 운영배차사건유형Code.거절, 운영배차제안유효성Code.전달실패)
            ],
            now,
            _marketTimeZone);

        Assert.Equal(2, result.오늘.유효제안수);
        Assert.Equal(1, result.오늘.거절수);
        Assert.Equal(0.5m, result.오늘.거절률);
    }

    [Fact]
    public void 수락률과_거절률은_같은유효제안분모를_사용한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event("offer-1", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효, orderId: "order-1"),
                Event("offer-2", Utc(2026, 9, 10, 1, 1), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효, orderId: "order-2"),
                Event("accept-1", Utc(2026, 9, 10, 1, 2), 운영배차사건유형Code.수락, orderId: "order-1"),
                Event("reject-2", Utc(2026, 9, 10, 1, 3), 운영배차사건유형Code.거절, 운영배차제안유효성Code.유효, orderId: "order-2")
            ],
            now,
            _marketTimeZone);

        Assert.Equal(0.5m, result.오늘.수락률);
        Assert.Equal(0.5m, result.오늘.거절률);
    }

    [Fact]
    public void 수락완료와_책임별중단_균형반환을_서로분리한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event("accept-1", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.수락, orderId: "order-1"),
                Event("accept-2", Utc(2026, 9, 10, 1, 1), 운영배차사건유형Code.수락, orderId: "order-2"),
                Event("accept-3", Utc(2026, 9, 10, 1, 2), 운영배차사건유형Code.수락, orderId: "order-3"),
                Event("accept-4", Utc(2026, 9, 10, 1, 3), 운영배차사건유형Code.수락, orderId: "order-4"),
                Event("complete", Utc(2026, 9, 10, 2, 0), 운영배차사건유형Code.완료, orderId: "order-1"),
                Event("driver-stop", Utc(2026, 9, 10, 2, 1), 운영배차사건유형Code.중단, responsibility: 운영배차책임Code.기사, orderId: "order-2"),
                Event("external-cancel", Utc(2026, 9, 10, 2, 2), 운영배차사건유형Code.취소, responsibility: 운영배차책임Code.주문자, orderId: "order-3"),
                Event("release", Utc(2026, 9, 10, 2, 3), 운영배차사건유형Code.균형건수반환, responsibility: 운영배차책임Code.주문자, orderId: "order-3"),
                Event("protected", Utc(2026, 9, 10, 2, 4), 운영배차사건유형Code.중단, responsibility: 운영배차책임Code.보호대상, orderId: "order-4")
            ],
            now,
            _marketTimeZone);

        Assert.Equal(4, result.오늘.수락수);
        Assert.Equal(1, result.오늘.완료수);
        Assert.Equal(0.5m, result.오늘.수락후완료율);
        Assert.Equal(1, result.오늘.기사책임중단수);
        Assert.Equal(1, result.오늘.외부책임취소수);
        Assert.Equal(1, result.오늘.보호중단수);
        Assert.Equal(4, result.오늘.균형점유수);
        Assert.Equal(1, result.오늘.균형반환수);
        Assert.Equal(3, result.오늘.균형순점유수);
    }

    [Fact]
    public void 보호중단은_균형건수를반환하지만_악용확정후에는_기사책임으로다시계산한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event("accept", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.수락,
                    orderId: "order-1", attemptId: "attempt-1"),
                Event("stop", Utc(2026, 9, 10, 1, 5), 운영배차사건유형Code.중단,
                    responsibility: 운영배차책임Code.보호대상, orderId: "order-1", attemptId: "attempt-1"),
                Event("release", Utc(2026, 9, 10, 1, 5), 운영배차사건유형Code.균형건수반환,
                    responsibility: 운영배차책임Code.보호대상, orderId: "order-1", attemptId: "attempt-1"),
                Event("review", Utc(2026, 9, 10, 2, 0), 운영배차사건유형Code.책임판정,
                    responsibility: 운영배차책임Code.기사, orderId: "order-1", attemptId: "attempt-1")
            ],
            now,
            _marketTimeZone);

        Assert.Equal(1, result.오늘.기사책임중단수);
        Assert.Equal(0, result.오늘.보호중단수);
        Assert.Equal(0, result.오늘.균형반환수);
        Assert.Equal(1, result.오늘.균형순점유수);
        Assert.Equal(0m, result.오늘.수락후완료율);
    }

    [Fact]
    public void 오늘완료된주문도_어제수락Cohort의완료율에_반영한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event("accepted-yesterday", Utc(2026, 9, 9, 14, 0), 운영배차사건유형Code.수락, orderId: "overnight-order"),
                Event("completed-today", Utc(2026, 9, 9, 16, 0), 운영배차사건유형Code.완료, orderId: "overnight-order")
            ],
            now,
            _marketTimeZone);

        Assert.Equal(1, result.어제.수락수);
        Assert.Equal(1, result.어제.완료수);
        Assert.Equal(1m, result.어제.수락후완료율);
        Assert.Equal(0, result.오늘.완료수);
    }

    [Fact]
    public void 같은사건재처리는_한번만계산한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var item = Event("same-event", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효);
        var copy = Event("same-event", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.제안전달, 운영배차제안유효성Code.유효);

        var result = _calculator.계산([item, copy], now, _marketTimeZone);

        Assert.Equal(1, result.오늘.유효제안수);
    }

    [Fact]
    public void 같은식별자의_다른사건은_거부한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);

        Assert.Throws<InvalidOperationException>(() => _calculator.계산(
            [
                Event("conflict", Utc(2026, 9, 10, 1, 0), 운영배차사건유형Code.수락, orderId: "order-conflict"),
                Event("conflict", Utc(2026, 9, 10, 1, 1), 운영배차사건유형Code.완료)
            ],
            now,
            _marketTimeZone));
    }

    [Fact]
    public void 분모가없으면_비율을만들지않는다()
    {
        var result = _calculator.계산([], Utc(2026, 9, 10, 3, 0), _marketTimeZone);

        Assert.Null(result.오늘.거절률);
        Assert.Null(result.오늘.수락률);
        Assert.Null(result.오늘.수락후완료율);
        Assert.Equal(0, result.오늘.균형순점유수);
    }

    [Fact]
    public void 제안후_조건부적합이확인되면_거절률분모에서도제외한다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event(
                    "restaurant-offer",
                    Utc(2026, 9, 10, 1, 0),
                    운영배차사건유형Code.제안전달,
                    운영배차제안유효성Code.유효,
                    orderId: "food-order-1",
                    subjectId: "restaurant:42",
                    subjectRole: 운영배차주체역할Code.음식점),
                Event(
                    "inventory-reject",
                    Utc(2026, 9, 10, 1, 1),
                    운영배차사건유형Code.거절,
                    운영배차제안유효성Code.조건부적합,
                    운영배차책임Code.음식점,
                    "food-order-1",
                    "restaurant:42",
                    운영배차주체역할Code.음식점)
            ],
            now,
            _marketTimeZone);

        Assert.Equal(0, result.오늘.유효제안수);
        Assert.Equal(0, result.오늘.거절수);
        Assert.Null(result.오늘.거절률);
    }

    [Fact]
    public void 같은주문이라도_음식점수락과기사완료를_서로다른Cohort로본다()
    {
        var now = Utc(2026, 9, 10, 3, 0);
        var result = _calculator.계산(
            [
                Event(
                    "restaurant-accept",
                    Utc(2026, 9, 10, 1, 0),
                    운영배차사건유형Code.수락,
                    orderId: "food-order-1",
                    subjectId: "restaurant:42",
                    subjectRole: 운영배차주체역할Code.음식점),
                Event(
                    "driver-complete",
                    Utc(2026, 9, 10, 2, 0),
                    운영배차사건유형Code.완료,
                    orderId: "food-order-1",
                    subjectId: "driver-7")
            ],
            now,
            _marketTimeZone);

        Assert.Equal(1, result.오늘.수락수);
        Assert.Equal(0, result.오늘.완료수);
        Assert.Equal(0, result.오늘.균형점유수);
        Assert.Null(result.오늘.수락후완료율);
    }

    private static 운영배차활동사건Dto Event(
        string id,
        DateTimeOffset occurredAtUtc,
        string type,
        string validity = 운영배차제안유효성Code.해당없음,
        string responsibility = 운영배차책임Code.해당없음,
        string? orderId = null,
        string subjectId = "driver-1",
        string subjectRole = 운영배차주체역할Code.기사,
        string? attemptId = null)
        => new()
        {
            사건StableId = id,
            발생시각Utc = occurredAtUtc,
            운영시장시간대Id = 운영배차시장시간대Code.대한민국,
            주체Id = subjectId,
            주문Id = orderId,
            업무시도Id = attemptId,
            주체역할Code = subjectRole,
            사건유형Code = type,
            제안유효성Code = validity,
            책임Code = responsibility,
            지표단위수 = 1
        };

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
