using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>정책 설정 화면이 사용하는 권위 조회·변경 포트. 새 업무 규칙이 아니다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "NPC 업무 정책의 권위 조회·개정 확인 변경 포트를 제공한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행,
        Boundary = "정책 화면은 상태 권위를 소유하지 않으며 포트만으로 원격 연결을 증명하지 않는다.")]
    public interface ISimulationNpcPolicyRuntime
    {
        ValueTask<경영SimulationSessionSnapshot> GetPolicySessionAsync(
            string sessionStableId, CancellationToken cancellationToken = default);
        ValueTask<경영SimulationSessionSnapshot> UpdateNpcPolicyAsync(
            string sessionStableId, SimulationNpcPolicyChangeRequest request,
            CancellationToken cancellationToken = default);
    }
}
