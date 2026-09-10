using System.Linq;
using System.Text;
using System.Globalization;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    internal static partial class SimulationReplayHasher
    {
        private static void AddNeighborhoodLife(StringBuilder target, 가상동네생활Snapshot? life)
        {
            if(life==null)return; // 기존 프로필의 해시에는 새 구분자도 추가하지 않는다.
            Add(target,가상동네생활기준.Revision);Add(target,life.WorkingTicks);Add(target,life.RestTicks);
            Add(target,life.RestockThreshold);Add(target,life.AutomationEnabled);Add(target,life.Priority);
            // 선택하지 않은 기존 저장의 해시 입력은 그대로 유지한다.
            if (life.DayEnabled) Add(target,"neighborhood-day.r1");
            foreach(var actor in life.Actors)
            {
                Add(target,actor.ActorId);Add(target,actor.Role);Add(target,actor.Stage);Add(target,actor.TaskId);Add(target,actor.Reason);
                Add(target,actor.StopAcceptingTick);Add(target,actor.RestUntilTick);Add(target,actor.CompletedRestCount);
                Add(target,actor.X.ToString("R",CultureInfo.InvariantCulture));Add(target,actor.Z.ToString("R",CultureInfo.InvariantCulture));
                Add(target,actor.WorkX.ToString("R",CultureInfo.InvariantCulture));Add(target,actor.WorkZ.ToString("R",CultureInfo.InvariantCulture));
                if (life.DayEnabled) {
                    Add(target,actor.HomeFacilityId); Add(target,actor.HomeX.ToString("R",CultureInfo.InvariantCulture));
                    Add(target,actor.HomeZ.ToString("R",CultureInfo.InvariantCulture)); Add(target,actor.HomeRouteStep);
                    Add(target,actor.EatingUntilTick); Add(target,actor.LastMealReceiptTick); Add(target,actor.NextAction);
                    foreach (var entry in actor.DayHistory) Add(target,entry);
                }
            }
            foreach(var owner in life.ResourceOwners.OrderBy(x=>x.Key,System.StringComparer.Ordinal)){Add(target,owner.Key);Add(target,owner.Value);}
            foreach(var wait in life.WaitingSince.OrderBy(x=>x.Key,System.StringComparer.Ordinal)){Add(target,wait.Key);Add(target,wait.Value);}
            var d=life.Depot;Add(target,d.SourceId);
            foreach(var q in d.Incoming)Add(target,q);foreach(var q in d.Stock)Add(target,q);foreach(var q in d.Reserved)Add(target,q);
            Add(target,d.WorkerStage);Add(target,d.ShipmentId);Add(target,d.Reason);Add(target,d.Shelf);Add(target,d.WorkTicks);
            Add(target,d.X.ToString("R",CultureInfo.InvariantCulture));Add(target,d.Z.ToString("R",CultureInfo.InvariantCulture));
            Add(target,d.TruckWorkTicks);Add(target,d.TruckCapacityUnits);
            var truck=d.Truck;Add(target,truck.ActorStableId);Add(target,truck.Stage);Add(target,truck.OrderStableId);Add(target,truck.Batch);
            Add(target,truck.Distance.ToString("R",CultureInfo.InvariantCulture));Add(target,truck.X.ToString("R",CultureInfo.InvariantCulture));
            Add(target,truck.Z.ToString("R",CultureInfo.InvariantCulture));Add(target,truck.VehicleX.ToString("R",CultureInfo.InvariantCulture));
            Add(target,truck.VehicleZ.ToString("R",CultureInfo.InvariantCulture));Add(target,truck.Carrying);
            foreach(var s in d.Shipments){Add(target,s.Id);Add(target,s.SourceId);Add(target,s.Stage);Add(target,s.RequestedTick);
                Add(target,s.ReadyTick??-1);Add(target,s.LoadedTick??-1);Add(target,s.UnloadedTick??-1);Add(target,s.ReceivedTick??-1);
                foreach(var q in s.Quantity)Add(target,q);foreach(var q in s.Cargo)Add(target,q);}
        }
    }
}
