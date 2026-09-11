using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 운영배차수신상태PolicyTests
{
    private readonly 운영배차수신상태Policy _policy = new();

    [Fact]
    public void 연결확인불가가_되어도_기사의_ON_의사를_보존한다()
    {
        var 시작 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시작);

        var 결과 = _policy.서버실효상태변경(
            on,
            운영배차실효상태Code.연결확인불가,
            "HeartbeatUnavailable",
            시작.AddMinutes(3));

        Assert.Equal(운영배차수신의사Code.On, 결과.수신의사Code);
        Assert.Equal(운영배차실효상태Code.연결확인불가, 결과.실효상태Code);
        Assert.Equal(on.수신의사변경시각Utc, 결과.수신의사변경시각Utc);
    }

    [Fact]
    public void OFF는_기사의_명시적_의사변경으로_기록한다()
    {
        var 시작 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var on = _policy.기사의사변경(null, "driver-1", 운영배차수신의사Code.On, 시작);
        var 일시정지 = _policy.서버실효상태변경(
            on,
            운영배차실효상태Code.서버일시정지,
            "ServerMaintenance",
            시작.AddMinutes(1));

        var 결과 = _policy.기사의사변경(
            일시정지,
            "driver-1",
            운영배차수신의사Code.Off,
            시작.AddMinutes(2));

        Assert.Equal(운영배차수신의사Code.Off, 결과.수신의사Code);
        Assert.Equal(운영배차실효상태Code.서버일시정지, 결과.실효상태Code);
        Assert.Equal(시작.AddMinutes(2), 결과.수신의사변경시각Utc);
    }

    [Fact]
    public void 다른_기사의_현재상태를_바꾸려면_거부한다()
    {
        var 기준시각 = new DateTimeOffset(2026, 9, 10, 1, 0, 0, TimeSpan.Zero);
        var 현재 = _policy.기사의사변경(
            null,
            "driver-1",
            운영배차수신의사Code.On,
            기준시각);

        Assert.Throws<InvalidOperationException>(() =>
            _policy.기사의사변경(
                현재,
                "driver-2",
                운영배차수신의사Code.Off,
                기준시각.AddMinutes(1)));
    }
}
