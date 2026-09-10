using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "30분 직업 생활·입출고·통행·저장 재생",Boundary="자동 Core 시험, 실제 Game View 아님")]
public sealed class 가상동네생활Tests
{
    [Fact]
    public void 삼백Tick_개별업무입력없이_두주민의_음식수령과_다음주문이_반복된다()
    {
        var core = Create();
        for (var i = 0; i < 300; i++) Tick(core, i);
        var state = core.Snapshot();
        foreach (var id in new[] { "participant:synthetic:a", "participant:synthetic:b" })
        {
            var received = state.FoodDeliveries.Where(x => x.OrdererStableId == id && x.ReceivedTick.HasValue)
                .OrderBy(x => x.AcceptedTick).ToArray();
            Assert.True(received.Length >= 2, id + " 음식 수령 " + received.Length);
            Assert.True(received[1].AcceptedTick > received[0].ReceivedTick);
            foreach (var food in received)
            {
                var entry = state.WaitingFleet!.OrderFlow!.Entries.Single(x => x.OrderId == food.FoodOrderStableId);
                Assert.Equal("Accepted", entry.ResponseCode);
                Assert.True(entry.QueuedTick > entry.ResponseTick);
                Assert.NotEmpty(entry.DriverId);
                Assert.True(food.ReadyForPickupTick <= food.PickedUpTick);
                Assert.True(food.PickedUpTick <= food.ReceivedTick);
            }
        }
        var before = Json(state);
        var view = Ssalddel.Unity.Observation.동네관찰Presenter.생성(state);
        Assert.Equal(2, view.FoodCycles.Length);
        Assert.Equal(before, Json(state));
        Assert.False(state.IsCompleted);
    }
    [Fact]
    public void 선택생활필드가없던_과거파일은복원하고_원문변조는거절한다()
    {
        var folder=Path.Combine(Path.GetTempPath(),"ssalddel-life-compat-"+Guid.NewGuid().ToString("N"));
        try
        {
            var core=new 경영SimulationSessionAggregate(가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()));
            Tick(core,0);
            var store=new Ssalddel.Simulation.Infrastructure.FileSimulationLocalSaveSlotStore(folder);
            store.Write("legacy-r4",Save(core));
            var path=Path.Combine(folder,"legacy-r4.ssalddel");
            var envelope=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            var package=envelope["Package"]!;
            RemoveOptionalFields(package);
            var raw=package.ToJsonString();
            envelope["PackageSha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            File.WriteAllText(path,envelope.ToJsonString());
            Assert.Equal(Save(core).ReplayHash,store.Read("legacy-r4").Package.ReplayHash);
            envelope["PackageSha256"]="invalid";
            File.WriteAllText(path,envelope.ToJsonString());
            Assert.Throws<SimulationContractException>(()=>store.Read("legacy-r4"));
        }
        finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }

    private static void RemoveOptionalFields(System.Text.Json.Nodes.JsonNode node)
    {
        if(node is System.Text.Json.Nodes.JsonObject obj)
            foreach(var item in obj.ToArray())
            {
                if(new[]{"NeighborhoodLife","ObserverWorkingTicks","ObserverRestTicks","ObserverRestockThreshold"}.Contains(item.Key))obj.Remove(item.Key);
                else if(item.Value!=null)RemoveOptionalFields(item.Value);
            }
        else if(node is System.Text.Json.Nodes.JsonArray array)
            foreach(var item in array)if(item!=null)RemoveOptionalFields(item);
    }

