using System;
using System.Linq;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private bool NeighborhoodLifeEnabled => 가상동네생활기준.Matches(ScenarioStableId, ScenarioDataRevision);
        private 가상동네생활Snapshot Life => waitingFleet!.NeighborhoodLife!;

        private 가상동네생활Snapshot CreateNeighborhoodLife()
        {
            var actors = new List<가상생활NpcSnapshot> {
                LifeActor(LocalActorId(가상동네생활기준.RestaurantActorId),"RestaurantOperator","restaurant-work"),
                LifeActor(LocalActorId(가상동네생활기준.MartWorkerId),"WarehouseWorker","mart-work"),
                LifeActor(LocalActorId(가상동네생활기준.DepotWorkerId),"WarehouseWorker","depot-work"),
                LifeActor(LocalActorId(가상동네생활기준.TruckDriverId),"FreightDriver","depot-bay"),
                LifeActor(ResidentId("a"),"Resident","home-a-resident"),
                LifeActor(ResidentId("b"),"Resident","home-b-resident") };
            for(int i=0;i<CourierIds.Length;i++) actors.Add(LifeActor(CourierIds[i],"Courier",WaitingSlots[i].x,WaitingSlots[i].z));
            var life = new 가상동네생활Snapshot { Actors = actors.ToArray(), DayEnabled = neighborhoodDayEnabled };
            life.Depot.Truck.ActorStableId = LocalActorId(가상동네생활기준.TruckDriverId);
            if (life.DayEnabled) 하루주거배정(life);
            return life;
        }
        private static 가상생활NpcSnapshot LifeActor(string id,string role,double x,double z)
            => new 가상생활NpcSnapshot {ActorId=id,Role=role,X=x,Z=z,WorkX=x,WorkZ=z};
        private static 가상생활NpcSnapshot LifeActor(string id,string role,string anchor)
        { var p = 가상동네배치기준.기준점(anchor); return LifeActor(id,role,p.x,p.z); }

        private bool LifeCanAccept(string actorId) => !NeighborhoodLifeEnabled
            || Life.Actors.Any(x=>x.ActorId==actorId && x.Stage=="Working" && CurrentTick<x.StopAcceptingTick
                    && (!Life.DayEnabled || !하루생활대상(x) || CurrentTick < 1050))
                && npcActors.TryGetValue(actorId,out var actor) && npcCapabilityGrants.Values.Any(x=>x.Active && x.ActorStableId==actorId
                    && x.OrganizationStableId==actor.OrganizationStableId && x.FacilityStableId==actor.HomeFacilityStableId
                    && x.CapabilityCode==(actor.ReferenceRoleCode=="RestaurantOperator"?SimulationNpcCapabilityCodes.RestaurantCooking:
                        actor.ReferenceRoleCode=="WarehouseWorker"?SimulationNpcCapabilityCodes.WarehouseOutboundPreparation:SimulationNpcCapabilityCodes.FreightTransport));

        private bool LifeBusy(가상생활NpcSnapshot actor)
        {
            if(actor.Role=="RestaurantOperator") return foodDeliveries.Values.Any(x=>
                !string.IsNullOrEmpty(x.RestaurantResponseDecisionStableId) && x.StateCode!=음식배달상태코드.거절 && !x.PickedUpTick.HasValue);
            if(actor.ActorId==LocalActorId(가상동네생활기준.MartWorkerId)) return waitingFleet!.Mart!.WorkerStage!="Idle";
            if(actor.ActorId==LocalActorId(가상동네생활기준.DepotWorkerId)) return Life.Depot.WorkerStage!="Idle";
            if(actor.Role=="FreightDriver") return Life.Depot.Truck.Stage!="Idle";
            var driver=waitingFleet!.Drivers.FirstOrDefault(x=>x.Courier.ActorStableId==actor.ActorId);
            return driver!=null && (driver.Courier.Stage!="Idle" || driver.PendingOrderId.Length>0);
        }

        [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,"인계 후 귀환·휴식·복귀와 신규 배정 제한",Boundary="기존 수락한 작업은 계속 처리한다")]
        private void AdvanceNeighborhoodLife()
        {
            if(!NeighborhoodLifeEnabled) return;
            waitingFleet ??= CreateWaitingFleet();
            foreach(var actor in Life.Actors.Where(x=>x.Role!="Resident"))
            {
                var previousStage = actor.Stage;
                var wasAway=actor.Stage!="Working" && actor.Stage!="Finishing";
                var driver=waitingFleet.Drivers.FirstOrDefault(x=>x.Courier.ActorStableId==actor.ActorId);
                if(driver!=null) { actor.X=driver.Courier.X;actor.Z=driver.Courier.Z;actor.WorkX=WaitingSlots[driver.Slot].x;actor.WorkZ=WaitingSlots[driver.Slot].z;actor.TaskId=driver.Courier.OrderStableId; }
                if(actor.ActorId==LocalActorId(가상동네생활기준.MartWorkerId)) {actor.X=waitingFleet.Mart!.X;actor.Z=waitingFleet.Mart.Z;actor.TaskId=waitingFleet.Mart.WorkerOrderId;}
                if(actor.ActorId==LocalActorId(가상동네생활기준.DepotWorkerId)) {actor.X=Life.Depot.X;actor.Z=Life.Depot.Z;actor.TaskId=Life.Depot.ShipmentId;}
                if(actor.Role=="FreightDriver") {actor.X=Life.Depot.Truck.X;actor.Z=Life.Depot.Truck.Z;actor.TaskId=Life.Depot.Truck.OrderStableId;}
                var daily = 하루퇴근진행(actor);
                if(!daily) {
                if(actor.Stage=="Working" && CurrentTick>=actor.StopAcceptingTick) actor.Stage="Finishing";
                if(actor.Stage=="Finishing" && !LifeBusy(actor)) actor.Stage="ReturningToRest";
                if(actor.Stage=="ReturningToRest")
                {
                    if(MoveLifeActor(actor,actor.WorkX+1.5,actor.WorkZ))
                    {actor.Stage="Resting";actor.RestUntilTick=CurrentTick+Life.RestTicks;RecordLife(actor.ActorId,"LIFE-REST","Resting",actor.ActorId);}
                }
                else if(actor.Stage=="Resting" && CurrentTick>=actor.RestUntilTick) actor.Stage="ReturningToWork";
                if(actor.Stage=="ReturningToWork" && MoveLifeActor(actor,actor.WorkX,actor.WorkZ))
                {actor.Stage="Working";actor.StopAcceptingTick=CurrentTick+Life.WorkingTicks;actor.CompletedRestCount++;RecordLife(actor.ActorId,"LIFE-SHIFT","Working",actor.ActorId);}
                }
                if(wasAway || actor.Stage!="Working" && actor.Stage!="Finishing")
                {
                    if(driver!=null) {driver.Courier.X=actor.X;driver.Courier.Z=actor.Z;}
                    if(actor.ActorId==LocalActorId(가상동네생활기준.MartWorkerId)) {waitingFleet.Mart!.X=actor.X;waitingFleet.Mart.Z=actor.Z;}
                    if(actor.ActorId==LocalActorId(가상동네생활기준.DepotWorkerId)) {Life.Depot.X=actor.X;Life.Depot.Z=actor.Z;}
                    if(actor.Role=="FreightDriver") {Life.Depot.Truck.X=actor.X;Life.Depot.Truck.Z=actor.Z;}
                }
                if (!daily) actor.Reason=actor.Stage=="Resting"?"휴식 · "+actor.RestUntilTick+"초까지":actor.Stage=="Finishing"?"신규 배정 중단 · 수락 업무 인계 중":
                    actor.Stage=="ReturningToRest"?"휴식점으로 귀환":actor.Stage=="ReturningToWork"?"근무점으로 복귀":LifeBusy(actor)?"업무 수행":"업무 대기";
                if (Life.DayEnabled && 하루생활대상(actor)) {
                    if (!daily) actor.NextAction = "맡은 업무와 휴식 · 저녁에 신규 배정 마감";
                    하루변화기록(actor, previousStage);
                }
            }
            AdvanceLifeResidents();
        }
        private static bool MoveLifeActor(가상생활NpcSnapshot actor,double x,double z)
        { var d=Segment((actor.X,actor.Z),(x,z));var r=d<=1?1:1/d;actor.X+=(x-actor.X)*r;actor.Z+=(z-actor.Z)*r;return d<=1; }

        private void AdvanceLifeResidents()
        {
            foreach(var resident in Life.Actors.Where(x=>x.Role=="Resident"))
            {
                var previousStage = resident.Stage;
                var destination=ResidenceId(new[]{"a","b"}.Single(x=>ResidentId(x)==resident.ActorId));
                var order=DeliveryTargets().LastOrDefault(x=>x.Destination==destination && !x.Received);
                var approaching=order!=null && waitingFleet!.Drivers.Any(x=>x.Courier.OrderStableId==order.Id &&
                    new[]{"DriveHome","WalkHome","Deliver","Receive"}.Contains(x.Courier.Stage));
                resident.TaskId=order?.Id??"";resident.Reason=approaching?"출입구에서 수령 준비":order!=null?"주문 처리 대기":"다음 주문 차례 대기";
                if(MoveLifeActor(resident,resident.WorkX,approaching?24:resident.WorkZ)) resident.Stage=approaching?"Receiving":"AtHome";
                if (Life.DayEnabled) { 주민하루진행(resident, order != null, approaching); 하루변화기록(resident, previousStage); }
            }
        }
        private bool LifeRecipientReady(string destination) => !NeighborhoodLifeEnabled
            || Life.Actors.Any(x=>x.ActorId==ResidentId(ResidenceKey(destination)) && x.Stage=="Receiving");

        private void GenerateLifeOrders()
        {
            foreach(var person in waitingFleet!.OrderFlow!.Orderers)
            {
                if(CurrentTick<person.NextTick) continue;
                do {person.NextTick+=60;} while(person.NextTick<=CurrentTick);
                if(!Life.AutomationEnabled || DurationTicks-CurrentTick<180 || Life.DayEnabled && CurrentTick>=1050) continue;
                var destination=ResidenceId(person.Residence);
                if(DeliveryTargets().Any(x=>x.Destination==destination && !x.Received)) continue;
                person.Sequence++;
                if(person.Sequence%2==1) CreateWaitingOrders(person.Sequence,new[]{person.Residence});
                else waitingFleet.Mart!.Orders=waitingFleet.Mart.Orders.Concat(new[]{new 가상마트주문Snapshot {
                    OrderId="mart-order:life:"+person.Residence+":"+person.Sequence,DestinationId=destination,AcceptedTick=CurrentTick }}).ToArray();
            }
        }

        private void ApplyNeighborhoodPolicy(SimulationNpcPolicyChangeRequest request)
        {
            var supplied=request.ObserverWorkingTicks.HasValue||request.ObserverRestTicks.HasValue||request.ObserverRestockThreshold.HasValue;
            if(!NeighborhoodLifeEnabled) {if(supplied)throw new SimulationContractException("NeighborhoodLifeProfileRequired");return;}
            if(request.PolicyStableId!="policy:restaurant:cooking") throw new SimulationContractException("NeighborhoodLifePolicyScopeInvalid");
            waitingFleet ??= CreateWaitingFleet();
            Life.WorkingTicks=request.ObserverWorkingTicks??Life.WorkingTicks;Life.RestTicks=request.ObserverRestTicks??Life.RestTicks;
            Life.RestockThreshold=request.ObserverRestockThreshold??Life.RestockThreshold;Life.AutomationEnabled=request.AutomationEnabled;Life.Priority=request.Priority;
            // 이미 시작한 근무/휴식의 마감은 보존하고 변경값은 다음 주기에 사용한다.
        }

        private void RecordLife(string actor,string action,string outcome,string target)
        {
            var previous=syntheticCourier;syntheticCourier=new 가상배달기사Snapshot {ActorStableId=actor,OrderStableId=target};
            RecordSynthetic(action,outcome);syntheticCourier=previous;
        }

        // 경로는 명시된 합성 동네 통로만 허용한다. 좁은 양방향 통로를 원자적으로 획득한다.
        private static string[] LifeRouteResources((double x,double z)[] points)
        {
            var resources=new HashSet<string>(StringComparer.Ordinal);
            for(int i=1;i<points.Length;i++)
            {
                var a=points[i-1];var b=points[i];
                if(a==b)continue;
                foreach(var junction in new[]{(0d,0d),(10d,0d),(20d,0d),(20d,20d),(10d,20d),(30d,20d)})
                    if(Math.Abs(Segment(a,junction)+Segment(junction,b)-Segment(a,b))<.0001)
                        resources.Add("road:junction:"+LifeBay(junction));
                if(a.z==0 && b.z==0) {
                    if(Math.Min(a.x,b.x)<0) resources.Add("road:west");
                    if(Math.Max(a.x,b.x)>0) resources.Add("road:east");
                }
                else if(a.x==20 && b.x==20) resources.Add("road:trunk");
                else if(a.z==20 && b.z==20) resources.Add(Math.Min(a.x,b.x)<20?"road:home-west":"road:home-east");
                else if(a.x==b.x && (new[]{-26d,-14d,-10d,10d,30d}.Contains(a.x))
                    && (Math.Min(a.z,b.z)>=-3 && Math.Max(a.z,b.z)<=2.5 || Math.Min(a.z,b.z)>=20 && Math.Max(a.z,b.z)<=22.5))
                    resources.Add("road:bay-spur:"+a.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+(a.z>=20?"home":"pickup"));
                else {
                    var slot=Array.FindIndex(WaitingSlots,x=>x==a||x==b);
                    if(slot<0) throw new SimulationContractException("NeighborhoodRoadNotApproved");
                    resources.Add("road:parking:"+slot);
                }
            }
            return resources.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        private static string LifeBay((double x,double z) p) => "bay:"+p.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+p.z.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private static double LifePickupZ(bool mart)=>mart?-2.5:2.5;
        private static (double x,double z)[] LifePickupRoute(int slot,bool mart)=>ToPickup(slot,mart).Concat(new[]{(mart?-10d:10d,LifePickupZ(mart))}).ToArray();
        private static (double x,double z)[] LifeHomeRoute(double pickupX,double home,bool mart)=>new[]{(pickupX,LifePickupZ(mart)),(pickupX,0d),(20d,0d),(20d,20d),(home,20d),(home,22.5)};
        private bool LifeReserve(string actor,params string[] resources)
        {
            if(resources.All(x=>Life.ResourceOwners.TryGetValue(x,out var owner)&&owner==actor)) return true;
            // 정차점은 차를 세워 둔 상태다. 도로/출입구 점유 중에는 다른 이동 묶음을 추가하지 않는다.
            if(Life.ResourceOwners.Any(x=>x.Value==actor && !x.Key.StartsWith("bay:",StringComparison.Ordinal))) return false;
            if(!Life.WaitingSince.ContainsKey(actor)) Life.WaitingSince[actor]=CurrentTick;
            var occupied=Life.ResourceOwners.Where(x=>x.Value!=actor).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
            var result=이동자원점유Policy.판정(occupied,new[]{new 이동자원진입요청(actor,Life.WaitingSince[actor],resources)});
            if(!result.판정목록.Single().진입가능) return false;
            foreach(var resource in resources) Life.ResourceOwners[resource]=actor;
            Life.WaitingSince.Remove(actor);return true;
        }
        private void LifeRelease(string actor,string prefix)
        {foreach(var key in Life.ResourceOwners.Where(x=>x.Value==actor && x.Key.StartsWith(prefix,StringComparison.Ordinal)).Select(x=>x.Key).ToArray())Life.ResourceOwners.Remove(key);}

        private bool MoveLifeVehicle((double x,double z)[] points,double speed,string next)
        {
            var actor=syntheticCourier!.ActorStableId;
            var targetBay=LifeBay(points.Last());
            var required=LifeRouteResources(points).Concat(new[]{targetBay}).ToArray();
            if(!LifeReserve(actor,required)) return false;
            // 이동 시작 뒤 원래 정차점은 해제한다. 목적 정차점은 도보 접근 동안 유지한다.
            foreach(var key in Life.ResourceOwners.Where(x=>x.Value==actor && x.Key.StartsWith("bay:",StringComparison.Ordinal) && x.Key!=targetBay).Select(x=>x.Key).ToArray())Life.ResourceOwners.Remove(key);
            MoveSynthetic(points,speed,true,next);
            if(syntheticCourier.Stage==next) LifeRelease(actor,"road:");
            return true;
        }
    }
}
