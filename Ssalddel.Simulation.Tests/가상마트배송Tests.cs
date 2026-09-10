using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "마트 주문·재고 보존·배달·저장 재생 회귀", Boundary = "실제 Unity 화면·Hosted 증거 아님")]
public sealed class 가상마트배송Tests
{
    [Fact]
    public void 같은Tick명령은_예약과인계를_중복하지않는다()
    {
        var core = new 경영SimulationSessionAggregate(가상배달관찰표본.CreateMart(Guid.NewGuid()));
        for (int i=0;i<200;i++) {
            var request = new 경영SimulationTick진행Request { CommandId="duplicate:"+i,
                ExpectedRevision=core.Snapshot().Revision, TickCount=1 };
            core.Advance(request);
            var before=Json(core.Snapshot());
            core.Advance(request);
            Assert.Equal(before,Json(core.Snapshot()));
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(45)]
    [InlineData(80)]
    public void 피킹포장배송과재고부족을_저장재생한다(int saveTick)
    {
        var core = new 경영SimulationSessionAggregate(가상배달관찰표본.CreateMart(Guid.NewGuid()));
        for (int i = 0; i < saveTick; i++) Tick(core, i);
        var restored = SimulationSessionReplay.Restore(core.CreateSavePackage(new SimulationSessionSaveRequest {
            SaveStableId = "save:mart", ExpectedRevision = core.Snapshot().Revision }));
        Assert.Equal(Json(core.Snapshot()), Json(restored.Snapshot()));
        var stages = new HashSet<string>();
        for (int i = saveTick; i < 365; i++)
        {
            var before = core.Snapshot();
            var state = Tick(core, i);
            Assert.Equal(Json(state), Json(Tick(restored, i)));
            var mart = state.WaitingFleet!.Mart!;
            stages.Add(mart.WorkerStage);
            Assert.InRange(Math.Sqrt(Math.Pow(mart.X - before.WaitingFleet!.Mart!.X, 2)
                + Math.Pow(mart.Z - before.WaitingFleet.Mart.Z, 2)), 0, 1.00001);
            Assert.All(mart.Stock, q => Assert.InRange(q, 0, 2));
            Assert.All(mart.Reserved, q => Assert.InRange(q, 0, 1));
            Assert.Equal(4, mart.Stock.Sum() + mart.Orders.Sum(x => x.PickedUnits));
            foreach (var order in mart.Orders)
            {
                if (!order.ReadyTick.HasValue)
                    Assert.DoesNotContain(state.WaitingFleet.Drivers,
                        x => x.Courier.OrderStableId == order.OrderId || x.PendingOrderId == order.OrderId);
                if (order.PickedUpTick.HasValue)
                { Assert.True(order.ReadyTick <= order.PickedUpTick); Assert.Equal(2, order.PickedUnits); }
                if (order.ReadyTick.HasValue) Assert.True(order.PackedTick < order.ReadyTick);
            }
            foreach (var driver in state.WaitingFleet.Drivers)
            {
                var old = before.WaitingFleet.Drivers.Single(x => x.Courier.ActorStableId == driver.Courier.ActorStableId).Courier;
                Assert.InRange(Math.Sqrt(Math.Pow(driver.Courier.X-old.X,2)+Math.Pow(driver.Courier.Z-old.Z,2)),0,5.00001);
            }
        }
        var final = core.Snapshot();
        Assert.Equal(2, final.WaitingFleet!.Mart!.Orders.Count(x => x.ReceivedTick.HasValue));
        Assert.Contains(final.WaitingFleet.Mart.Orders, x => x.WaitReason.Contains("재고 부족"));
        Assert.All(final.WaitingFleet.Mart.Reserved, q => Assert.Equal(0, q));
        Assert.All(final.FoodDeliveries, x => Assert.NotNull(x.ReceivedTick));
        Assert.All(final.WaitingFleet.Drivers, x => Assert.Equal("Idle", x.Courier.Stage));
        final.WaitingFleet.Mart.Stock[0] = 999;
        Assert.NotEqual(999, core.Snapshot().WaitingFleet!.Mart!.Stock[0]);
        if (saveTick == 0)
            foreach (var stage in new[] { "WalkShelf", "Pick", "WalkPack", "Pack", "WalkHandoff", "Return", "Idle" })
                Assert.Contains(stage, stages);
        var request = new SimulationSessionSaveRequest { SaveStableId = "save:final", ExpectedRevision = core.Snapshot().Revision };
        Assert.Equal(core.CreateSavePackage(request).ReplayHash, restored.CreateSavePackage(request).ReplayHash);
    }
    private static 경영SimulationSessionSnapshot Tick(경영SimulationSessionAggregate core, int tick)
        => core.Advance(new 경영SimulationTick진행Request { CommandId = "tick:" + tick,
            ExpectedRevision = core.Snapshot().Revision, TickCount = 1 });
    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}
