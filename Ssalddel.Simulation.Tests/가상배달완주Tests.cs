using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "공통 Core의 합성 배달 반복·재생·순차 인계를 검증한다.", Boundary = "실제 Unity Game View 증거가 아니다.")]
public sealed class 가상배달완주Tests
{
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(23)]
    [InlineData(40)]
    [InlineData(90)]
    public void 중간저장후_배송반복이_같다(int tick)
    {
        var core = new 경영SimulationSessionAggregate(가상배달관찰표본.Create(Guid.NewGuid()));
        var state = core.Snapshot();
        for (var i = 0; i < tick; i++) state = Advance(core, state, i);
        var saved = core.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:synthetic", ExpectedRevision = state.Revision });
        var restored = SimulationSessionReplay.Restore(saved);
        var replay = restored.Snapshot();
        Assert.Equal(Json(state), Json(replay));
        for (var i = tick; i < 200; i++)
        {
            state = Advance(core, state, i); replay = Advance(restored, replay, i);
            Assert.Equal(Json(state), Json(replay));
        }
        Assert.True(state.SyntheticCourier!.Batch >= 2);
        Assert.True(state.FoodDeliveries.Count(x => x.ReceivedTick.HasValue) >= 4);
        var finalSave = new SimulationSessionSaveRequest { SaveStableId = "save:final", ExpectedRevision = state.Revision };
        Assert.Equal(core.CreateSavePackage(finalSave).ReplayHash, restored.CreateSavePackage(finalSave).ReplayHash);
        Assert.All(state.FoodDeliveries.Where(x => x.ReceivedTick.HasValue), order =>
        {
            Assert.True(order.ReadyForPickupTick < order.PickedUpTick);
            Assert.True(order.PickedUpTick < order.DeliveredTick);
            Assert.Equal(order.DeliveredTick + 1, order.ReceivedTick);
        });
    }

    [Fact]
    public void 종료까지_모든주문을수령하고복귀하며_중복Tick은재실행하지않는다()
    {
        var core = new 경영SimulationSessionAggregate(가상배달관찰표본.Create(Guid.NewGuid()));
        var state = core.Snapshot();
        for (var tick = 0; tick < state.DurationTicks; tick++)
        {
            var request = new 경영SimulationTick진행Request { CommandId = "tick:" + tick, ExpectedRevision = state.Revision, TickCount = 1 };
            var before = state.SyntheticCourier?.Copy();
            state = core.Advance(request);
            Assert.Equal(Json(state), Json(core.Advance(request)));
            var courier = state.SyntheticCourier!;
            if (before != null)
            {
                var delta = Math.Sqrt(Math.Pow(courier.X - before.X, 2) + Math.Pow(courier.Z - before.Z, 2));
                Assert.InRange(delta, 0, 5);
            }
            Assert.True(courier.Z <= 24 && courier.Z >= 0);
            // 외부에 반환한 사본을 수정해도 Core의 위치가 바뀌지 않는다.
            courier.X = -999;
            state = core.Snapshot();
            Assert.NotEqual(-999, state.SyntheticCourier!.X);
        }
        Assert.True(state.IsCompleted);
        Assert.Equal("Idle", state.SyntheticCourier!.Stage);
        Assert.All(state.FoodDeliveries, x => Assert.NotNull(x.ReceivedTick));
    }

    private static 경영SimulationSessionSnapshot Advance(경영SimulationSessionAggregate core, 경영SimulationSessionSnapshot state, int index)
        => core.Advance(new 경영SimulationTick진행Request { CommandId = "tick:" + index, ExpectedRevision = state.Revision, TickCount = 1 });
    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}
