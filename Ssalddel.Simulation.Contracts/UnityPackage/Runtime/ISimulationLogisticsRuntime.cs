using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Simulation 물류 Preview·Confirm 실행 경계를 제공한다.",
        Boundary = "독립 영역 준비 전 연결 경로를 암묵적으로 열지 않는다.")]
    public interface ISimulationLogisticsRuntime
    {
        ValueTask<SimulationLogisticsMovementPreviewSnapshot>
            PreviewLogisticsMovementAsync(
            string sessionStableId,
            SimulationLogisticsMovementPreviewRequest request,
            CancellationToken cancellationToken = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmLogisticsMovementAsync(
            string sessionStableId,
            SimulationLogisticsMovementConfirmRequest request,
            CancellationToken cancellationToken = default);

        ValueTask<SimulationFreightDispatchPreviewSnapshot>
            PreviewFreightDispatchAsync(
            string sessionStableId,
            SimulationFreightDispatchPreviewRequest request,
            CancellationToken cancellationToken = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFreightDispatchAsync(
            string sessionStableId,
            SimulationFreightDispatchConfirmRequest request,
            CancellationToken cancellationToken = default);
    }
}
