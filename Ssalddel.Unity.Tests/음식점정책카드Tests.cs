using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;
using Xunit;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "음식점 카드의 정책 미설정·오류 차단·충돌 후 재조회를 검증한다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀,
    Boundary = ".NET 연결 시험이며 실제 Unity UI·Play Mode·Game View 검증이 아니다.")]
public sealed class 음식점정책카드Tests
{
    [Fact]
    public async Task 표시행은_원본수정과분리되고_다른음식점을포함하지않는다()
    {
        var runtime = new Runtime();
        runtime.state.FoodDeliveries = new[] {
            new Simulation음식배달Snapshot { FoodOrderStableId = "food-order:a", RestaurantFacilityStableId = "facility:test",
                StateCode = "주문대기", TaskStableId = "task:a" },
            new Simulation음식배달Snapshot { FoodOrderStableId = "food-order:b", RestaurantFacilityStableId = "facility:other" } };
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        var row = Assert.Single(card.주문목록);
        Assert.Equal("접수 대기", row.표시상태);
        runtime.state.FoodDeliveries[0].StateCode = "픽업대기";
        Assert.Equal("접수 대기", row.표시상태);
        Assert.Equal("픽업 준비", Assert.Single(card.주문목록).표시상태);
    }

    [Fact]
    public async Task 설정카드는_입력판본을전달하고_권위재조회로갱신한다()
    {
        var runtime = new Runtime();
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        Assert.Equal(1, card.조리자리수);
        await card.적용Async("command:settings", card.판본, 2, 3, true);
        Assert.Equal(1, runtime.Request!.ExpectedRevision);
        Assert.Equal(2, runtime.Reads);
        Assert.Equal(2, card.조리자리수);
        Assert.Equal(3, card.조리시간);
    }

    private sealed class Runtime : ISimulationNpcPolicyRuntime
    {
        public SimulationNpcPolicyChangeRequest? Request;
        public int Reads;
        public int Updates;
        public Exception? ReadError;
        public Exception? UpdateError;
        public readonly 경영SimulationSessionSnapshot state = new()
        {
            Revision = 1,
            NpcWorkPolicies = new[] { new SimulationNpcWorkPolicySnapshot {
                PolicyStableId = "policy:test", FacilityStableId = "facility:test",
                ActionCode = SimulationNpcActionCodes.RestaurantCooking, CookingSlots = 1, WorkDurationTicks = 2 } },
        };
        public ValueTask<경영SimulationSessionSnapshot> GetPolicySessionAsync(string id, CancellationToken token = default)
        { Reads++; if (ReadError != null) throw ReadError; return new(state); }
        public ValueTask<경영SimulationSessionSnapshot> UpdateNpcPolicyAsync(string id, SimulationNpcPolicyChangeRequest request, CancellationToken token = default)
        {
            Request = request;
            Updates++;
            if (UpdateError != null) throw UpdateError;
            state.NpcWorkPolicies[0].CookingSlots = request.CookingSlots;
            state.NpcWorkPolicies[0].WorkDurationTicks = request.CookingDurationTicks!.Value;
            state.NpcWorkPolicies[0].AutomationEnabled = request.AutomationEnabled;
            state.Revision++;
            return new(new 경영SimulationSessionSnapshot());
        }
    }

    [Fact]
    public async Task 미설정자리는_설정된한자리로표시하지않으며_명시적으로설정할수있다()
    {
        var runtime = new Runtime();
        runtime.state.NpcWorkPolicies[0].CookingSlots = null;
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        Assert.True(card.편집가능);
        Assert.Null(card.조리자리수);
        Assert.Equal("미설정·조리 대기", card.설정상태);
        Assert.Null(runtime.Request);
        await card.적용Async("command:set", card.판본, 음식점정책카드Presenter.새설정기본자리수, 2, true);
        Assert.Equal(1, card.조리자리수);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 정책누락과중복은_편집을차단하며_재조회로회복한다(bool duplicate)
    {
        var runtime = new Runtime();
        var policy = runtime.state.NpcWorkPolicies[0];
        runtime.state.NpcWorkPolicies = duplicate ? new[] { policy, policy } : Array.Empty<SimulationNpcWorkPolicySnapshot>();
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        Assert.True(card.조회됨);
        Assert.False(card.편집가능);
        Assert.Contains(duplicate ? "중복" : "없음", card.안내);
        await Assert.ThrowsAsync<InvalidOperationException>(() => card.적용Async("command:no", 1, 1, 2, true));
        Assert.Null(runtime.Request);
        runtime.state.NpcWorkPolicies = new[] { policy };
        await card.조회Async();
        Assert.True(card.편집가능);
    }

    [Fact]
    public async Task 재조회실패는_낡은설정과주문을표시하지않고_편집을차단한다()
    {
        var runtime = new Runtime();
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        runtime.ReadError = new InvalidOperationException("read failed");
        Assert.Same(runtime.ReadError, await Assert.ThrowsAsync<InvalidOperationException>(() => card.조회Async()));
        Assert.False(card.조회됨);
        Assert.False(card.편집가능);
        Assert.Null(card.조리자리수);
        Assert.Empty(card.주문상태);
        await Assert.ThrowsAsync<InvalidOperationException>(() => card.적용Async("command:no", 1, 1, 2, true));
        Assert.Null(runtime.Request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 적용충돌은_자동재시도하지않고_재조회하며_원래오류를보존한다(bool refreshFails)
    {
        var runtime = new Runtime();
        var card = new 음식점정책카드Presenter(runtime, "session:test", "facility:test");
        await card.조회Async();
        runtime.state.Revision = 2;
        runtime.state.NpcWorkPolicies[0].CookingSlots = 3;
        runtime.UpdateError = new InvalidOperationException("revision conflict");
        if (refreshFails) runtime.ReadError = new InvalidOperationException("refresh failed");
        Assert.Same(runtime.UpdateError, await Assert.ThrowsAsync<InvalidOperationException>(
            () => card.적용Async("command:stale", 1, 2, 3, true)));
        Assert.Equal(1, runtime.Request!.ExpectedRevision);
        Assert.Equal(2, runtime.Reads);
        Assert.Equal(1, runtime.Updates);
        Assert.Equal(!refreshFails, card.편집가능);
        if (!refreshFails)
        {
            Assert.Equal(2, card.판본);
            Assert.Equal(3, card.조리자리수);
            Assert.Contains("설정 적용 실패", card.안내);
        }
    }
}
