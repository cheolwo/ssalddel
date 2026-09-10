using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "로컬 주문 원장·수락 후 배차·독립 주기·저장 재생", Boundary = "실제 Unity 화면/운영 서버 증거 아님")]
public sealed class 가상주문흐름Tests
{
    [Fact]
    public void 기사가먼저도착하면_조리완료까지기다린다()
    {
        var request=가상배달관찰표본.CreateOrderFlow(Guid.NewGuid());
        request.NpcWorkforce!.Policies[0].WorkDurationTicks=14;
        var core=new 경영SimulationSessionAggregate(request);
        var waited=false;
        for(int i=0;i<80;i++) {
            var state=Tick(core,i);
            waited |= state.WaitingFleet!.Drivers.Any(x=>x.WaitReason=="조리 완료 대기");
            foreach(var order in state.FoodDeliveries.Where(x=>x.PickedUpTick.HasValue))
                Assert.True(order.ReadyForPickupTick<=order.PickedUpTick);
        }
        Assert.True(waited);
    }

    [Fact]
    public void 가용기사부재는_대기후재탐색한다()
    {
        var core=Create(); var waiting=new HashSet<string>();
        for(int i=0;i<365;i++) {
            var state=Tick(core,i);
            foreach(var entry in state.WaitingFleet!.OrderFlow!.Entries.Where(x=>x.WaitReason.Contains("가용 기사 없음")))
                waiting.Add(entry.OrderId);
        }
        Assert.NotEmpty(waiting);
        Assert.All(core.Snapshot().WaitingFleet!.OrderFlow!.Entries.Where(x=>waiting.Contains(x.OrderId)),x=>Assert.Equal("Completed",x.State));
    }

