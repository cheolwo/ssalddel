using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Observation;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "동네 관찰 명령 직렬화·실패·초안 시험", Boundary = "Fake Port 단위 시험은 Unity 실제 입력과 구분한다")]
public sealed class 동네관찰Tests
{
    [Theory]
    [InlineData(false, true, 300, "다음 주문 검토")]
    [InlineData(false, false, 300, "신규 주문 중단")]
    [InlineData(false, true, 1700, "마감")]
    [InlineData(true, true, 1800, "표본 종료")]
    public void 자율반복안내는_완료와정책을읽고_명령이나상태를바꾸지않는다(bool completed, bool automation, int tick, string expected)
    {
        var state = 표본(); state.CurrentTick = tick; state.DurationTicks = 1800; state.IsCompleted = completed;
        state.WaitingFleet!.NeighborhoodLife!.AutomationEnabled = automation;
        var before = System.Text.Json.JsonSerializer.Serialize(state);
        var model = 동네관찰Presenter.생성(state);
        Assert.Equal(2, model.FoodCycles.Length);
        Assert.All(model.FoodCycles, row => Assert.Contains(expected, row.Body));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(state));
    }
    private sealed class Port : I동네관찰SessionPort
    {
        public 경영SimulationSessionSnapshot State = 표본();
        public int Ticks, Saves, Policies, Restarts, Reads;
        public bool FailSave, FailRead;
        public TaskCompletionSource? Hold;
        public long Expected;
        public Task<경영SimulationSessionSnapshot> 조회Async() { Reads++; if (FailRead) throw new InvalidOperationException(); return Task.FromResult(State); }
        public async Task 진행Async(long revision) { Expected = revision; Ticks++; if (Hold != null) await Hold.Task; State.CurrentTick++; State.Revision++; }
        public Task 정책Async(SimulationNpcPolicyChangeRequest request) { Policies++; Expected = request.ExpectedRevision; State.Revision++; return Task.CompletedTask; }
        public Task 저장Async(long revision) { Saves++; if (FailSave) throw new IOException("not for UI"); return Task.CompletedTask; }
        public Task 재시작Async() { Restarts++; State = 표본(); return Task.CompletedTask; }
    }
    private static 경영SimulationSessionSnapshot 표본() => new() {
        CurrentTick = 30, DurationTicks = 1800, Revision = 42, SessionStableId = "session:test",
        WaitingFleet = new() { NeighborhoodLife = new() { Actors = new[] {
            new 가상생활NpcSnapshot { ActorId = "actor:synthetic-courier:1", Role = "Courier", Stage = "Working" } } },
            Mart = new(), OrderFlow = new(), Drivers = new[] { new 가상배달대기기사Snapshot {
                Courier = new() { ActorStableId = "actor:synthetic-courier:1", Stage = "Idle" } } } },
        NpcWorkPolicies = new[] { new SimulationNpcWorkPolicySnapshot { PolicyStableId = "policy:restaurant:cooking", WorkDurationTicks = 2 } } };

    [Fact]
    public async Task 재생_정책_저장은_진행상태를_보존한다()
    {
        var port = new Port(); using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); Assert.True(await controller.시간진행Async(1));
        Assert.True(await controller.정책적용Async(new())); Assert.True(controller.Playing);
        Assert.Equal(43, port.Expected); Assert.True(await controller.저장Async()); Assert.True(controller.Playing);
        Assert.Equal(3, controller.SaveCount); Assert.Equal(3, port.Reads);
    }
    [Fact]
    public async Task 동시에_입력하면_명령을_겹치거나_밀린시간을_따라잡지_않는다()
    {
        var port = new Port { Hold = new() }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); var tick = controller.시간진행Async(1); Assert.True(controller.Busy);
        var policy = controller.정책적용Async(new()); Assert.False(policy.IsCompleted); Assert.True(controller.CommandPending);
        Assert.False(await controller.저장Async());
        Assert.False(await controller.시간진행Async(60)); Assert.Equal(0, port.Policies);
        port.Hold.SetResult(); await tick; Assert.True(await policy); Assert.False(controller.Busy); Assert.False(controller.CommandPending);
        Assert.Equal(43, port.Expected); Assert.Equal(1, port.Policies);
        Assert.False(await controller.시간진행Async(.1)); Assert.Equal(1, port.Ticks);
    }
    [Fact]
    public async Task 긴저장뒤에도_밀린Tick은_한주기까지만_인정한다()
    {
        var port = new Port { Hold = new() }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); var tick = controller.시간진행Async(1); await controller.시간진행Async(60);
        port.Hold.SetResult(); await tick;
        Assert.True(await controller.시간진행Async(.1)); Assert.Equal(2, port.Ticks);
        Assert.False(await controller.시간진행Async(.1)); Assert.Equal(2, port.Ticks);
    }
    [Fact]
    public async Task 저장중에도_즉시_정지요청할수있다()
    {
        var port = new Port { Hold = new() }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); var tick = controller.시간진행Async(1); Assert.True(controller.Busy);
        controller.재생전환(); Assert.False(controller.Playing);
        port.Hold.SetResult(); await tick; Assert.False(controller.Playing); Assert.Equal(1, port.Saves);
    }
    [Fact]
    public async Task 선행저장실패는_대기하던정책도_차단한다()
    {
        var port = new Port { Hold = new(), FailSave = true }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); var tick = controller.시간진행Async(1); var policy = controller.정책적용Async(new());
        port.Hold.SetResult(); Assert.False(await tick); Assert.False(await policy);
        Assert.True(controller.SaveBlocked); Assert.False(controller.CommandPending); Assert.Equal(0, port.Policies);
    }
    [Fact]
    public async Task 짧은_저장대기는_1초_관찰주기안에_포함한다()
    {
        var port = new Port { Hold = new() }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); var first = controller.시간진행Async(1);
        await controller.시간진행Async(.4); port.Hold.SetResult(); await first;
        Assert.False(await controller.시간진행Async(.5)); Assert.True(await controller.시간진행Async(.1)); Assert.Equal(2, port.Ticks);
    }
    [Fact]
    public async Task 저장실패_정지_명시적재시도_재생은_별도이다()
    {
        var port = new Port { FailSave = true }; using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); Assert.False(await controller.시간진행Async(1));
        Assert.True(controller.SaveBlocked); Assert.False(controller.Playing); Assert.Equal(31, controller.State.CurrentTick);
        Assert.DoesNotContain("not for UI", controller.Message);
        controller.재생전환(); Assert.False(controller.Playing); Assert.False(await controller.한초진행Async());
        port.FailSave = false; Assert.True(await controller.저장Async()); Assert.False(controller.SaveBlocked); Assert.False(controller.Playing);
        controller.재생전환(); Assert.True(controller.Playing); Assert.Equal(1, port.Ticks);
    }
    [Fact]
    public async Task 조회실패도_정지하고_저장재시도에서_재조회한다()
    {
        var port = new Port { FailRead = true }; using var controller = new 동네관찰SessionController(port, port.State);
        Assert.False(await controller.한초진행Async()); Assert.Equal(0, port.Saves);
        port.FailRead = false; Assert.True(await controller.저장Async()); Assert.Equal(2, port.Reads);
    }
    [Fact]
    public async Task 완료된_표본만_확인후_재시작한다()
    {
        var port = new Port(); using var controller = new 동네관찰SessionController(port, port.State);
        Assert.False(await controller.재시작Async(true)); port.State.IsCompleted = true;
        Assert.False(await controller.재시작Async(false)); Assert.False(await controller.한초진행Async());
        Assert.True(await controller.재시작Async(true)); Assert.Equal(1, port.Restarts); Assert.Equal(1, port.Saves); Assert.False(controller.Playing);
    }
    [Fact]
    public async Task 정지_재개에_밀린시간을_더하지_않는다()
    {
        var port = new Port(); using var controller = new 동네관찰SessionController(port, port.State);
        controller.재생전환(); await controller.시간진행Async(.8); controller.정지(); await controller.시간진행Async(120);
        controller.재생전환(); Assert.False(await controller.시간진행Async(.3)); Assert.Equal(0, port.Ticks);
        Assert.True(await controller.시간진행Async(120)); Assert.Equal(1, port.Ticks);
    }
    [Fact]
    public async Task 제거중_완료된_명령은_View에_콜백하지_않는다()
    {
        var port = new Port { Hold = new() }; var controller = new 동네관찰SessionController(port, port.State);
        int notifications = 0; controller.Changed += () => notifications++;
        var step = controller.한초진행Async(); controller.Dispose(); int before = notifications;
        port.Hold.SetResult(); await step; Assert.Equal(before, notifications); Assert.False(await controller.저장Async());
    }
    [Fact]
    public void 정책초안은_조회변화와_독립이고_취소에서만_덮어쓴다()
    {
        var state = 표본(); var draft = new 동네정책Draft(); draft.동기화(state);
        draft.변경(cooking: 5, threshold: 3); draft.동기화(state); Assert.True(draft.Dirty); Assert.Equal(5, draft.Cooking);
        Assert.Equal(2, state.NpcWorkPolicies[0].WorkDurationTicks); Assert.Equal(3, draft.요청().ObserverRestockThreshold);
        draft.동기화(state, true); Assert.False(draft.Dirty); Assert.Equal(2, draft.Cooking);
    }
    [Fact]
    public void 대기시간은_원본시각이_있을때만_표시한다()
    {
        var state = 표본(); var model = 동네관찰Presenter.생성(state);
        Assert.DoesNotContain("대기 30초", model.Actors[0].Body);
        state.WaitingFleet!.NeighborhoodLife!.WaitingSince["actor:synthetic-courier:1"] = 23;
        model = 동네관찰Presenter.생성(state); Assert.Contains("통행 대기 7초", model.Actors[0].Body);
    }
    [Fact]
    public void 음식주문은_기존원장의_담당기사와_연결된다()
    {
        var state = 표본(); state.FoodDeliveries = new[] { new Simulation음식배달Snapshot {
            FoodOrderStableId = "food:1", StateCode = "조리완료", ReadyForPickupTick = 20, OrdererStableId = "participant:synthetic:a" } };
        state.WaitingFleet!.OrderFlow!.Entries = new[] { new 가상주문연결Snapshot { OrderId = "food:1", DriverId = "actor:synthetic-courier:1" } };
        state.WaitingFleet.Drivers[0].Courier.OrderStableId = "food:1";
        var model = 동네관찰Presenter.생성(state);
        Assert.Contains("주민 A", model.Orders[0].Title); Assert.Contains("배달 기사 1", model.Orders[0].Body);
        Assert.Contains("픽업 대기 10초", model.Orders[0].Body); Assert.Equal("food:1", model.Actors[0].RelatedId);
        Assert.Equal("restaurant", model.Orders[0].FacilityId); Assert.Equal(42, state.Revision);
    }
    [Theory]
    [InlineData("DriveHome", "주문지로 운전")]
    [InlineData("Pickup", "물품 픽업")]
    [InlineData("Deliver", "주문자에게 전달")]
    [InlineData("Receive", "수령 확인 대기")]
    [InlineData("Unknown", "상태 확인 필요")]
    [InlineData("수령확인", "수령확인")]
    public void 업무상태는_한국어로_보인다(string code, string expected) => Assert.Equal(expected, 동네관찰Presenter.단계(code));
}
