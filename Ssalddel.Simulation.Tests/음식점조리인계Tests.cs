using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "두 목적지 주문의 조리 완료와 저장 재진입을 배달 연결 전에 검증한다.",
    Boundary = "조리 전용 기존 Runtime 회귀이며 기사 배정·이동·Unity 화면 증거가 아니다.")]
public sealed class 음식점조리인계Tests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task 두목적지조리는_중간저장재진입후에도_동일하고_배송을자동확정하지않는다(int saveTick)
    {
        var path = Path.Combine(Path.GetTempPath(), "restaurant-handoff-tests", Guid.NewGuid().ToString("N"));
        using var original = Create(path);
        var state = await original.Sessions.CreateAsync(음식점관찰표본.Create(Guid.NewGuid()));
        var id = state.SessionStableId;
        foreach (var destination in new[] { "a", "b" })
        {
            var request = 음식점관찰표본.주문("order:" + destination, state.Revision, "food-order:" + destination);
            request.FoodDelivery.DestinationFacilityStableId = "facility:residence:" + destination;
            request.FoodDelivery.OrdererStableId = "participant:" + destination;
            request.FoodDelivery.ActorStableId = request.FoodDelivery.OrdererStableId;
            state = await original.ConfirmFoodDeliveryAsync(id, request);
        }
        for (var tick = 0; tick < saveTick; tick++)
            state = await original.Sessions.AdvanceWorldTickAsync(id, new 경영SimulationTick진행Request
            { CommandId = "before:" + tick, ExpectedRevision = state.Revision, TickCount = 1 });

        var saved = await original.Sessions.SaveSlotAsync(id, new SimulationLocalSaveSlotRequest
        { SlotStableId = "cooking-handoff", ExpectedRevision = state.Revision });
        using var restored = Create(path);
        var loaded = await restored.Sessions.LoadSlotAsync("cooking-handoff");
        var replay = loaded.Restore.Session;
        Assert.Equal(saved.SavedWorldTick, replay.CurrentTick);
        Assert.Equal(state.Revision, replay.Revision);

        for (var tick = saveTick; tick < 8; tick++)
        {
            state = await original.Sessions.AdvanceWorldTickAsync(id, new 경영SimulationTick진행Request
            { CommandId = "after:" + tick, ExpectedRevision = state.Revision, TickCount = 1 });
            replay = await restored.Sessions.AdvanceWorldTickAsync(replay.SessionStableId, new 경영SimulationTick진행Request
            { CommandId = "after:" + tick, ExpectedRevision = replay.Revision, TickCount = 1 });
            Assert.Equal(state.Revision, replay.Revision);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(state.FoodDeliveries),
                System.Text.Json.JsonSerializer.Serialize(replay.FoodDeliveries));
        }
        Assert.Equal(2, state.FoodDeliveries.Length);
        Assert.Equal(2, state.FoodDeliveries.Select(x => x.DestinationFacilityStableId).Distinct().Count());
        Assert.All(state.FoodDeliveries, order =>
        {
            Assert.Equal("픽업대기", order.StateCode);
            Assert.NotNull(order.ReadyForPickupTick);
            Assert.Null(order.DispatchCandidateTick);
            Assert.Null(order.PickedUpTick);
            Assert.Null(order.DeliveredTick);
            Assert.Null(order.ReceivedTick);
        });
    }

    private static LocalSimulationRuntime Create(string path) => new(
        new InMemory경영SimulationSessionStore(), new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(path));
}
