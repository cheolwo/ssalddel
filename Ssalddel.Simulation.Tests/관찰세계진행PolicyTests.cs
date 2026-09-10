using System.Text.Json;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "관찰 세계의 시간 상한·분할 정산·재시도와 접속 변경 규칙을 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증,
    Boundary = "순수 정책 시험이며 실제 서버 시계·성장·자원 소비 연결을 증명하지 않는다.")]
public sealed class 관찰세계진행PolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    private static 관찰세계진행Snapshot Create(bool connected = false) => new()
    {
        WorldId = "world:observer-test", OwnerId = "player:one", Enabled = true,
        SettledAtUtc = Start, Connected = connected,
        DisconnectedAtUtc = connected ? null : Start
    };

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(23, 23, false)]
    [InlineData(24, 24, true)]
    [InlineData(25, 24, true)]
    [InlineData(240, 24, true)]
    public void 미접속_개인누적만_24시간으로_제한(int hours, int personal, bool paused)
    {
        var result = 관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(hours));
        Assert.Equal(TimeSpan.FromHours(hours), result.WorldElapsed);
        Assert.Equal(TimeSpan.FromHours(personal), result.PersonalNpcElapsed);
        Assert.Equal(TimeSpan.Zero, result.PlayerActiveElapsed);
        Assert.False(result.PlayerAutonomyAllowed);
        Assert.Equal(paused, result.PersonalWorkPaused);
    }

    [Fact]
    public void 관찰만_해도_접속중이면_분신활동_가능()
    {
        var result = 관찰세계진행Policy.정산(Create(true), "player:one", Start.AddHours(48));
        Assert.Equal(TimeSpan.FromHours(48), result.PlayerActiveElapsed);
        Assert.Equal(result.WorldElapsed, result.PersonalNpcElapsed);
        Assert.True(result.PlayerAutonomyAllowed);
        Assert.False(result.PersonalWorkPaused);
    }

    [Fact]
    public void 분할정산과_한번정산이_동일하고_재시도는_중복없음()
    {
        var checkpoint = Create();
        var total = TimeSpan.Zero;
        for (var hour = 1; hour <= 48; hour++)
        {
            var part = 관찰세계진행Policy.정산(checkpoint, "player:one", Start.AddHours(hour));
            total += part.PersonalNpcElapsed;
            checkpoint = JsonSerializer.Deserialize<관찰세계진행Snapshot>(JsonSerializer.Serialize(part.Next))!;
        }
        Assert.Equal(관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(48)).PersonalNpcElapsed, total);
        var retry = 관찰세계진행Policy.정산(checkpoint, "player:one", Start.AddHours(48));
        Assert.Equal(TimeSpan.Zero, retry.WorldElapsed);
        Assert.Equal(TimeSpan.Zero, retry.PersonalNpcElapsed);
    }

    [Fact]
    public void 반복종료는_상한을_연장하지_않음()
    {
        var repeated = 관찰세계진행Policy.접속변경(Create(), "player:one", Start.AddHours(20), false);
        Assert.Equal(Start, repeated.Next.DisconnectedAtUtc);
        var next = 관찰세계진행Policy.정산(repeated.Next, "player:one", Start.AddHours(30));
        Assert.Equal(TimeSpan.FromHours(4), next.PersonalNpcElapsed);
    }

    [Fact]
    public void 재접속은_이전_미접속구간부터_정산하고_기본관찰로_복귀()
    {
        var initial = Create();
        initial.Control = 관찰세계제어방식.직접;
        var reconnect = 관찰세계진행Policy.접속변경(initial, "player:one", Start.AddHours(48), true);
        Assert.Equal(TimeSpan.FromHours(24), reconnect.PersonalNpcElapsed);
        Assert.Equal(TimeSpan.Zero, reconnect.PlayerActiveElapsed);
        Assert.Null(reconnect.Next.DisconnectedAtUtc);
        Assert.Equal(관찰세계제어방식.자율, reconnect.Next.Control);
        var online = 관찰세계진행Policy.정산(reconnect.Next, "player:one", Start.AddHours(49));
        Assert.Equal(TimeSpan.FromHours(1), online.PlayerActiveElapsed);
        Assert.Equal(TimeSpan.FromHours(1), online.PersonalNpcElapsed);
    }

    [Fact]
    public void 접속종료_직전은_접속시간으로_인정()
    {
        var result = 관찰세계진행Policy.접속변경(Create(true), "player:one", Start.AddHours(2), false);
        Assert.Equal(TimeSpan.FromHours(2), result.PlayerActiveElapsed);
        Assert.Equal(Start.AddHours(2), result.Next.DisconnectedAtUtc);
    }

    [Fact]
    public void 직접조작은_성장을_막지_않고_자율제어만_막음()
    {
        var change = 관찰세계진행Policy.제어변경(Create(true), "player:one", Start, 관찰세계제어방식.직접);
        var result = 관찰세계진행Policy.정산(change.Next, "player:one", Start.AddHours(1));
        Assert.False(result.PlayerAutonomyAllowed);
        Assert.Equal(TimeSpan.FromHours(1), result.PlayerActiveElapsed);
        Assert.Throws<InvalidOperationException>(() =>
            관찰세계진행Policy.제어변경(Create(), "player:one", Start, 관찰세계제어방식.직접));
    }

    [Fact]
    public void 소유권_역행시각_잘못된_저장상태_거부()
    {
        Assert.Throws<InvalidOperationException>(() => 관찰세계진행Policy.정산(Create(), "other", Start));
        Assert.Throws<InvalidOperationException>(() => 관찰세계진행Policy.정산(Create(), "player:one", Start.AddTicks(-1)));
        var invalid = Create();
        invalid.Connected = true;
        Assert.Throws<InvalidOperationException>(() => 관찰세계진행Policy.정산(invalid, "player:one", Start));
        invalid = Create();
        invalid.SchemaVersion = "unknown";
        Assert.Throws<InvalidOperationException>(() => 관찰세계진행Policy.정산(invalid, "player:one", Start));
    }

    [Fact]
    public void 입력상태는_불변이고_미설정프로필은_진행하지_않음()
    {
        var state = Create();
        state.Enabled = false;
        var before = JsonSerializer.Serialize(state);
        var result = 관찰세계진행Policy.정산(state, "player:one", Start.AddHours(48));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.Equal(TimeSpan.Zero, result.WorldElapsed);
        Assert.Equal(TimeSpan.Zero, result.PersonalNpcElapsed);
        Assert.True(result.PersonalWorkPaused);
        Assert.False(new 관찰세계진행Snapshot().Enabled);
    }

    [Fact]
    public void 상한_바로_전후의_정밀시간과_동일순간_재시도()
    {
        var first = 관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(24).AddTicks(-1));
        Assert.False(first.PersonalWorkPaused);
        var last = 관찰세계진행Policy.정산(first.Next, "player:one", Start.AddHours(24).AddTicks(1));
        Assert.Equal(TimeSpan.FromTicks(1), last.PersonalNpcElapsed);
        Assert.True(last.PersonalWorkPaused);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(23, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(240, 2)]
    public void 실행구간의_시간합은_기존_정산량과_동일(int hours, int windows)
    {
        var result = 관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(hours));
        Assert.Equal(windows, result.ExecutionWindows.Count);
        Assert.Equal(result.WorldElapsed.Ticks, result.ExecutionWindows.Sum(window => window.Elapsed.Ticks));
        Assert.Equal(result.PersonalNpcElapsed.Ticks,
            result.ExecutionWindows.Where(window => window.PersonalNpcWorkAllowed).Sum(window => window.Elapsed.Ticks));
        Assert.All(result.ExecutionWindows, window =>
        {
            Assert.True(window.EndedAtUtc > window.StartedAtUtc);
            Assert.False(window.PlayerActivityAllowed);
            Assert.False(window.PlayerAutonomyAllowed);
        });
    }

    [Fact]
    public void 상한을_넘긴_배치는_허용구간과_세계만_진행할_구간으로_나뉨()
    {
        var before = 관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(23));
        var result = 관찰세계진행Policy.정산(before.Next, "player:one", Start.AddHours(25));
        Assert.True(result.PersonalWorkPaused); // 종료 상태가 대기여도 앞의 허용 시간을 버리지 않는다.
        Assert.Collection(result.ExecutionWindows,
            active =>
            {
                Assert.Equal(Start.AddHours(23), active.StartedAtUtc);
                Assert.Equal(Start.AddHours(24), active.EndedAtUtc);
                Assert.True(active.PersonalNpcWorkAllowed);
            },
            paused =>
            {
                Assert.Equal(Start.AddHours(24), paused.StartedAtUtc);
                Assert.Equal(Start.AddHours(25), paused.EndedAtUtc);
                Assert.False(paused.PersonalNpcWorkAllowed);
            });
        var later = 관찰세계진행Policy.정산(result.Next, "player:one", Start.AddHours(26));
        Assert.False(Assert.Single(later.ExecutionWindows).PersonalNpcWorkAllowed);
    }

    [Fact]
    public void 재접속_구간은_과거의_미접속_권한을_유지()
    {
        var result = 관찰세계진행Policy.접속변경(Create(), "player:one", Start.AddHours(25), true);
        Assert.True(result.Next.Connected);
        Assert.All(result.ExecutionWindows, window => Assert.False(window.PlayerActivityAllowed));
        var online = 관찰세계진행Policy.정산(result.Next, "player:one", Start.AddHours(26));
        Assert.True(Assert.Single(online.ExecutionWindows).PlayerActivityAllowed);
        Assert.True(Assert.Single(online.ExecutionWindows).PersonalNpcWorkAllowed);
    }

    [Fact]
    public void 제어권_전환은_이미_지난_구간의_권한을_바꾸지_않음()
    {
        var change = 관찰세계진행Policy.제어변경(Create(true), "player:one", Start.AddHours(1), 관찰세계제어방식.직접);
        Assert.True(Assert.Single(change.ExecutionWindows).PlayerAutonomyAllowed);
        var direct = 관찰세계진행Policy.정산(change.Next, "player:one", Start.AddHours(2));
        Assert.True(Assert.Single(direct.ExecutionWindows).PlayerActivityAllowed);
        Assert.False(Assert.Single(direct.ExecutionWindows).PlayerAutonomyAllowed);
        change.Next.Connected = false;
        change.Next.SettledAtUtc = Start.AddYears(1);
        Assert.True(Assert.Single(change.ExecutionWindows).PlayerActivityAllowed);
        Assert.Equal(Start.AddHours(1), Assert.Single(change.ExecutionWindows).EndedAtUtc);
        var list = Assert.IsAssignableFrom<IList<관찰세계실행구간>>(change.ExecutionWindows);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    [Fact]
    public void 비활성_또는_동일시각_재시도는_실행구간_없음()
    {
        var disabled = Create(true);
        disabled.Enabled = false;
        Assert.Empty(관찰세계진행Policy.정산(disabled, "player:one", Start.AddHours(1)).ExecutionWindows);
        Assert.Empty(관찰세계진행Policy.정산(Create(true), "player:one", Start).ExecutionWindows);
        var settled = 관찰세계진행Policy.정산(Create(), "player:one", Start.AddHours(25));
        Assert.Empty(관찰세계진행Policy.정산(settled.Next, "player:one", Start.AddHours(25)).ExecutionWindows);
    }

    [Fact]
    public void 실행경계는_UTC로_정규화하고_최대날짜에서도_넘치지_않음()
    {
        var state = Create();
        state.SettledAtUtc = Start.ToOffset(TimeSpan.FromHours(9));
        state.DisconnectedAtUtc = state.SettledAtUtc;
        var result = 관찰세계진행Policy.정산(state, "player:one", Start.AddHours(25).ToOffset(TimeSpan.FromHours(-5)));
        Assert.All(result.ExecutionWindows, window =>
        {
            Assert.Equal(TimeSpan.Zero, window.StartedAtUtc.Offset);
            Assert.Equal(TimeSpan.Zero, window.EndedAtUtc.Offset);
        });
        state.SettledAtUtc = DateTimeOffset.MaxValue.AddHours(-1);
        state.DisconnectedAtUtc = state.SettledAtUtc;
        var edge = 관찰세계진행Policy.정산(state, "player:one", DateTimeOffset.MaxValue);
        Assert.Equal(TimeSpan.FromHours(1), Assert.Single(edge.ExecutionWindows).Elapsed);
    }
}
