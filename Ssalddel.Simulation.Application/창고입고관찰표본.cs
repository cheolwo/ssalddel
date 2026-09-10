using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    /// <summary>독립 Hub 모의 입고 한 건. 기존 R2 루틴을 사용하며 출고는 시작하지 않는다.</summary>
    public static class 창고입고관찰표본
    {
        public static 경영SimulationSession생성Request Create(Guid clientRequestId)
        {
            var workforce = PyeongchangSimulationNpcWorkforceFixture.CreateHubWarehouseFullLoopFixture();
            workforce.Policies = workforce.Policies.Where(value =>
                value.ActionCode == SimulationNpcActionCodes.WarehouseInboundInspection
                || value.ActionCode == SimulationNpcActionCodes.WarehouseStorageMove).ToArray();
            return new 경영SimulationSession생성Request
            {
                ClientRequestId = clientRequestId,
                ScenarioStableId = "scenario:hub-inbound-observer.v1",
                ScenarioDataRevision = "hub-inbound-observer.r1",
                ScenarioSeed = 300,
                RuleRevision = "world-interaction.hub.r1",
                NpcRoutineControlRevision = SimulationNpcRoutineControlRevisionCodes.R2,
                DurationTicks = 30,
                NpcWorkforce = workforce,
                SpatialWorld = PyeongchangSimulation공간상호작용Fixture.Create(),
                WorldContext = new SimulationWorldContext생성Request
                {
                    FactionStableId = "faction:sim:hub-observer",
                    TerritoryStableId = "territory:sim:hub-observer",
                    SettlementStableId = "settlement:sim:hub-observer",
                    GameDateStartsOn = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
                },
            };
        }
    }
}
