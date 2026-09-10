using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    /// <summary>보관 완료된 독립 모의 재고에서 출고 준비까지만 관찰한다.</summary>
    public static class 창고출고관찰표본
    {
        public static 경영SimulationSession생성Request Create(Guid clientRequestId)
        {
            var request = 창고입고관찰표본.Create(clientRequestId);
            request.ScenarioStableId = "scenario:hub-outbound-observer.v1";
            request.ScenarioDataRevision = "hub-outbound-observer.r1";
            request.NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture.CreateHubOutboundReadyFixture();
            request.NpcWorkforce.Policies = request.NpcWorkforce.Policies.Where(value =>
                value.ActionCode == SimulationNpcActionCodes.WarehouseOutboundFlow).ToArray();
            // 기존 복합 Fixture에서 피킹 정의만 재사용한다. Farm/마트/회랑 정의는 세션에 넣지 않는다.
            var picking = PyeongchangSimulation공간상호작용Fixture.CreateFarmHubSupply(
                "facility:fixture:unselected-farm").Definitions.Single(value =>
                    value.SpatialStableId == PyeongchangSimulation공간StableIds.진부Hub피킹공간);
            var spatial = request.SpatialWorld ?? throw new InvalidOperationException("창고 공간 표본이 필요합니다.");
            spatial.Definitions = spatial.Definitions.Concat(new[] { picking }).ToArray();
            return request;
        }
    }
}
