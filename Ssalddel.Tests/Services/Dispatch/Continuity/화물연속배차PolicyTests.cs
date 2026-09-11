using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Continuity;

public sealed class 화물연속배차PolicyTests
{
    private readonly 화물연속배차Policy _policy = new();

    [Fact]
    public void 시간약속은_서비스시간과경로에_잠금완충을더한다()
    {
        var start = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);

        var result = _policy.시간약속계산(start, 10, 60, 10, 0.2m, 15, null);

        Assert.Equal(start.AddMinutes(80), result.목표도착시각Utc);
        Assert.Equal(start.AddMinutes(95), result.최종도착한계시각Utc);
        Assert.Equal(15, result.잠금완충분);
    }

    [Fact]
    public void 화주한계가_목표보다빠르면_시간약속을만들지않는다()
    {
        var start = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);

        Assert.Throws<InvalidOperationException>(() =>
            _policy.시간약속계산(start, 10, 60, 10, 0.2m, 15, start.AddMinutes(70)));
    }

    [Theory]
    [InlineData(70, 80, 화물시간위험Code.정상)]
    [InlineData(85, 90, 화물시간위험Code.주의)]
    [InlineData(95, 101, 화물시간위험Code.위험)]
    public void 위험판정은_보수예상도착과_잠금한계를비교한다(
        int 예상분,
        int 보수예상분,
        string expected)
    {
        var start = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);

        var actual = _policy.위험판정(
            start.AddMinutes(80),
            start.AddMinutes(100),
            start.AddMinutes(예상분),
            start.AddMinutes(보수예상분));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 최소지급액이_제시운임보다크면_추천을차단한다()
    {
        var result = _policy.최소지급액계산(
            차량기본액: 10_000m,
            공차거리Km: 5m,
            공차Km단가: 500m,
            적재거리Km: 10m,
            적재Km단가: 700m,
            예상업무시간분: 60m,
            시간당단가: 12_000m,
            톨비: 2_000m,
            수작업비: 0m,
            대기료: 0m,
            할증: 0m,
            제시운임: 30_000m,
            승인프로모션보전액: 0m);

        Assert.Equal(33_500m, result.기사최소지급액);
        Assert.Equal(3_500m, result.운임부족금액);
        Assert.False(result.추천가능);
    }
}
