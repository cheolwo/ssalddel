using System;
using System.Linq;
using System.Collections.Generic;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Contracts
{
    public static class 가상동네생활기준
    {
        public const string ScenarioId = "scenario:synthetic-neighborhood-life.r1";
        public const string Revision = "synthetic-neighborhood-life.r1";
        public const int DurationTicks = 1800;
        public const string DepotWorkerId = "actor:synthetic-depot-worker";
        public const string MartWorkerId = "actor:synthetic-mart-worker";
        public const string TruckDriverId = "actor:synthetic-freight-driver";
        public const string RestaurantActorId = "actor:sim.restaurant-1";
        public static bool Matches(string scenario, string revision) => scenario == ScenarioId && revision == Revision;
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "직업·근무·휴식과 업무 참조", Boundary = "주문/재고 원장을 복제하지 않는다")]
    public sealed class 가상생활NpcSnapshot
    {
        public string ActorId { get; set; } = "";
        public string Role { get; set; } = "";
        public string Stage { get; set; } = "Working";
        public string TaskId { get; set; } = "";
        public string Reason { get; set; } = "업무 대기";
        public int StopAcceptingTick { get; set; } = 420;
        public int RestUntilTick { get; set; }
        public int CompletedRestCount { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double WorkX { get; set; }
        public double WorkZ { get; set; }
        public string HomeFacilityId { get; set; } = "";
        public double HomeX { get; set; }
        public double HomeZ { get; set; }
        public int HomeRouteStep { get; set; }
        public int EatingUntilTick { get; set; }
        public int LastMealReceiptTick { get; set; } = -1;
        public string NextAction { get; set; } = "";
        public string[] DayHistory { get; set; } = Array.Empty<string>();
        public 가상생활NpcSnapshot Copy()
        { var copy = (가상생활NpcSnapshot)MemberwiseClone(); copy.DayHistory = DayHistory.ToArray(); return copy; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "보충 출고 물품의 보관·상차·입고 계보", Boundary = "유한 합성 재고, 실제 발주 아님")]
    public sealed class 가상보충운송Snapshot
    {
        public string Id { get; set; } = "";
        public string SourceId { get; set; } = "synthetic-manifest:depot-initial.r1";
        public string Stage { get; set; } = "Reserved";
        public int RequestedTick { get; set; }
        public int? ReadyTick { get; set; }
        public int? LoadedTick { get; set; }
        public int? UnloadedTick { get; set; }
        public int? ReceivedTick { get; set; }
        public int[] Quantity { get; set; } = new[] { 4, 4 };
        public int[] Cargo { get; set; } = new[] { 0, 0 };
        public 가상보충운송Snapshot Copy()
        { var copy = (가상보충운송Snapshot)MemberwiseClone(); copy.Quantity = Quantity.ToArray(); copy.Cargo = Cargo.ToArray(); return copy; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "독립 보충창고와 화물차 상태", Boundary = "마트 재고와 별개 위치의 물품만 소유한다")]
    public sealed class 가상보충창고Snapshot
    {
        public string SourceId { get; set; } = "synthetic-manifest:depot-initial.r1";
        public int[] Incoming { get; set; } = new[] { 40, 40 };
        public int[] Stock { get; set; } = new[] { 0, 0 };
        public int[] Reserved { get; set; } = new[] { 0, 0 };
        public string WorkerStage { get; set; } = "UnloadInbound";
        public string ShipmentId { get; set; } = "";
        public string Reason { get; set; } = "합성 입고 물품 하차";
        public int Shelf { get; set; }
        public int WorkTicks { get; set; }
        public double X { get; set; } = -26;
        public double Z { get; set; } = -8;
        public int TruckWorkTicks { get; set; }
        public int TruckCapacityUnits { get; set; } = 12;
        public 가상배달기사Snapshot Truck { get; set; } = new 가상배달기사Snapshot {
            ActorStableId = 가상동네생활기준.TruckDriverId, X = -26, Z = -3, VehicleX = -26, VehicleZ = -3 };
        public 가상보충운송Snapshot[] Shipments { get; set; } = Array.Empty<가상보충운송Snapshot>();
        public 가상보충창고Snapshot Copy()
        {
            var copy = (가상보충창고Snapshot)MemberwiseClone();
            copy.Incoming = Incoming.ToArray(); copy.Stock = Stock.ToArray(); copy.Reserved = Reserved.ToArray();
            copy.Truck = Truck.Copy(); copy.Shipments = Shipments.Select(x => x.Copy()).ToArray(); return copy;
        }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "30분 직업 생활·자원 점유 사본", Boundary = "기존 프로필에는 생성하지 않는다")]
    public sealed class 가상동네생활Snapshot
    {
        public bool DayEnabled { get; set; }
        public int WorkingTicks { get; set; } = 420;
        public int RestTicks { get; set; } = 120;
        public int RestockThreshold { get; set; } = 2;
        public bool AutomationEnabled { get; set; } = true;
        public int Priority { get; set; } = 100;
        public 가상생활NpcSnapshot[] Actors { get; set; } = Array.Empty<가상생활NpcSnapshot>();
        public 가상보충창고Snapshot Depot { get; set; } = new 가상보충창고Snapshot();
        public Dictionary<string, string> ResourceOwners { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, int> WaitingSince { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public 가상동네생활Snapshot Copy()
        {
            var copy = (가상동네생활Snapshot)MemberwiseClone();
            copy.Actors = Actors.Select(x => x.Copy()).ToArray(); copy.Depot = Depot.Copy();
            copy.ResourceOwners = new Dictionary<string, string>(ResourceOwners, StringComparer.Ordinal);
            copy.WaitingSince = new Dictionary<string, int>(WaitingSince, StringComparer.Ordinal); return copy;
        }
    }
}
