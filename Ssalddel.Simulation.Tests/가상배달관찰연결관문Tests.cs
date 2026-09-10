using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation;
using 조리표본 = Ssalddel.Unity.Warehouse.음식점관찰표본;
using 경로표본 = Ssalddel.Unity.Presentation.음식배달관찰표본;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "기존 조리 전용 Core와 표현 경로를 새 배송 완료 근거로 오인하지 않도록 검사한다.",
    Boundary = "기존 계약의 회귀 시험이며 신규 배차·이동·Scene·E 승격 증거가 아니다.")]
public sealed class 가상배달관찰연결관문Tests
{
    [Theory]
    [InlineData(8, "A")]
    [InlineData(20, "B")]
    public async Task 조리전용표본은_시간과표현경로완주만으로_배송되지않는다(int ticks, string residence)
    {
        var path = Path.Combine(Path.GetTempPath(), "synthetic-delivery-boundary-tests", Guid.NewGuid().ToString("N"));
        using var runtime = new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(path));
        var session = await runtime.Sessions.CreateAsync(조리표본.Create(Guid.NewGuid()));
        var id = session.SessionStableId;
        session = await runtime.ConfirmFoodDeliveryAsync(id,
            조리표본.주문("boundary-order-command", session.Revision, "food-order:boundary"));
        for (var tick = 0; tick < ticks; tick++)
            session = await runtime.Sessions.AdvanceWorldTickAsync(id, new 경영SimulationTick진행Request
            { CommandId = "boundary-tick:" + tick, ExpectedRevision = session.Revision, TickCount = 1 });

        var before = Assert.Single(session.FoodDeliveries);
        Assert.Equal("픽업대기", before.StateCode);
        Assert.NotNull(before.ReadyForPickupTick);
        Assert.Null(before.DispatchCandidateTick);
        Assert.Null(before.PickedUpTick);
        Assert.Null(before.DeliveredTick);
        Assert.Null(before.ReceivedTick);

        // 이 계산은 기사 이동이 아니다. 최종 표현 좌표를 구해도 권위 상태가 바뀌면 안 된다.
        var route = 경로표본.주택(residence);
        var endpoint = route.인계접근위치(1);
        Assert.Equal(route.EntranceRoute.End.X, endpoint.X);
        Assert.Equal(route.EntranceRoute.End.Z, endpoint.Z);
        var after = await runtime.Sessions.GetAsync(id);
        Assert.Equal(session.Revision, after.Revision);
        var order = Assert.Single(after.FoodDeliveries);
        Assert.Equal("픽업대기", order.StateCode);
        Assert.False(new 음식배달관찰상태(order).ReceiptConfirmed);
        Assert.Null(order.PickedUpTick);
        Assert.Null(order.DeliveredTick);
        Assert.Null(order.ReceivedTick);
    }
}
