using Ssalddel.WorkflowRules;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "다중 기사 후보 정렬·권역 확장과 자원 진입 후보의 결정성을 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3계약회귀,
    Boundary = "순수 계산 시험이며 실제 배차·점유·Unity·저장 재진입 증거가 아니다.")]
public sealed class 다중기사공통규칙Tests
{
    private static readonly DateTime Now = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 음수이동시간과중복점유요청은거부한다()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => 음식배달픽업평가Policy.예상소요시간판정(1, 0, Now, Now, -1, 0, 0));
        var request = new 이동자원진입요청("a", 0, "door");
        Assert.Throws<ArgumentException>(() => 이동자원점유Policy.판정(new Dictionary<string, string>(), new[] { request, request }));
    }
    private static 음식배달기사후보 Driver(string id, int area = 0, decimal distance = 1, decimal minutes = 2,
        bool available = true, bool fresh = true, bool route = true)
        => new(id, area, distance, minutes, 0, 5, available, fresh, route);

    [Fact]
    public void 가까운기사보다_조리시각에맞는기사를선택한다()
    {
        var result = 음식배달후보선정Policy.판정(new[] { Driver("near", minutes: 1),
            Driver("ready", distance: 2, minutes: 10), Driver("late", minutes: 20) }, Now, Now.AddMinutes(10), 5, 3, 0, 0);
        Assert.Equal("ready", result.선택기사Id);
    }

    [Theory]
    [InlineData(3, "adjacent")]
    [InlineData(1, "same")]
    public void 동일권후보가부족할때만_인접권을평가한다(int minimum, string selected)
    {
        var result = 음식배달후보선정Policy.판정(new[] { Driver("same", minutes: 1),
            Driver("adjacent", 1, minutes: 10), Driver("far", 2, minutes: 10) }, Now, Now.AddMinutes(10), 5, minimum, 0, 0);
        Assert.Equal(selected, result.선택기사Id);
        Assert.Equal("SearchAreaNotExpanded", result.평가목록.Single(x => x.후보.기사Id == "far").제외사유);
    }

    [Fact]
    public void 후보없을때만확장하며_부적격사유를반환한다()
    {
        var result = 음식배달후보선정Policy.판정(new[] { Driver("busy", available: false),
            Driver("stale", fresh: false), Driver("blocked", route: false), Driver("outside", distance: 6), Driver("expanded", 2) }, Now, null, 5);
        Assert.Equal("expanded", result.선택기사Id);
        Assert.Equal(4, result.평가목록.Count(x => !x.적격));
        Assert.Null(음식배달후보선정Policy.판정(new[] { Driver("blocked", route: false) }, Now, null, 5).선택기사Id);
    }

    [Fact]
    public void 입력순서가달라도동점선정은동일하고_중복기사는거부한다()
    {
        var input = new[] { Driver("b"), Driver("a") };
        Assert.Equal("a", 음식배달후보선정Policy.판정(input, Now, null, 5).선택기사Id);
        Assert.Equal("a", 음식배달후보선정Policy.판정(input.Reverse(), Now, null, 5).선택기사Id);
        Assert.Throws<ArgumentException>(() => 음식배달후보선정Policy.판정(new[] { input[0], input[0] }, Now, null, 5));
    }

    [Fact]
    public void 기존거리속도호출과명시소요시간은같은평가다()
    {
        var old = 음식배달픽업평가Policy.판정(2, 3, Now.AddMinutes(6), Now, 20, 1, 2);
        var current = 음식배달픽업평가Policy.예상소요시간판정(2, 3, Now.AddMinutes(6), Now, 6, 1, 2);
        Assert.Equal((old.단계, old.차이분, old.추천점수), (current.단계, current.차이분, current.추천점수));
    }

    [Theory]
    [InlineData("road:both-directions")]
    [InlineData("entrance:restaurant")]
    public void 같은자원은먼저기다린한기사만진입한다(string resource)
    {
        var input = new[] { new 이동자원진입요청("a", 2, resource), new 이동자원진입요청("b", 1, resource) };
        var occupied = new Dictionary<string, string>();
        var result = 이동자원점유Policy.판정(occupied, input);
        Assert.Equal("b", result.점유후보[resource]);
        Assert.Single(result.판정목록, x => x.진입가능);
        Assert.Empty(occupied);
        Assert.Equal("b", 이동자원점유Policy.판정(occupied, input.Reverse()).점유후보[resource]);
    }

    [Fact]
    public void 부분점유를하지않고_선행대기를추월하지않는다()
    {
        var occupied = new Dictionary<string, string> { ["road"] = "owner" };
        var result = 이동자원점유Policy.판정(occupied, new[] {
            new 이동자원진입요청("first", 1, "road", "door"), new 이동자원진입요청("second", 2, "door") });
        Assert.Single(result.점유후보);
        Assert.Equal("EarlierWaiter", result.판정목록[1].사유);
        var released = 이동자원점유Policy.판정(new Dictionary<string, string>(), new[] { new 이동자원진입요청("first", 1, "road", "door") });
        Assert.Equal(2, released.점유후보.Count);
    }

    [Fact]
    public void 기존점유는반복가능하지만_추가자원대기는금지한다()
    {
        var occupied = new Dictionary<string, string> { ["road"] = "a" };
        Assert.True(이동자원점유Policy.판정(occupied, new[] { new 이동자원진입요청("a", 1, "road") }).판정목록[0].진입가능);
        Assert.Equal("ReleaseBeforeNewReservation", 이동자원점유Policy.판정(occupied,
            new[] { new 이동자원진입요청("a", 1, "road", "door") }).판정목록[0].사유);
    }
}
