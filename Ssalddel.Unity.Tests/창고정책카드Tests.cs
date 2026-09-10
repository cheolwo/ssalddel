using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "창고 카드의 초안 소유·정책 변경·권위 재조회와 조회 실패 표시를 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀,
    Boundary = ".NET 연결 시험이며 실제 Unity 카드·Scene·Play Mode 검증이 아니다.")]
public sealed class 창고정책카드Tests
{
    [Fact]
    public async Task 적용응답대신_재조회결과로표시하고_다른화면초안은거부한다()
    {
        var port = new Port();
        var cards = new 창고정책카드Presenter(port, "session:one", "facility:hub");
        await cards.조회Async();
        var draft = cards.편집시작("policy:inspection");
        draft.Priority = 222;
        await cards.적용Async(draft, "command:one");
        Assert.Equal(2, port.Reads);
        Assert.Equal(222, cards.편집시작(draft.PolicyStableId).Priority);
        var other = new 창고정책카드Presenter(port, "session:other", "facility:hub");
        await other.조회Async();
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.적용Async(draft, "command:other"));
        Assert.Equal(1, port.Writes);
    }

    [Fact]
    public async Task 변경후재조회실패는_이전상태로성공을위장하지않는다()
    {
        var port = new Port();
        var cards = new 창고정책카드Presenter(port, "session:one", "facility:hub");
        await cards.조회Async();
        var draft = cards.편집시작("policy:inspection");
        port.FailRead = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => cards.적용Async(draft, "command:one"));
        Assert.False(cards.조회됨);
        Assert.Empty(cards.카드목록);
        Assert.Contains("조회하지 못", cards.마지막안내);
    }

    private sealed class Port : ISimulationNpcPolicyRuntime
    {
        public int Reads;
        public int Writes;
        public bool FailRead;
        private int priority = 100;
        public ValueTask<경영SimulationSessionSnapshot> GetPolicySessionAsync(string id, CancellationToken token = default)
        {
            Reads++;
            if (FailRead) throw new InvalidOperationException("read failed");
            return new ValueTask<경영SimulationSessionSnapshot>(new 경영SimulationSessionSnapshot
            {
                SessionStableId = id, Revision = Writes + 1,
                NpcWorkPolicies = new[] { new SimulationNpcWorkPolicySnapshot
                {
                    PolicyStableId = "policy:inspection", FacilityStableId = "facility:hub",
                    ActionCode = SimulationNpcActionCodes.WarehouseInboundInspection, Priority = priority,
                } },
            });
        }
        public ValueTask<경영SimulationSessionSnapshot> UpdateNpcPolicyAsync(string id,
            SimulationNpcPolicyChangeRequest request, CancellationToken token = default)
        {
            Writes++;
            priority = request.Priority;
            // 적용 응답을 그대로 그리는 구현이면 시험에서 실패하게 한다.
            return new ValueTask<경영SimulationSessionSnapshot>(new 경영SimulationSessionSnapshot());
        }
    }
}
