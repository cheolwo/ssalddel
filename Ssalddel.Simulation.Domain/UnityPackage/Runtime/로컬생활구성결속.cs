using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private readonly 로컬생활구성? localLife;
        private static bool LocalLifeEqual(로컬생활구성? a, 로컬생활구성? b)
        {
            if(a == null || b == null) return a == b;
            return a.Revision == b.Revision && a.SourceBundleSha256 == b.SourceBundleSha256 && b.Actors != null && b.Facilities != null
                && a.Actors.OrderBy(x=>x.SlotKey,StringComparer.Ordinal).Select(x=>(x.SlotKey,x.ActorStableId,x.DisplayName,x.Role,x.VisualKey))
                    .SequenceEqual(b.Actors.OrderBy(x=>x.SlotKey,StringComparer.Ordinal).Select(x=>(x.SlotKey,x.ActorStableId,x.DisplayName,x.Role,x.VisualKey)))
                && a.Facilities.OrderBy(x=>x.SlotKey,StringComparer.Ordinal).Select(x=>(x.SlotKey,x.FacilityStableId,x.DisplayName))
                    .SequenceEqual(b.Facilities.OrderBy(x=>x.SlotKey,StringComparer.Ordinal).Select(x=>(x.SlotKey,x.FacilityStableId,x.DisplayName)));
        }
        private string LocalActorId(string slot) => localLife == null ? slot :
            localLife.Actors.Single(x => x.SlotKey == slot).ActorStableId;
        private string LocalFacilityId(string slot) => localLife == null ? slot :
            localLife.Facilities.Single(x => x.SlotKey == slot).FacilityStableId;
        private string ResidentId(string residence) => LocalActorId("participant:synthetic:" + residence);
        private string ResidenceId(string residence) => LocalFacilityId("facility:synthetic:" + residence);
        private string ResidenceKey(string facility) => new[] { "a", "b" }.Single(x => ResidenceId(x) == facility);
        private double ResidenceX(string facility) => 가상동네배치기준.기준점("home-" + ResidenceKey(facility) + "-receive").x;
        private string[] CourierIds => localLife == null
            ? Enumerable.Range(1, 3).Select(i => "actor:synthetic-courier:" + i).ToArray()
            : localLife.Actors.Where(x => x.Role == "Courier").OrderBy(x => x.SlotKey, StringComparer.Ordinal)
                .Select(x => x.ActorStableId).ToArray();

        private static 로컬생활구성? ValidateLocalLife(경영SimulationSession생성Request request)
        {
            if (request.LocalLife == null) return null;
            var value = request.LocalLife;
            void Require(bool ok, string code) { if (!ok) throw new SimulationContractException(code); }
            Require(가상동네생활기준.Matches(request.ScenarioStableId, request.ScenarioDataRevision)
                && !request.NeighborhoodDayEnabled && request.DurationTicks == 1800, "OfflineLifeProfileMismatch");
            Require(value.Revision == "offline-life.r1" && value.SourceBundleSha256 != null
                && value.SourceBundleSha256.Length == 64 && value.SourceBundleSha256.All(Uri.IsHexDigit), "OfflineLifeSourceHashInvalid");
            if (value.Actors == null || value.Facilities == null || value.Actors.Any(x => x == null)
                || value.Facilities.Any(x => x == null)) throw new SimulationContractException("OfflineLifeBindingsMissing");
            var required = new[] { (가상동네생활기준.RestaurantActorId,"RestaurantOperator"),
                (가상동네생활기준.MartWorkerId,"WarehouseWorker"), (가상동네생활기준.DepotWorkerId,"WarehouseWorker"),
                (가상동네생활기준.TruckDriverId,"FreightDriver"), ("participant:synthetic:a","Resident"),
                ("participant:synthetic:b","Resident") };
            Require(value.Actors.All(x => !string.IsNullOrWhiteSpace(x.ActorStableId) && x.ActorStableId.Length <= 120
                && !string.IsNullOrWhiteSpace(x.DisplayName) && x.DisplayName.Length <= 80 && x.VisualKey == "local-life:actor")
                && value.Actors.All(x => x.ActorStableId.StartsWith(x.Role == "Resident" ? "participant:" : "actor:",StringComparison.Ordinal))
                && value.Actors.Select(x => x.SlotKey).Distinct().Count() == value.Actors.Length
                && value.Actors.Select(x => x.ActorStableId).Distinct().Count() == value.Actors.Length, "OfflineLifeActorInvalid");
            Require(required.All(r => value.Actors.Count(x => x.SlotKey == r.Item1 && x.Role == r.Item2) == 1), "OfflineLifeRoleMissing");
            var couriers = value.Actors.Where(x => x.Role == "Courier").OrderBy(x => x.SlotKey,StringComparer.Ordinal).ToArray();
            // 5개 대기점 중 귀환용 여유 1개를 보존한다. 무제한 NPC·통행 일반화가 아니다.
            Require(couriers.Length >= 1 && couriers.Length <= 4 && value.Actors.Length == required.Length + couriers.Length
                && couriers.Select(x => x.SlotKey).SequenceEqual(Enumerable.Range(1,couriers.Length).Select(i => "actor:synthetic-courier:" + i)), "OfflineLifeCourierCapacityInvalid");
            var facilities = new[] { "facility:sim.restaurant-1", "facility:synthetic:a", "facility:synthetic:b", "facility:synthetic:mart", "facility:synthetic:depot", "facility:synthetic:courier-base" };
            Require(value.Facilities.Length == facilities.Length && facilities.All(s => value.Facilities.Count(x => x.SlotKey == s) == 1)
                && value.Facilities.All(x => !string.IsNullOrWhiteSpace(x.FacilityStableId) && x.FacilityStableId.StartsWith("facility:",StringComparison.Ordinal) && x.FacilityStableId.Length <= 120)
                && value.Facilities.Select(x => x.FacilityStableId).Distinct().Count() == facilities.Length
                && !value.Facilities.Any(f => value.Actors.Any(a => a.ActorStableId == f.FacilityStableId)), "OfflineLifeFacilityInvalid");
            Require(request.NpcWorkforce != null, "OfflineLifeWorkforceMissing");
            foreach (var a in value.Actors.Where(x => x.Role != "Resident"))
            {
                Require(request.NpcWorkforce!.Actors.Count(x => x.ActorStableId == a.ActorStableId && x.ReferenceRoleCode == a.Role) == 1,
                    "OfflineLifeWorkforceBindingMismatch");
                var actor = request.NpcWorkforce!.Actors.Single(x => x.ActorStableId == a.ActorStableId);
                var capability = a.Role == "RestaurantOperator" ? SimulationNpcCapabilityCodes.RestaurantCooking :
                    a.Role == "WarehouseWorker" ? SimulationNpcCapabilityCodes.WarehouseOutboundPreparation : SimulationNpcCapabilityCodes.FreightTransport;
                Require(actor.AssignableCapabilityCodes.Contains(capability) && request.NpcWorkforce.CapabilityGrants.Any(g =>
                    g.ActorStableId == a.ActorStableId && g.OrganizationStableId == actor.OrganizationStableId &&
                    g.FacilityStableId == actor.HomeFacilityStableId && g.CapabilityCode == capability), "OfflineLifeCapabilityMissing");
            }
            return value.Copy();
        }
    }
}
