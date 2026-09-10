using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Warehouse
{
    /// <summary>내보내기 도구 전용의 명시적 합성 시작 입력. Unity 실행 중 실패 대체용으로 호출하지 않는다.</summary>
    public static class 로컬생활구성표본
    {
        public static 경영SimulationSession생성Request Create(Guid id, int courierCount = 3, string idPrefix = "")
        {
            if(courierCount < 1 || courierCount > 4) throw new ArgumentOutOfRangeException(nameof(courierCount));
            var r = 가상배달관찰표본.CreateNeighborhoodLife(id);
            var w = r.NpcWorkforce!;
            var prototype = w.Actors.First(x => x.ReferenceRoleCode == "Courier");
            var grant = w.CapabilityGrants.First(x => x.ActorStableId == prototype.ActorStableId);
            w.Actors = w.Actors.Where(x => x.ReferenceRoleCode != "Courier").Concat(Enumerable.Range(1,courierCount).Select(i =>
                new SimulationNpcActorInitialRequest {ActorStableId="actor:synthetic-courier:"+i,OrganizationStableId=prototype.OrganizationStableId,
                    DisplayName="배달 기사 "+i,HomeFacilityStableId=prototype.HomeFacilityStableId,ReferenceRoleCode="Courier",
                    MaximumConcurrentTasks=prototype.MaximumConcurrentTasks,AssignableCapabilityCodes=prototype.AssignableCapabilityCodes.ToArray(),SourceStableIds=prototype.SourceStableIds.ToArray()})).ToArray();
            w.CapabilityGrants = w.CapabilityGrants.Where(x => !x.ActorStableId.StartsWith("actor:synthetic-courier:",StringComparison.Ordinal))
                .Concat(Enumerable.Range(1,courierCount).Select(i => new SimulationNpcCapabilityGrantInitialRequest {
                    GrantStableId="grant:life:courier:"+i,ActorStableId="actor:synthetic-courier:"+i,OrganizationStableId=grant.OrganizationStableId,
                    FacilityStableId=grant.FacilityStableId,CapabilityCode=grant.CapabilityCode,GrantedByActorStableId="actor:synthetic-courier:"+i,SourceStableIds=grant.SourceStableIds.ToArray()})).ToArray();
            string Actor(string value) => value.Insert(value.IndexOf(':')+1,idPrefix);
            string Facility(string value) => value.Insert(value.IndexOf(':')+1,idPrefix);
            var config = new 로컬생활구성 {SourceBundleSha256=new string('0',64),
                Actors=w.Actors.Select(x => new 로컬생활객체결속 {SlotKey=x.ActorStableId,ActorStableId=Actor(x.ActorStableId),DisplayName=x.DisplayName,Role=x.ReferenceRoleCode})
                    .Concat(new[]{"a","b"}.Select(x => new 로컬생활객체결속 {SlotKey="participant:synthetic:"+x,
                        ActorStableId=Actor("participant:synthetic:"+x),DisplayName="주민 "+x.ToUpperInvariant(),Role="Resident"})).OrderBy(x=>x.SlotKey,StringComparer.Ordinal).ToArray(),
                Facilities = new[]{("facility:sim.restaurant-1","음식점"),("facility:synthetic:a","주택 A"),("facility:synthetic:b","주택 B"),
                    ("facility:synthetic:mart","마트"),("facility:synthetic:depot","보충 창고"),("facility:synthetic:courier-base","기사 대기점")}
                    .Select(x => new 로컬생활시설결속 {SlotKey=x.Item1,FacilityStableId=Facility(x.Item1),DisplayName=x.Item2}).ToArray()};
            foreach(var a in w.Actors) {a.ActorStableId=Actor(a.ActorStableId);a.HomeFacilityStableId=Facility(a.HomeFacilityStableId);}
            foreach(var g in w.CapabilityGrants) {g.ActorStableId=Actor(g.ActorStableId);g.GrantedByActorStableId=Actor(g.GrantedByActorStableId);g.FacilityStableId=Facility(g.FacilityStableId);}
            foreach(var o in w.Organizations) o.FacilityStableIds=o.FacilityStableIds.Select(Facility).ToArray();
            foreach(var p in w.Policies) p.FacilityStableId=Facility(p.FacilityStableId);
            r.LocalLife=config;return r;
        }
    }
}
