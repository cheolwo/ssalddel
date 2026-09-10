using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>
    /// Hosted Simulation의 음식 주문 포트를 HTTP로 호출하는 전송 Adapter다.
    /// 업무 규칙이나 상태를 재구현하지 않으며 인증과 HttpClient 수명은 조립자가 소유한다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "RemoteHost의 음식 주문·음식점 응답·배달 수령 포트를 기존 Simulation API로 전달한다.",
        Boundary = "업무 규칙·인증·상태 권위를 재구현하지 않는 HTTP Adapter다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
    public sealed class RemoteSimulationFoodOrderRuntime : ISimulationFoodOrderRuntime
    {
        private readonly RemoteSimulationHttpJsonClient transport;

        public RemoteSimulationFoodOrderRuntime(
            HttpClient client,
            JsonSerializerOptions? jsonOptions = null)
        {
            transport = new RemoteSimulationHttpJsonClient(client, jsonOptions);
        }

        public ValueTask<경영SimulationSessionSnapshot> ConfirmRestaurantResponseAsync(
            string sessionStableId,
            Simulation음식점응답Request request,
            CancellationToken token = default)
            => transport.PostAsync<Simulation음식점응답Request, 경영SimulationSessionSnapshot>(
                sessionStableId, "restaurant-responses/confirm", request, token);

        public ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(
            string sessionStableId,
            Simulation음식배달PreviewRequest request,
            CancellationToken token = default)
            => transport.PostAsync<Simulation음식배달PreviewRequest, Simulation음식배달PreviewSnapshot>(
                sessionStableId, "food-delivery-previews", request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(
            string sessionStableId,
            Simulation음식배달ConfirmRequest request,
            CancellationToken token = default)
            => transport.PostAsync<Simulation음식배달ConfirmRequest, 경영SimulationSessionSnapshot>(
                sessionStableId, "food-deliveries/confirm", request, token);

        public ValueTask<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceiptAsync(
            string sessionStableId,
            Simulation음식배달수령PreviewRequest request,
            CancellationToken token = default)
            => transport.PostAsync<Simulation음식배달수령PreviewRequest, SimulationDecisionPreviewSnapshot>(
                sessionStableId, "food-delivery-receipt-previews", request, token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceiptAsync(
            string sessionStableId,
            Simulation음식배달수령ConfirmRequest request,
            CancellationToken token = default)
            => transport.PostAsync<Simulation음식배달수령ConfirmRequest, 경영SimulationSessionSnapshot>(
                sessionStableId, "food-delivery-receipts/confirm", request, token);
    }
}
