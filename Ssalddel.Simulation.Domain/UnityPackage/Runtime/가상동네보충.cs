using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Domain
{
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodWorkflowLineage, SsalddelCodeLayer.Domain,
        "가상 보충창고 출고 예약에서 운영과 같은 재고 배분 계산 사용",
        StepKey = "domain.warehouse-outbound-shared-rule", FlowOrder = 55,
        ExecutionStage = SsalddelCodeExecutionStage.Tick,
        ReadsFrom = SsalddelCodeDataScope.SimulationState, WritesTo = SsalddelCodeDataScope.SimulationState,
        Effects = SsalddelCodeEffect.StateMutation,
        SourceCodeRefs = new[] { "Ssalddel/Services/LogisticsProcessing/Warehouse/OutboundBatchEngine.cs" },
        ReuseKind = "SharedRuleCall",
        SharedRuleRefs = new[] { "Ssalddel.WorkflowRules/UnityPackage/Runtime/창고출고배분Policy.cs" },
        Adaptation = "공통 배분 계산만 공유한다. 가상은 유한 합성 재고/고정 보충량/세션 예약을 사용하며 운영 DB 후보 조회·서비스 권역·출고 원장 저장을 수행하지 않는다.",
        Boundary = "가상 동네 보충 재고만 변경; 운영 창고·화물 생성 없음")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,"독립 보충창고 검수·적치·출고와 화물차 인계",Boundary="유한 합성 물품만 이동하며 운영 서버/다른 Area를 호출하지 않는다")]
        private void AdvanceNeighborhoodDepot()
        {
            var depot=Life.Depot;
            AdvanceLifeWarehouseWorker();
            AdvanceLifeFreight();
            AdvanceLifeMartInbound();
            if(!Life.AutomationEnabled || depot.WorkerStage!="Idle" || !LifeCanAccept(LocalActorId(가상동네생활기준.DepotWorkerId))
                || DurationTicks-CurrentTick<180 || depot.Shipments.Any(x=>!x.ReceivedTick.HasValue)) return;
            var mart=waitingFleet!.Mart!;
            if(mart.Stock.Zip(mart.Reserved,(s,r)=>s-r).All(x=>x>=Life.RestockThreshold)) return;
            // 기존 운영 OutboundBatchEngine과 같은 순수 배분 계산. 조회/예약/저장은 Session 소유다.
            var candidates=Enumerable.Range(0,2).Select(i=>new[]{i} as System.Collections.Generic.IReadOnlyList<int>).ToArray();
            var allocation=창고출고배분Policy.판정(new[]{4,4},candidates,i=>(long)i,i=>1L,
                i=>depot.Stock[i]-depot.Reserved[i],i=>100m,i=>true);
            if(Enumerable.Range(0,2).Any(i=>allocation.Where(x=>x.LineIndex==i).Sum(x=>x.Quantity)!=4))
            {depot.Reason="보충창고 재고 부족 · 유한 합성 입고 소진";return;}
            var shipment=new 가상보충운송Snapshot {Id="shipment:life:"+(depot.Shipments.Length+1),RequestedTick=CurrentTick};
            foreach(var item in allocation) depot.Reserved[item.Candidate]+=item.Quantity;
            depot.Shipments=depot.Shipments.Concat(new[]{shipment}).ToArray();
            depot.ShipmentId=shipment.Id;depot.Shelf=0;depot.WorkTicks=0;depot.WorkerStage="WalkShelf";
            depot.Reason="보충 물품 피킹";RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-RESERVE","Reserved",shipment.Id);
        }

        private void AdvanceLifeWarehouseWorker()
        {
            var d=Life.Depot;
            var shipment=d.Shipments.SingleOrDefault(x=>x.Id==d.ShipmentId);
            switch(d.WorkerStage)
            {
                case "UnloadInbound": if(++d.WorkTicks>=6){d.WorkerStage="InspectInbound";d.WorkTicks=0;}break;
                case "InspectInbound":
                    if(++d.WorkTicks>=4){d.WorkerStage="PutAwayInbound";d.WorkTicks=0;RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-INSPECT","Inspected",d.SourceId);}break;
                case "PutAwayInbound":
                    if(MoveDepotWorker(-26,-14))
                    {for(int i=0;i<2;i++){d.Stock[i]+=d.Incoming[i];d.Incoming[i]=0;}d.WorkerStage="Return";RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-PUTAWAY","Stored",d.SourceId);}break;
                case "Idle": break;
                case "WalkShelf":
                    if(MoveDepotWorker(d.Z>-14?-26:d.Shelf==0?-30:-26,-14) && d.X==(d.Shelf==0?-30:-26))
                    {d.WorkerStage="Pick";d.WorkTicks=0;}break;
                case "Pick":
                    if(++d.WorkTicks<2) break;
                    var quantity=shipment!.Quantity[d.Shelf];
                    if(d.Stock[d.Shelf]<quantity || d.Reserved[d.Shelf]<quantity) throw new SimulationConflictException("NeighborhoodReservedStockMissing");
                    d.Stock[d.Shelf]-=quantity;d.Reserved[d.Shelf]-=quantity;shipment.Cargo[d.Shelf]+=quantity;
                    RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-PICK","Picked",shipment.Id);
                    if(++d.Shelf<2)d.WorkerStage="WalkShelf";else {d.WorkerStage="WalkPack";shipment.Stage="Picked";}break;
                case "WalkPack": if(MoveDepotWorker(-22,-14)){d.WorkerStage="Pack";d.WorkTicks=0;}break;
                case "Pack":
                    if(++d.WorkTicks>=4){shipment!.Stage="Packed";d.WorkerStage="WalkHandoff";RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-PACK","Packed",shipment.Id);}break;
                case "WalkHandoff":
                    if(MoveDepotWorker(-26,d.X!=-26?-14:-8) && d.Z==-8)
                    {shipment!.Stage="Ready";shipment.ReadyTick=CurrentTick;d.WorkerStage="Return";RecordLife(LocalActorId(가상동네생활기준.DepotWorkerId),"DEPOT-STAGE","Ready",shipment.Id);}break;
                case "Return":
                    if(MoveDepotWorker(d.Z>-14?-26:-22,-14) && d.X==-22)
                    {d.WorkerStage="Idle";d.ShipmentId="";d.Reason="보충 요청 대기";}break;
                default: throw new SimulationContractException("NeighborhoodWarehouseStageInvalid");
            }
        }
        private bool MoveDepotWorker(double x,double z)
        {
            var d=Life.Depot;var length=Segment((d.X,d.Z),(x,z));var ratio=length<=1?1:1/length;
            d.X+=(x-d.X)*ratio;d.Z+=(z-d.Z)*ratio;return length<=1;
        }

        private void AdvanceLifeFreight()
        {
            var d=Life.Depot;var truck=d.Truck;
            if(truck.Stage=="Idle")
            {
                if(!LifeCanAccept(truck.ActorStableId))return;
                var ready=d.Shipments.FirstOrDefault(x=>x.Stage=="Ready");
                if(ready==null)return;
                if(ready.Cargo.Sum()>d.TruckCapacityUnits || ready.Cargo.Sum()<=0)
                {d.Reason="차량 적재 조건 불충족 · 배차 보류";return;}
                truck.OrderStableId=ready.Id;truck.Stage="WalkLoad";truck.Distance=0;
            }
            var shipment=d.Shipments.Single(x=>x.Id==truck.OrderStableId);
            var previous=syntheticCourier;syntheticCourier=truck;
            switch(truck.Stage)
            {
                case "WalkLoad":
                    if(LifeReserve(truck.ActorStableId,"entrance:depot"))MoveSynthetic(new[]{(-26d,-3d),(-26d,-8d)},1,false,"Loading");break;
                case "Loading":
                    if(++d.TruckWorkTicks>=4){d.TruckWorkTicks=0;truck.Carrying=true;shipment.Stage="Loaded";shipment.LoadedTick=CurrentTick;
                        RecordSynthetic("FREIGHT-LOAD","Loaded");SetSyntheticStage("WalkLoadOut");}break;
                case "WalkLoadOut":
                    MoveSynthetic(new[]{(-26d,-8d),(-26d,-3d)},1,false,"DriveMart");
                    if(truck.Stage=="DriveMart")LifeRelease(truck.ActorStableId,"entrance:");break;
                case "DriveMart":
                    d.Reason=MoveLifeVehicle(new[]{(-26d,-3d),(-26d,0d),(-14d,0d),(-14d,-3d)},4,"WalkUnload")?"마트 보충 운송":"화물차 도로/하역점 대기";break;
                case "WalkUnload":
                    if(LifeReserve(truck.ActorStableId,"entrance:mart-inbound"))MoveSynthetic(new[]{(-14d,-3d),(-14d,-8d)},1,false,"Unloading");break;
                case "Unloading":
                    if(++d.TruckWorkTicks>=4){d.TruckWorkTicks=0;truck.Carrying=false;shipment.Stage="Unloaded";shipment.UnloadedTick=CurrentTick;
                        RecordSynthetic("FREIGHT-UNLOAD","Unloaded");SetSyntheticStage("WalkUnloadOut");}break;
                case "WalkUnloadOut":
                    MoveSynthetic(new[]{(-14d,-8d),(-14d,-3d)},1,false,"Return");
                    if(truck.Stage=="Return")LifeRelease(truck.ActorStableId,"entrance:");break;
                case "Return": MoveLifeVehicle(new[]{(-14d,-3d),(-14d,0d),(-26d,0d),(-26d,-3d)},4,"Idle");break;
                default:throw new SimulationContractException("NeighborhoodFreightStageInvalid");
            }
            syntheticCourier=previous;
        }

        private void AdvanceLifeMartInbound()
        {
            var mart=waitingFleet!.Mart!;
            var shipment=Life.Depot.Shipments.FirstOrDefault(x=>x.UnloadedTick.HasValue && !x.ReceivedTick.HasValue);
            if(shipment==null)return;
            if(mart.WorkerStage=="Idle" && Life.Priority<100 && mart.Orders.Any(x=>x.Stage=="Pending")
                && Enumerable.Range(0,2).All(i=>mart.Stock[i]-mart.Reserved[i]>0))return;
            if(mart.WorkerStage=="Idle" && LifeCanAccept(LocalActorId(가상동네생활기준.MartWorkerId)))
            {mart.WorkerStage="LifeInboundWalk";mart.WorkerOrderId=shipment.Id;mart.WorkTicks=0;}
            switch(mart.WorkerStage)
            {
                case "LifeInboundWalk":
                    if(Life.ResourceOwners.ContainsKey("entrance:mart-inbound"))return;
                    if(MoveMartWorker(mart,-14,mart.X!=-14?-14:-8) && mart.Z==-8) mart.WorkerStage="LifeInboundInspect";break;
                case "LifeInboundInspect": if(++mart.WorkTicks>=4){mart.WorkerStage="LifeInboundStore";shipment.Stage="Inspected";}break;
                case "LifeInboundStore":
                    if(MoveMartWorker(mart,mart.Z>-14?-14:-8,-14) && mart.X==-8)
                    {
                        if(Enumerable.Range(0,2).Any(i=>mart.Stock[i]+shipment.Cargo[i]>8)) {Life.Depot.Reason="마트 적재 공간 부족 · 입고 대기";return;}
                        for(int i=0;i<2;i++){mart.Stock[i]+=shipment.Cargo[i];shipment.Cargo[i]=0;}
                        shipment.ReceivedTick=CurrentTick;shipment.Stage="Received";mart.WorkerStage="LifeInboundReturn";
                        RecordLife(LocalActorId(가상동네생활기준.MartWorkerId),"MART-INBOUND","Received",shipment.Id);
                    }break;
            }
        }
    }
}
