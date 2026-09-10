using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "마트·도로변 대기 기사 사본과 배달/복귀·재생을 검증한다.", Boundary = "Unity 화면이나 다중 차량 동시 통행 증거가 아니다.")]
public sealed class 마트도로변대기Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(35)]
    [InlineData(80)]
    public void 세기사_대기_배달_가까운자리복귀를재생한다(int saveTick)
    {
        var core = new 경영SimulationSessionAggregate(가상배달관찰표본.CreateWaiting(Guid.NewGuid()));
        var state = core.Snapshot();
        for(int i=0;i<saveTick;i++) state = Advance(core,state,i);
        var restored = SimulationSessionReplay.Restore(core.CreateSavePackage(new SimulationSessionSaveRequest {
            SaveStableId="save:fleet", ExpectedRevision=state.Revision }));
        Assert.Equal(Json(state),Json(restored.Snapshot()));
        for(int i=saveTick;i<365;i++) {
            var before=state.WaitingFleet!.Drivers.Select(x=>x.Courier.Copy()).ToArray();
            state=Advance(core,state,i);
            Assert.Equal(Json(state),Json(Advance(restored,restored.Snapshot(),i)));
            Assert.Equal(3,state.WaitingFleet!.Drivers.Length);
            Assert.Null(state.SyntheticCourier);
            Assert.InRange(state.WaitingFleet.Drivers.Count(x=>x.Courier.Stage!="Idle" && x.Courier.Stage!="WaitingRoad"),0,1);
            for(int n=0;n<3;n++) {
                var driver=state.WaitingFleet.Drivers[n].Courier;
                Assert.InRange(Math.Sqrt(Math.Pow(driver.X-before[n].X,2)+Math.Pow(driver.Z-before[n].Z,2)),0,5.00001);
            }
        }
        Assert.True(state.FoodDeliveries.Length>=2);
        Assert.All(state.FoodDeliveries,x=>Assert.NotNull(x.ReceivedTick));
        Assert.All(state.WaitingFleet!.Drivers,x=>Assert.Equal("Idle",x.Courier.Stage));
        Assert.Contains(state.WaitingFleet.Drivers,x=>x.Slot>=3);
        Assert.Equal(3,state.WaitingFleet.Drivers.Select(x=>x.Slot).Distinct().Count());
        state.WaitingFleet.Drivers[0].Courier.X=-999;
        Assert.NotEqual(-999,core.Snapshot().WaitingFleet!.Drivers[0].Courier.X);
        var request=new SimulationSessionSaveRequest {SaveStableId="save:final",ExpectedRevision=state.Revision};
        Assert.Equal(core.CreateSavePackage(request).ReplayHash,restored.CreateSavePackage(request).ReplayHash);
    }
    private static 경영SimulationSessionSnapshot Advance(경영SimulationSessionAggregate core,경영SimulationSessionSnapshot state,int tick)
        => core.Advance(new 경영SimulationTick진행Request {CommandId="tick:"+tick,ExpectedRevision=state.Revision,TickCount=1});
    private static string Json(object value)=>System.Text.Json.JsonSerializer.Serialize(value);
}
