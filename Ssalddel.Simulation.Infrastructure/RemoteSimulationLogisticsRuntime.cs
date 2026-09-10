using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>
    /// Hosted Simulation의 화물 배차와 시설 간 물류 이동을 HTTP로 전달한다.
    /// 업무 규칙이나 상태를 재구현하지 않는다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "RemoteHost의 배차·배송·창고 물류 포트를 기존 Simulation API로 전달한다.",
        Boundary = "업무 규칙·상태 권위를 재구현하지 않는 HTTP Adapter다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
    public sealed class RemoteSimulationLogisticsRuntime : ISimulationLogisticsRuntime
    {
        private readonly RemoteSimulationHttpJsonClient transport;

        public RemoteSimulationLogisticsRuntime(
            HttpClient client,
            JsonSerializerOptions? jsonOptions = null)
            => transport = new RemoteSimulationHttpJsonClient(
                client ?? throw new ArgumentNullException(nameof(client)),
                jsonOptions);

        public ValueTask<SimulationLogisticsMovementPreviewSnapshot>
            PreviewLogisticsMovementAsync(
                string sessionStableId,
                SimulationLogisticsMovementPreviewRequest request,
                CancellationToken cancellationToken = default)
            => transport.PostAsync<SimulationLogisticsMovementPreviewRequest,
                SimulationLogisticsMovementPreviewSnapshot>(sessionStableId,
                "logistics-movement-previews", request, cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmLogisticsMovementAsync(
                string sessionStableId,
                SimulationLogisticsMovementConfirmRequest request,
                CancellationToken cancellationToken = default)
            => transport.PostAsync<SimulationLogisticsMovementConfirmRequest,
                경영SimulationSessionSnapshot>(sessionStableId,
                "logistics-movements/confirm", request, cancellationToken);

        public ValueTask<SimulationFreightDispatchPreviewSnapshot>
            PreviewFreightDispatchAsync(
                string sessionStableId,
                SimulationFreightDispatchPreviewRequest request,
                CancellationToken cancellationToken = default)
            => transport.PostAsync<SimulationFreightDispatchPreviewRequest,
                SimulationFreightDispatchPreviewSnapshot>(sessionStableId,
                "freight-dispatch-previews", request, cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFreightDispatchAsync(
                string sessionStableId,
                SimulationFreightDispatchConfirmRequest request,
                CancellationToken cancellationToken = default)
            => transport.PostAsync<SimulationFreightDispatchConfirmRequest,
                경영SimulationSessionSnapshot>(sessionStableId,
                "freight-dispatches/confirm", request, cancellationToken);
    }
}
