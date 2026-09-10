using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Domain
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelCodeMetadata(
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeFeatureKeys.FoodWorkflowLineage,
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeLayer.Domain,
        "음식점 진행의 조리중·픽업대기 의미를 NPC 조리 슬롯과 Tick 작업으로 재구성",
        StepKey = "domain.restaurant-cooking-adaptation", FlowOrder = 25,
        ExecutionStage = Ssalddel.Contracts.Common.Metadata.SsalddelCodeExecutionStage.Tick,
        ReadsFrom = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        WritesTo = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        Effects = Ssalddel.Contracts.Common.Metadata.SsalddelCodeEffect.StateMutation,
        SourceCodeRefs = new[] { "Ssalddel/Services/Food/음식점주문진행Policy.cs" },
        ReuseKind = "SemanticAdaptation",
        Adaptation = "운영 조리예상분 대신 WorkDurationTicks/CookingSlots/NPC capability를 사용한다. 메뉴·가격·리뷰·운영자 계정은 이관하지 않는다.",
        Boundary = "조리 완료는 픽업 대기만 만들며 기사 배정·전달을 자동 확정하지 않음")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private void AdvanceRestaurantAndDecisionWork()
        {
            AdvanceRestaurantAcceptance();
            AdvanceDecisionWork(CurrentTick);
            AdvanceRestaurantCooking();
        }

        private void AdvanceRestaurantCooking()
        {
            foreach (var policy in npcWorkPolicies.Values.Where(value => (value.AutomationEnabled || NeighborhoodLifeEnabled)
                && value.ActionCode == SimulationNpcActionCodes.RestaurantCooking && value.CookingSlots.HasValue)
                .OrderBy(value => value.PolicyStableId, StringComparer.Ordinal))
            {
                var active = tasks.Values.Count(task => task.FacilityStableId == policy.FacilityStableId
                    && task.TaskTypeCode == "FoodPreparationOnly"
                    && task.StateCode != SimulationTaskStateCodes.Completed && task.StateCode != SimulationTaskStateCodes.Cancelled);
                var available = Math.Max(0, policy.CookingSlots!.Value - active);
                if (available == 0 || CurrentTick + policy.WorkDurationTicks > DurationTicks) continue;
                var actor = npcActors.Values.Where(value => value.OrganizationStableId == policy.OrganizationStableId
                    && value.HomeFacilityStableId == policy.FacilityStableId
                    && npcCapabilityGrants.Values.Any(grant => grant.Active && grant.ActorStableId == value.ActorStableId
                        && grant.OrganizationStableId == policy.OrganizationStableId && grant.FacilityStableId == policy.FacilityStableId
                        && grant.CapabilityCode == SimulationNpcCapabilityCodes.RestaurantCooking))
                    .OrderBy(value => value.ActorStableId, StringComparer.Ordinal).FirstOrDefault();
                if (actor == null) continue;
                foreach (var order in foodDeliveries.Values.Where(value => value.RestaurantFacilityStableId == policy.FacilityStableId
                    && !string.IsNullOrEmpty(value.RestaurantResponseDecisionStableId)
                    && value.StateCode == 음식배달상태코드.주문대기
                    && tasks.TryGetValue(value.TaskStableId, out var queued) && queued.TaskTypeCode == "FoodOrderCookingQueued")
                    .OrderBy(value => value.AcceptedTick).ThenBy(value => value.FoodOrderStableId, StringComparer.Ordinal).Take(available).ToArray())
                {
                    var taskId = "task:restaurant-cooking:" + order.FoodOrderStableId;
                    tasks.Add(taskId, new SimulationTaskSnapshot
                    {
                        TaskStableId = taskId, TaskTypeCode = "FoodPreparationOnly", StateCode = SimulationTaskStateCodes.Scheduled,
                        Revision = 1, CausedByDecisionStableId = order.RestaurantResponseDecisionStableId,
                        FacilityStableId = policy.FacilityStableId, AssignedActorStableId = actor.ActorStableId,
                        ScheduledStartTick = CurrentTick + 1, ExpectedEndTick = CurrentTick + policy.WorkDurationTicks,
                        AssignedCapacity = order.Quantity, AssignedCapacityUnitCode = order.UnitCode,
                        InputLotStableIds = new[] { order.FoodOrderStableId }, OutputCandidateCodes = new[] { 음식배달상태코드.픽업대기 },
                        SourceStableIds = MergeSources(order.SourceStableIds, policy.SourceStableIds),
                    });
                    order.TaskStableId = taskId;
                    var effectId = "effect:restaurant-cooking:" + order.FoodOrderStableId;
                    effects.Add(effectId, new SimulationEffectRecord
                    {
                        EffectStableId = effectId, EffectTypeCode = "RestaurantCookingScheduled",
                        StateCode = SimulationEffectStateCodes.Applied, Revision = 1, AppliedTick = CurrentTick,
                        CausedByDecisionStableId = order.RestaurantResponseDecisionStableId, CausedByTaskStableId = taskId,
                        TargetLedgerStableId = order.FoodOrderStableId, BeforeValue = 0, Delta = 1, AfterValue = 1,
                        UnitCode = "count", SourceStableIds = MergeSources(order.SourceStableIds, policy.SourceStableIds),
                    });
                    order.PreparationDurationTicks = policy.WorkDurationTicks;
                    order.TotalDurationTicks = policy.WorkDurationTicks;
                    order.Revision++;
                }
            }
        }
    }
}
