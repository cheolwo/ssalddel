using System.Security.Cryptography;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "승인된 합성 이동망과 진행·차단·복원·시간 누적 후보를 검증한다.",
    Boundary = "독립 지원 모듈 시험이며 실제 기사·배송·Session Save/Replay·Unity 증거가 아니다.")]
public sealed class 가상동네이동Tests
{
    [Fact]
    public void 승인된작은동네는_건물셋과명시한접근로를가진다()
    {
        var map = 지도();
        Assert.Equal(3, map.Buildings.Count);
        Assert.Equal(9, map.Nodes.Count);
        Assert.Equal(8, map.Roads.Count);
        Assert.All(map.Buildings, x => Assert.Equal(4d, x.HeightMeters));
        Assert.True(map.Source.IsSynthetic);
        Assert.False(map.RuntimeAuthorized);
        foreach (var name in new[] { "restaurant", "residence-a", "residence-b" })
        {
            Assert.Equal(4, 경로(map, name + "-stop", name + "-door", 동네이동수단.Pedestrian).LengthMeters);
            Assert.False(경로(map, name + "-stop", name + "-door").Found);
        }
    }

    [Theory]
    [InlineData("residence-a-stop", 30)]
    [InlineData("residence-b-stop", 10)]
    public void 차량은_굴절된도로40미터를_여덟Tick동안진행한다(string destination, double endX)
    {
        var route = 경로(지도(), "restaurant-stop", destination);
        Assert.Equal(40, route.LengthMeters);
        var engine = new 동네이동진행Engine();
        var start = engine.시작(route);
        var state = start;
        for (var tick = 1; tick <= 8; tick++)
        {
            state = engine.진행(route, state, tick);
            Assert.Equal(tick * 5, state.DistanceMeters);
            Assert.Equal(tick == 8, state.ReachedCandidateEnd);
            Assert.False(state.RuntimeAuthorized);
        }
        Assert.Equal((endX, 20d), (state.Position.X, state.Position.Z));
        Assert.Equal(0, start.DistanceMeters); // 입력 사본 불변
        Assert.Equal(40, engine.진행(route, state, 9).DistanceMeters);
    }

    [Fact]
    public void 도보와복귀는_지정경로만사용한다()
    {
        var map = 지도();
        var route = 경로(map, "residence-a-stop", "residence-a-door", 동네이동수단.Pedestrian);
        var engine = new 동네이동진행Engine();
        var state = engine.시작(route);
        for (var i = 1; i <= 4; i++) state = engine.진행(route, state, i);
        Assert.Equal((30d, 24d), (state.Position.X, state.Position.Z));
        Assert.True(state.ReachedCandidateEnd);
        var back = 경로(map, "residence-a-stop", "depot");
        Assert.Equal(50, back.LengthMeters);
        var returning = engine.시작(back);
        for (var i = 1; i <= 10; i++) returning = engine.진행(back, returning, i);
        Assert.Equal((0d, 0d), (returning.Position.X, returning.Position.Z));
    }

    [Fact]
    public void 차단과중복요청은_위치를건너뛰거나시간을따라잡지않는다()
    {
        var route = 경로(지도());
        var engine = new 동네이동진행Engine();
        var first = engine.진행(route, engine.시작(route), 1);
        var blocked = engine.진행(route, first, 2, true);
        Assert.Equal(first.Position, blocked.Position);
        Assert.Equal("NeighborhoodMovementBlocked", blocked.ReasonCode);
        Assert.Same(blocked, engine.진행(route, blocked, 2, true));
        Assert.Throws<InvalidDataException>(() => engine.진행(route, blocked, 2, false));
        var stillBlocked = engine.진행(route, blocked, 3, true);
        Assert.Equal(5, stillBlocked.DistanceMeters);
        Assert.Equal(10, engine.진행(route, stillBlocked, 4).DistanceMeters);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(100)]
    public void Tick은_다음하나만허용한다(int nextTick)
    {
        var route = 경로(지도());
        var engine = new 동네이동진행Engine();
        Assert.Throws<InvalidDataException>(() => engine.진행(route, engine.시작(route), nextTick));
    }

