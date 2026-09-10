using System.Text.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "음식 배달 관찰 경로의 위치·차단·복귀와 권위 주문 상태 읽기를 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀,
    Boundary = "엔진 독립 시험이며 실제 Unity 배치·이동·배차·수령 완료는 증명하지 않는다.")]
public sealed class 음식배달관찰Tests
{
    [Theory]
    [InlineData("A", 30)]
    [InlineData("B", 10)]
    public void 양주택의_차도와_현관이_분리된다(string home, double x)
    {
        var route = 음식배달관찰표본.주택(home);
        Assert.Equal(40, route.VehicleRoute.Length);
        Assert.Equal(4, route.EntranceRoute.Length);
        Assert.Equal(x, route.정차위치.X);
        Assert.Equal(20, route.정차위치.Z);
        Assert.Equal(24, route.인계접근위치(1).Z);
    }

    [Fact]
    public void 도로_회전을_따르고_건물을_가로지르는_직선을_쓰지_않는다()
    {
        var route = 음식배달관찰표본.주택("A").VehicleRoute;
        var midpoint = route.위치(.5);
        Assert.Equal(20, midpoint.X);
        Assert.Equal(10, midpoint.Z);
        Assert.Equal(20, route.위치(.25).X);
        Assert.Equal(0, route.위치(.25).Z);
        Assert.Equal(20, route.위치(.75).Z);
    }

    [Fact]
    public void 역방향_복귀는_같은_도로의_역순이며_같은_입력은_같은_위치()
    {
        var route = 음식배달관찰표본.주택("B").VehicleRoute;
        var back = route.역방향("return");
        for (var i = 0; i <= 10; i++)
        {
            var point = route.위치(i / 10d);
            var replay = back.위치(1 - i / 10d);
            Assert.Equal(point.X, replay.X, 8);
            Assert.Equal(point.Z, replay.Z, 8);
        }
    }

    [Fact]
    public void 폐쇄_단절_모르는목적지는_대체경로없이_차단()
    {
        var route = 음식배달관찰표본.음식점();
        Assert.Throws<InvalidOperationException>(() => route.VehicleRoute.위치(.2, true));
        Assert.Throws<ArgumentException>(() => 음식배달관찰표본.주택("C"));
        Assert.Throws<ArgumentException>(() => new 음식배달관찰동선(route.VehicleRoute,
            음식배달관찰표본.주택("A").EntranceRoute));
        Assert.Throws<ArgumentException>(() => new 음식배달관찰경로("route", "r1", new 배달평면위치(0, 0)));
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void 잘못된_진행률_거부(double progress)
        => Assert.Throws<ArgumentOutOfRangeException>(() => 음식배달관찰표본.음식점().VehicleRoute.위치(progress));

    [Fact]
    public void 원본배열변경과_잘못된좌표_방어()
    {
        var points = new[] { new 배달평면위치(0, 0), new 배달평면위치(10, 0) };
        var route = new 음식배달관찰경로("route", "r1", points);
        points[1] = new 배달평면위치(100, 100);
        Assert.Equal(10, route.위치(1).X);
        Assert.Throws<ArgumentException>(() => new 음식배달관찰경로("route", "r1",
            new 배달평면위치(0, 0), new 배달평면위치(double.NaN, 1)));
        Assert.Throws<ArgumentException>(() => new 음식배달관찰경로("route", "r1",
            new 배달평면위치(0, 0), new 배달평면위치(0, 0)));
    }

    [Theory]
    [InlineData("전달완료", false)]
    [InlineData("수령확인", true)]
    public void 기존주문_상태만_읽고_위치도착으로_수령을_확정하지_않는다(string state, bool received)
    {
        var source = new Simulation음식배달Snapshot
        {
            FoodOrderStableId = "food-order:test", Revision = 3, StateCode = state,
            DeliveredTick = 6, ReceivedTick = received ? 7 : null
        };
        var before = JsonSerializer.Serialize(source);
        _ = 음식배달관찰표본.주택("A").VehicleRoute.위치(1);
        var result = new 음식배달관찰상태(source);
        Assert.Equal(received, result.ReceiptConfirmed);
        Assert.Equal(3, result.OrderRevision);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        var restored = JsonSerializer.Deserialize<Simulation음식배달Snapshot>(before)!;
        Assert.Equal(result.Description, new 음식배달관찰상태(restored).Description);
    }

    [Fact]
    public void 증거없는_수령과_알수없는_상태는_실패()
    {
        var source = new Simulation음식배달Snapshot { FoodOrderStableId = "order", StateCode = "수령확인" };
        Assert.Throws<ArgumentException>(() => new 음식배달관찰상태(source));
        source.StateCode = "unrecognized";
        Assert.Throws<ArgumentException>(() => new 음식배달관찰상태(source));
    }
}
