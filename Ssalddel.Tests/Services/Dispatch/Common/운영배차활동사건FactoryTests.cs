using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Tests.Services.Dispatch.Common;

public sealed class 운영배차활동사건FactoryTests
{
    [Fact]
    public void 같은_추천라운드의_같은사건은_StableId가_같다()
    {
        var occurredAt = new DateTime(2026, 9, 10, 1, 2, 3, DateTimeKind.Utc);

        var first = 운영배차활동사건Factory.유효제안("driver-1", "offer-1", 3, occurredAt);
        var retry = 운영배차활동사건Factory.유효제안("driver-1", "offer-1", 3, occurredAt.AddSeconds(5));
        var nextRound = 운영배차활동사건Factory.유효제안("driver-1", "offer-1", 4, occurredAt);

        Assert.Equal(first.사건StableId, retry.사건StableId);
        Assert.NotEqual(first.사건StableId, nextRound.사건StableId);
        Assert.DoesNotContain("driver-1", first.사건StableId, StringComparison.Ordinal);
        Assert.DoesNotContain("offer-1", first.사건StableId, StringComparison.Ordinal);
        Assert.Equal(운영배차제안유효성Code.유효, first.제안유효성Code);
        Assert.Equal(1, first.지표단위수);
    }

    [Theory]
    [InlineData(null, 운영배차거절사유Code.미입력)]
    [InlineData("", 운영배차거절사유Code.미입력)]
    [InlineData("DistanceBurden", 운영배차거절사유Code.거리부담)]
    [InlineData(" PersonalCircumstance ", 운영배차거절사유Code.개인사정)]
    public void 거절사유는_허용된코드로_정규화한다(string? input, string expected)
    {
        var item = 운영배차활동사건Factory.거절(
            "driver-1",
            "offer-1",
            1,
            DateTime.UtcNow,
            input);

        Assert.Equal(expected, item.사유Code);
        Assert.Equal(운영배차책임Code.기사, item.책임Code);
    }

    [Fact]
    public void 임의의_거절사유문장은_원장코드로_받지않는다()
    {
        Assert.Throws<ArgumentException>(() => 운영배차활동사건Factory.거절(
            "driver-1",
            "offer-1",
            1,
            DateTime.UtcNow,
            "오늘은 개인적으로 너무 힘들어서 거절합니다"));
    }

    [Fact]
    public void 수락과완료는_같은주문을_서로다른사건으로_기록한다()
    {
        var occurredAt = DateTime.UtcNow;
        var accepted = 운영배차활동사건Factory.수락(
            "driver-1",
            "offer-1",
            "order-1",
            1,
            occurredAt);
        var completed = 운영배차활동사건Factory.완료(
            "driver-1",
            "offer-1",
            "order-1",
            1,
            occurredAt.AddMinutes(20));

        Assert.NotEqual(accepted.사건StableId, completed.사건StableId);
        Assert.Equal(운영배차사건유형Code.수락, accepted.사건유형Code);
        Assert.Equal(운영배차사건유형Code.완료, completed.사건유형Code);
        Assert.Equal("order-1", accepted.주문Id);
        Assert.Equal("order-1", completed.주문Id);
    }

    [Theory]
    [InlineData("RestaurantDecision", "Valid", "Restaurant")]
    [InlineData("InventoryUnavailable", "Ineligible", "Restaurant")]
    [InlineData("OutsideBusinessHours", "Ineligible", "Restaurant")]
    [InlineData("KitchenCapacityUnavailable", "Ineligible", "Restaurant")]
    [InlineData("ServiceIncident", "ProtectedReason", "Protected")]
    public void 음식점거절은_사유에따라_유효성분모와책임을분리한다(
        string reason,
        string expectedValidity,
        string expectedResponsibility)
    {
        var item = 운영배차활동사건Factory.음식점거절(
            42,
            "food-order-1",
            DateTime.UtcNow,
            reason);

        Assert.Equal("restaurant:42", item.주체Id);
        Assert.Equal(운영배차주체역할Code.음식점, item.주체역할Code);
        Assert.Equal(expectedValidity, item.제안유효성Code);
        Assert.Equal(expectedResponsibility, item.책임Code);
        Assert.Equal(reason, item.사유Code);
    }
}