    [Fact]
    public void 삼십분표본은_재고를보존하고_배달보충휴식후_정확히종료한다()
    {
        var core=Create();var prior=core.Snapshot();bool concurrent=false,blocked=false,resting=false;
        for(int i=0;i<1800;i++)
        {
            var state=Tick(core,i);var fleet=state.WaitingFleet!;var life=fleet.NeighborhoodLife!;var depot=life.Depot;var mart=fleet.Mart!;
            for(int sku=0;sku<2;sku++) {
                Assert.Equal(42,depot.Incoming[sku]+depot.Stock[sku]+depot.Shipments.Sum(x=>x.Cargo[sku])+mart.Stock[sku]+mart.Orders.Count(x=>x.PickedUnits>sku));
                Assert.InRange(mart.Reserved[sku],0,mart.Stock[sku]);Assert.InRange(depot.Reserved[sku],0,depot.Stock[sku]);
            }
            Assert.All(depot.Shipments,x=>Assert.InRange(x.Cargo.Sum(),0,depot.TruckCapacityUnits));
            Assert.All(state.FoodDeliveries.Where(x=>x.PickedUpTick.HasValue),x=>Assert.True(x.ReadyForPickupTick<=x.PickedUpTick));
            Assert.All(mart.Orders.Where(x=>x.PickedUpTick.HasValue),x=>Assert.True(x.ReadyTick<=x.PickedUpTick));
            foreach(var p in fleet.OrderFlow!.Orderers) {
                var destination="facility:synthetic:"+p.Residence;
                Assert.InRange(state.FoodDeliveries.Count(x=>x.DestinationFacilityStableId==destination && !x.ReceivedTick.HasValue && x.StateCode!=음식배달상태코드.거절)
                    +mart.Orders.Count(x=>x.DestinationId==destination && !x.ReceivedTick.HasValue),0,1);
            }
            var before=prior.WaitingFleet!.Drivers;
            concurrent|=fleet.Drivers.Count(x=>before.Any(y=>y.Courier.ActorStableId==x.Courier.ActorStableId &&
                (y.Courier.VehicleX!=x.Courier.VehicleX || y.Courier.VehicleZ!=x.Courier.VehicleZ)))>=2;
            blocked|=fleet.Drivers.Any(x=>x.WaitReason.Contains("대기"));resting|=life.Actors.Any(x=>x.Stage=="Resting");
            foreach(var driver in fleet.Drivers.Where(x=>x.AssignedTick==state.CurrentTick))
                Assert.Equal("Working",life.Actors.Single(x=>x.ActorId==driver.Courier.ActorStableId).Stage);
            prior=state;
        }
        Assert.True(prior.IsCompleted);Assert.Equal(1800,prior.CurrentTick);Assert.True(concurrent,"동시 통행 없음");Assert.True(blocked,"대기 없음");Assert.True(resting,"휴식 없음");
        Assert.Contains(prior.WaitingFleet!.NeighborhoodLife!.Depot.Shipments,x=>x.ReceivedTick.HasValue);
        Assert.True(prior.WaitingFleet.Mart!.Orders.Count(x=>x.ReceivedTick.HasValue)>2);
        Assert.All(prior.WaitingFleet.NeighborhoodLife.Actors.Where(x=>x.Role!="Resident"),x=>Assert.True(x.CompletedRestCount>0,x.ActorId+":"+x.Stage+":"+x.Reason));
        Assert.Throws<SimulationConflictException>(()=>Tick(core,1800));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(110)]
    [InlineData(480)]
    public void 작업과휴식중저장_정책과해시를재생한다(int saveAt)
    {
        var core=Create();
        core.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest {CommandId="policy:life-test",ExpectedRevision=core.Snapshot().Revision,
            PolicyStableId="policy:restaurant:cooking",AutomationEnabled=true,Priority=120,ObserverWorkingTicks=300,ObserverRestTicks=90,ObserverRestockThreshold=3,CookingDurationTicks=5});
        for(int i=0;i<saveAt;i++)Tick(core,i);
        var package=Save(core);var restored=SimulationSessionReplay.Restore(package);
        Assert.Equal(Json(core.Snapshot()),Json(restored.Snapshot()));
        for(int i=saveAt;i<saveAt+30;i++)Assert.Equal(Json(Tick(core,i)),Json(Tick(restored,i)));
        Assert.Equal(Save(core).ReplayHash,Save(restored).ReplayHash);
        Assert.Equal(300,restored.Snapshot().WaitingFleet!.NeighborhoodLife!.WorkingTicks);
    }

    [Fact]
    public void 기존프로필의기간과저장은유지하고_허용되지않은정책은거절한다()
    {
        var old=가상배달관찰표본.CreateOrderFlow(Guid.NewGuid());old.DurationTicks=1800;
        Assert.Throws<SimulationContractException>(()=>new 경영SimulationSessionAggregate(old));
        var request=가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid());request.DurationTicks=1801;
        Assert.Throws<SimulationContractException>(()=>new 경영SimulationSessionAggregate(request));
        var core=Create();var before=Json(core.Snapshot());
        Assert.Throws<SimulationContractException>(()=>core.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest {
            CommandId="bad:policy",ExpectedRevision=core.Snapshot().Revision,PolicyStableId="policy:restaurant:cooking",ObserverRestTicks=0}));
        Assert.Equal(before,Json(core.Snapshot()));
    }

    [Fact]
    public void 자동업무중단은신규주문을막고_정책명령은멱등이다()
    {
        var core=Create();var command=new SimulationNpcPolicyChangeRequest {CommandId="policy:stop",ExpectedRevision=core.Snapshot().Revision,
            PolicyStableId="policy:restaurant:cooking",AutomationEnabled=false};
        core.UpdateNpcPolicy(command);var before=Json(core.Snapshot());core.UpdateNpcPolicy(command);Assert.Equal(before,Json(core.Snapshot()));
        for(int i=0;i<65;i++)Tick(core,i);
        Assert.Empty(core.Snapshot().FoodDeliveries);Assert.Empty(core.Snapshot().WaitingFleet!.Mart!.Orders);
    }

    [Fact]
    public void 신규업무중단후에도_수락한조리와배송은완료된다()
    {
        var core=Create();
        for(int i=0;i<12;i++)Tick(core,i);
        Assert.Contains(core.Snapshot().FoodDeliveries,x=>!string.IsNullOrEmpty(x.RestaurantResponseDecisionStableId));
        core.UpdateNpcPolicy(new SimulationNpcPolicyChangeRequest {CommandId="policy:drain",ExpectedRevision=core.Snapshot().Revision,
            PolicyStableId="policy:restaurant:cooking",AutomationEnabled=false});
        var count=core.Snapshot().FoodDeliveries.Length;
        for(int i=12;i<200;i++)Tick(core,i);
        var state=core.Snapshot();
        Assert.Equal(count,state.FoodDeliveries.Length);
        Assert.All(state.FoodDeliveries,x=>Assert.True(x.ReceivedTick.HasValue));
    }

    [Fact]
    public void 조회사본수정은권위상태에영향을주지않는다()
    {
        var core=Create();Tick(core,0);var before=Json(core.Snapshot());var copy=core.Snapshot().WaitingFleet!.NeighborhoodLife!;
        copy.Depot.Stock[0]=1000;copy.Actors[0].Stage="Broken";copy.ResourceOwners["fake"]="fake";
        Assert.Equal(before,Json(core.Snapshot()));
    }
    private static 경영SimulationSessionAggregate Create()=>new(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid()));
    private static 경영SimulationSessionSnapshot Tick(경영SimulationSessionAggregate core,int i)=>core.Advance(new 경영SimulationTick진행Request {
        CommandId="life:tick:"+i,ExpectedRevision=core.Snapshot().Revision,TickCount=1});
    private static SimulationSessionSavePackage Save(경영SimulationSessionAggregate core)=>core.CreateSavePackage(new SimulationSessionSaveRequest {
        SaveStableId="save:life-test",ExpectedRevision=core.Snapshot().Revision});
    private static string Json(object value)=>System.Text.Json.JsonSerializer.Serialize(value);
}
