using System;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Warehouse
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1,
        "실제 데이터 없이 합성 배달 관찰 프로필을 명시적으로 선택한다.", Boundary = "기존 수동 음식점 표본과 별도 슬롯이다.")]
    public static class 가상배달관찰표본
    {
        public const string ScenarioId = "scenario:synthetic-delivery.r1";
        public const string SlotId = "synthetic-delivery-r1-primary";
        public const string WaitingSlotId = "synthetic-delivery-r2-primary";
        public const string MartSlotId = "synthetic-delivery-r3-primary";
        public const string OrderFlowSlotId = "synthetic-delivery-r4-primary";
        public const string NeighborhoodLifeSlotId = "synthetic-neighborhood-life-r1-primary";
        public static 경영SimulationSession생성Request CreateNeighborhoodLife(Guid id, bool dayEnabled = false)
        {
            var request = CreateOrderFlow(id);
            request.ScenarioStableId = 가상동네생활기준.ScenarioId;
            request.ScenarioDataRevision = 가상동네생활기준.Revision;
            request.DurationTicks = 가상동네생활기준.DurationTicks;
            request.NeighborhoodDayEnabled = dayEnabled;
            var workforce=request.NpcWorkforce!;
            var rows=new[] {
                (가상동네생활기준.MartWorkerId,"facility:synthetic:mart","WarehouseWorker",SimulationNpcCapabilityCodes.WarehouseOutboundPreparation),
                (가상동네생활기준.DepotWorkerId,"facility:synthetic:depot","WarehouseWorker",SimulationNpcCapabilityCodes.WarehouseOutboundPreparation),
                (가상동네생활기준.TruckDriverId,"facility:synthetic:depot","FreightDriver",SimulationNpcCapabilityCodes.FreightTransport),
                ("actor:synthetic-courier:1","facility:synthetic:courier-base","Courier",SimulationNpcCapabilityCodes.FreightTransport),
                ("actor:synthetic-courier:2","facility:synthetic:courier-base","Courier",SimulationNpcCapabilityCodes.FreightTransport),
                ("actor:synthetic-courier:3","facility:synthetic:courier-base","Courier",SimulationNpcCapabilityCodes.FreightTransport) };
            const string organization="organization:synthetic:logistics";
            var sources=new[]{"source:"+가상동네생활기준.Revision};
            workforce.Organizations=workforce.Organizations.Concat(new[]{new SimulationNpcOrganizationInitialRequest {
                OrganizationStableId=organization,DisplayName="합성 도심 업무",FacilityStableIds=rows.Select(x=>x.Item2).Distinct().ToArray(),
                AllowedCapabilityCodes=rows.Select(x=>x.Item4).Distinct().ToArray(),SourceStableIds=sources }}).ToArray();
            workforce.Actors=workforce.Actors.Concat(rows.Select(x=>new SimulationNpcActorInitialRequest {
                ActorStableId=x.Item1,OrganizationStableId=organization,DisplayName=x.Item1,HomeFacilityStableId=x.Item2,
                ReferenceRoleCode=x.Item3,MaximumConcurrentTasks=1,AssignableCapabilityCodes=new[]{x.Item4},SourceStableIds=sources })).ToArray();
            workforce.CapabilityGrants=workforce.CapabilityGrants.Concat(rows.Select(x=>new SimulationNpcCapabilityGrantInitialRequest {
                GrantStableId="grant:life:"+x.Item1,ActorStableId=x.Item1,OrganizationStableId=organization,FacilityStableId=x.Item2,
                CapabilityCode=x.Item4,GrantedByActorStableId=x.Item1,SourceStableIds=sources })).ToArray();
            return request;
        }
        public static 경영SimulationSession생성Request CreateOrderFlow(Guid id)
        {
            var request = CreateMart(id);
            request.ScenarioStableId = "scenario:synthetic-delivery.r4";
            request.ScenarioDataRevision = "synthetic-delivery.r4";
            return request;
        }
        public static 경영SimulationSession생성Request CreateMart(Guid id)
        {
            var request = Create(id);
            request.ScenarioStableId = "scenario:synthetic-delivery.r3";
            request.ScenarioDataRevision = "synthetic-delivery.r3";
            request.DurationTicks = 365;
            return request;
        }
        public static 경영SimulationSession생성Request CreateWaiting(Guid id)
        {
            var request = Create(id);
            request.ScenarioStableId = "scenario:synthetic-delivery.r2";
            request.ScenarioDataRevision = "synthetic-delivery.r2";
            return request;
        }
        public static 경영SimulationSession생성Request Create(Guid id)
        {
            var request = 음식점관찰표본.Create(id);
            request.ScenarioStableId = ScenarioId;
            request.ScenarioDataRevision = "synthetic-delivery.r1";
            request.DurationTicks = 365;
            return request;
        }
    }
}