    [Fact]
    public void 같은공간판본에서도_다른목적지경로를거부한다()
    {
        var map = 지도();
        var route = 경로(map);
        var another = 경로(map, "restaurant-stop", "residence-b-stop");
        Assert.Equal(route.Revision, another.Revision);
        var engine = new 동네이동진행Engine();
        var state = engine.시작(route);
        Assert.Throws<InvalidDataException>(() => engine.진행(another, state, 1));
        Assert.Throws<InvalidDataException>(() => engine.복원(another, state.RouteFingerprint, 0, 0, false));
    }

    [Fact]
    public void 동일경로에서_복원한진행은_후속계산이같다()
    {
        var route = 경로(지도());
        var engine = new 동네이동진행Engine();
        var state = engine.진행(route, engine.시작(route), 1);
        state = engine.진행(route, state, 2, true);
        var restored = engine.복원(route, state.RouteFingerprint, state.Tick, state.DistanceMeters, state.Blocked);
        var expected = engine.진행(route, state, 3);
        var actual = engine.진행(route, restored, 3);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.RouteFingerprint, actual.RouteFingerprint);
        Assert.Equal(expected.DistanceMeters, actual.DistanceMeters);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(1, -1)]
    [InlineData(1, 2.5)]
    [InlineData(100, 41)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    public void 잘못된진행복원을거부한다(int tick, double distance)
    {
        var route = 경로(지도());
        var engine = new 동네이동진행Engine();
        Assert.Throws<InvalidDataException>(() => engine.복원(route, engine.시작(route).RouteFingerprint, tick, distance, false));
    }

    [Fact]
    public void 없는경로와제자리경로를구분한다()
    {
        var map = 지도();
        var engine = new 동네이동진행Engine();
        Assert.Throws<InvalidDataException>(() => engine.시작(경로(map, "depot", "restaurant-door")));
        var state = engine.시작(경로(map, "depot", "depot"));
        Assert.True(state.ReachedCandidateEnd);
        Assert.False(state.RuntimeAuthorized);
        Assert.Equal(0, state.DistanceMeters);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void 프레임수와무관하게_초당한요청이다(int frames)
    {
        var clock = new 관찰시간누적기();
        var requests = Enumerable.Range(0, frames * 10).Count(_ => clock.Tick요청(1d / frames, true));
        Assert.Equal(10, requests);
    }

    [Fact]
    public void 정지와명령대기의누적은폐기하고_장시간프레임도한번만요청한다()
    {
        var clock = new 관찰시간누적기();
        Assert.False(clock.Tick요청(.8, true));
        Assert.False(clock.Tick요청(600, false));
        Assert.False(clock.Tick요청(.2, true));
        Assert.False(clock.Tick요청(.8, true, true));
        Assert.False(clock.Tick요청(.2, true));
        Assert.True(clock.Tick요청(600, true));
        Assert.False(clock.Tick요청(0, true));
        clock.초기화();
        Assert.False(clock.Tick요청(.8, true));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void 부적절한프레임시간은거부한다(double elapsed)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new 관찰시간누적기().Tick요청(elapsed, true));

    private static 동네공간Snapshot 지도()
    {
        using var stream = typeof(가상동네이동Tests).Assembly.GetManifestResourceStream("Ssalddel.Simulation.Tests.synthetic-delivery-block.v1.json")!;
        using var memory = new MemoryStream(); stream.CopyTo(memory);
        var bytes = memory.ToArray();
        return new 동네공간ImportService().읽기(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static 동네이동경로후보 경로(동네공간Snapshot map, string from = "restaurant-stop",
        string to = "residence-a-stop", 동네이동수단 mode = 동네이동수단.Vehicle)
        => new 동네이동경로Engine().탐색(map, map.Revision, from, to, mode);
}