    [Fact]
    public async Task 저장실패는_숨기지않고_같은상태를재시도한다()
    {
        var store=new 저장실패Store();
        using var runtime=new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),new InMemorySimulationSessionSaveStore(),store);
        var state=await runtime.Sessions.CreateAsync(가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()));
        state=await runtime.Sessions.AdvanceWorldTickAsync(state.SessionStableId,new 경영SimulationTick진행Request {
            CommandId="tick:save-failure",ExpectedRevision=state.Revision,TickCount=2 });
        var request=new SimulationLocalSaveSlotRequest {SlotStableId=가상배달관찰표본.OrderFlowSlotId,ExpectedRevision=state.Revision};
        await Assert.ThrowsAsync<IOException>(async()=>await runtime.Sessions.SaveSlotAsync(state.SessionStableId,request));
        Assert.Null(store.Package);
        Assert.Equal(Json(state),Json(await runtime.Sessions.GetAsync(state.SessionStableId)));
        store.Fail=false;
        await runtime.Sessions.SaveSlotAsync(state.SessionStableId,request);
        var restored=SimulationSessionReplay.Restore(store.Package!);
        Assert.Equal(Json(state),Json(restored.Snapshot()));
    }

    private sealed class 저장실패Store : ISimulationLocalSaveSlotStore
    {
        public bool Fail=true;
        public SimulationSessionSavePackage? Package;
        public void Write(string slot,SimulationSessionSavePackage package)
        { if(Fail) throw new IOException("TestSaveUnavailable"); Package=package; }
        public SimulationLocalSaveSlotPackage Read(string slot)=>new() {SlotStableId=slot,Package=Package!};
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(31)]
    [InlineData(90)]
    public void 수락후에만배차하고_독립주문과저장재생을유지한다(int saveTick)
    {
        var core = Create();
        for (int i=0;i<saveTick;i++) Tick(core,i);
        var restored = SimulationSessionReplay.Restore(core.CreateSavePackage(new SimulationSessionSaveRequest {
            SaveStableId="save:order-flow",ExpectedRevision=core.Snapshot().Revision }));
        Assert.Equal(Json(core.Snapshot()),Json(restored.Snapshot()));
        for(int i=saveTick;i<365;i++) {
            var request=new 경영SimulationTick진행Request {CommandId="tick:"+i,ExpectedRevision=core.Snapshot().Revision,TickCount=1};
            var state=core.Advance(request);
            Assert.Equal(Json(state),Json(Tick(restored,i)));
            var before=Json(core.Snapshot()); core.Advance(request); Assert.Equal(before,Json(core.Snapshot()));
            var flow=state.WaitingFleet!.OrderFlow!;
            Assert.Equal(flow.Entries.Length,flow.Entries.Select(x=>x.OrderId).Distinct().Count());
            foreach(var entry in flow.Entries.Where(x=>x.SourceKind=="Restaurant")) {
                if(entry.QueuedTick.HasValue) {
                    Assert.Equal("Accepted",entry.ResponseCode);
                    Assert.True(entry.QueuedTick>entry.ResponseTick);
                }
                if(entry.DriverId.Length>0) Assert.NotNull(entry.QueuedTick);
                if(!entry.QueuedTick.HasValue)
                    Assert.DoesNotContain(state.WaitingFleet.Drivers,x=>x.Courier.OrderStableId==entry.OrderId || x.PendingOrderId==entry.OrderId);
            }
            foreach(var person in flow.Orderers)
                Assert.InRange(state.FoodDeliveries.Count(x=>x.OrdererStableId=="participant:synthetic:"+person.Residence
                    && !x.ReceivedTick.HasValue && x.StateCode!=음식배달상태코드.거절),0,1);
        }
        var final=core.Snapshot();
        Assert.True(final.FoodDeliveries.Length>2);
        Assert.All(final.FoodDeliveries,x=>Assert.NotNull(x.ReceivedTick));
        Assert.Contains(final.WaitingFleet!.Mart!.Orders,x=>x.WaitReason.Contains("재고 부족"));
        Assert.Contains(final.WaitingFleet.OrderFlow!.Entries,x=>x.SourceKind=="Restaurant" && x.SubmittedTick>=121);
        var save=new SimulationSessionSaveRequest {SaveStableId="save:final",ExpectedRevision=final.Revision};
        Assert.Equal(core.CreateSavePackage(save).ReplayHash,restored.CreateSavePackage(save).ReplayHash);
    }

    [Fact]
    public void 음식점담당자부재는_수락과배차없이_원장에남는다()
    {
        var request=가상배달관찰표본.CreateOrderFlow(Guid.NewGuid());
        request.NpcWorkforce!.CapabilityGrants=Array.Empty<SimulationNpcCapabilityGrantInitialRequest>();
        var core=new 경영SimulationSessionAggregate(request);
        for(int i=0;i<10;i++) Tick(core,i);
        Assert.All(core.Snapshot().WaitingFleet!.OrderFlow!.Entries.Where(x=>x.SourceKind=="Restaurant"),x=> {
            Assert.Equal("Pending",x.ResponseCode); Assert.Null(x.QueuedTick); Assert.Equal("Inbox",x.State);
        });
    }

    [Fact]
    public void 거절한주문은_배차큐에들어가지않는다()
    {
        var core=Create();
        var request=음식점관찰표본.주문("command:manual",core.Snapshot().Revision,"food-order:manual-reject");
        request.FoodDelivery.NpcAutoAccept=false;
        var state=core.ConfirmFoodDelivery(request);
        var order=state.FoodDeliveries.Single();
        var response=new Simulation음식점응답Request {CommandId="command:reject",ExpectedRevision=state.Revision,
            FoodOrderStableId=order.FoodOrderStableId,FoodOrderRevision=order.Revision,
            ActorStableId=음식점관찰표본.ActorId,Accept=false,RejectionReasonCode="reason:test"};
        core.ConfirmRestaurantResponse(response);
        for(int i=0;i<10;i++) Tick(core,i);
        var entry=core.Snapshot().WaitingFleet!.OrderFlow!.Entries.Single(x=>x.OrderId==order.FoodOrderStableId);
        Assert.Equal("Rejected",entry.ResponseCode); Assert.Null(entry.QueuedTick); Assert.Empty(entry.DriverId);
    }
    private static 경영SimulationSessionAggregate Create() => new(가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()));
    private static 경영SimulationSessionSnapshot Tick(경영SimulationSessionAggregate core,int i)
        =>core.Advance(new 경영SimulationTick진행Request {CommandId="tick:"+i,ExpectedRevision=core.Snapshot().Revision,TickCount=1});
    private static string Json(object value)=>System.Text.Json.JsonSerializer.Serialize(value);
}
