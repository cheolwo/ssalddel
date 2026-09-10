using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "모의 음식 주문과 음식점 응답의 미리보기·확정 실행 경계를 제공한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행,
        Boundary = "운영 주문 API나 실제 배차·배송·Unity 화면 완료를 보장하지 않는다.")]
    public interface ISimulationFoodOrderRuntime
    {
        ValueTask<경영SimulationSessionSnapshot> ConfirmRestaurantResponseAsync(string sessionStableId,
            Simulation음식점응답Request request, CancellationToken token = default);
        ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(string sessionStableId,
            Simulation음식배달PreviewRequest request, CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(string sessionStableId,
            Simulation음식배달ConfirmRequest request, CancellationToken token = default);
        ValueTask<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceiptAsync(
            string sessionStableId,
            Simulation음식배달수령PreviewRequest request,
            CancellationToken token = default);
        ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceiptAsync(
            string sessionStableId,
            Simulation음식배달수령ConfirmRequest request,
            CancellationToken token = default);
    }
}
