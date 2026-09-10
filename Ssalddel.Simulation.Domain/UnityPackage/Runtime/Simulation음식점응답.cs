using System;
using System.Globalization;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Domain
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelCodeMetadata(
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeFeatureKeys.FoodWorkflowLineage,
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeLayer.Domain,
        "운영 음식점의 주문 수락·거절 의미를 세션 결정과 작업으로 재구성",
        StepKey = "domain.restaurant-response-adaptation", FlowOrder = 20,
        ExecutionStage = Ssalddel.Contracts.Common.Metadata.SsalddelCodeExecutionStage.Confirm,
        ReadsFrom = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        WritesTo = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        Effects = Ssalddel.Contracts.Common.Metadata.SsalddelCodeEffect.StateMutation,
        SourceCodeRefs = new[] { "Ssalddel/Application/Food/Handlers/음식점주문수락CommandHandler.cs", "Ssalddel/Application/Food/Handlers/음식점주문진행변경CommandHandler.cs", "Ssalddel/Services/Food/음식점주문진행Policy.cs" },
        ReuseKind = "SemanticAdaptation",
        Adaptation = "운영의 주문 상태 의미를 사용하지만 사용자·음식점 접근권한, UTC 이력, Store/Event를 호출하지 않는다. 세션 Actor capability와 ExpectedRevision으로 별도 판정한다.",
        Boundary = "Simulation 결정/작업만 변경; 운영 음식점 주문·계정·알림 없음")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private const string RestaurantAccept = "RestaurantOrderAccept";
        private const string RestaurantReject = "RestaurantOrderReject";
        private const string RestaurantQueueAccept = "RestaurantOrderQueueAccept";

        public 경영SimulationSessionSnapshot ConfirmRestaurantResponse(Simulation음식점응답Request request)
            => ConfirmRestaurantResponseCore(request, false);

        private 경영SimulationSessionSnapshot ConfirmRestaurantResponseCore(Simulation음식점응답Request request, bool automatic)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            RequireStableId(request.ActorStableId, "SimulationActorStableIdInvalid");
            RequirePrefixedId(request.FoodOrderStableId, "food-order:", "SimulationFoodDeliveryStableIdInvalid");
            if (request.FoodOrderRevision <= 0 || request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            if (!request.Accept) RequirePrefixedId(request.RejectionReasonCode, "reason:", "SimulationRestaurantRejectionReasonRequired");
            else if (!string.IsNullOrEmpty(request.RejectionReasonCode))
                throw new SimulationContractException("SimulationRestaurantAcceptanceReasonInvalid");
            lock (gate)
            {
                if (!foodDeliveries.TryGetValue(request.FoodOrderStableId.Trim(), out var order))
                    throw new SimulationNotFoundException("SimulationFoodDeliveryNotFound");
                var sourceIds = Copy(order.SourceStableIds);
                var command = new SimulationDecisionConfirmRequest
                {
                    CommandId = request.CommandId, ExpectedRevision = request.ExpectedRevision,
                    Preview = new SimulationDecisionPreviewRequest
                    {
                        DecisionStableId = "decision:restaurant-response:" + order.FoodOrderStableId,
                        DecisionTypeCode = automatic ? RestaurantQueueAccept : request.Accept ? RestaurantAccept : RestaurantReject,
                        ActorStableId = request.ActorStableId.Trim(),
                        TargetStableIds = new[] { order.FoodOrderStableId,
                            "order-revision:" + request.FoodOrderRevision.ToString(CultureInfo.InvariantCulture) }
                            .Concat(request.Accept ? Array.Empty<string>() : new[] { request.RejectionReasonCode.Trim() }).ToArray(),
                        SourceStableIds = sourceIds,
                        ExpectedEffects = new[] { Projection("RestaurantResponseRecorded", order.FoodOrderStableId, 1m, "count", sourceIds) },
                        Task = new SimulationTaskPlanRequest
                        {
                            TaskStableId = "task:restaurant-response:" + order.FoodOrderStableId,
                            TaskTypeCode = automatic ? "FoodOrderCookingQueued" : request.Accept ? "FoodPreparationOnly" : "FoodOrderRejected",
                            FacilityStableId = order.RestaurantFacilityStableId,
                            DurationTicks = automatic ? 1 : request.Accept ? order.PreparationDurationTicks : 1,
                            AssignedCapacity = order.Quantity, AssignedCapacityUnitCode = order.UnitCode,
                            InputLotStableIds = new[] { order.FoodOrderStableId },
                            OutputCandidateCodes = new[] { automatic ? "FoodOrderCookingQueued" : request.Accept ? 음식배달상태코드.픽업대기 : 음식배달상태코드.거절 },
                            SourceStableIds = sourceIds,
                        },
                    },
                };
                // Tick 재생이 같은 자동 결정을 다시 만들므로 독립 명령으로 중복 기록하지 않는다.
                return automatic ? ConfirmDecisionCore(command, false, _ => { }) : ConfirmDecision(command);
            }
        }

        private 음식점응답준비? PrepareRestaurantResponse(SimulationDecisionPreviewRequest request)
        {
            var queued = request.DecisionTypeCode == RestaurantQueueAccept;
            if (!queued && request.DecisionTypeCode != RestaurantAccept && request.DecisionTypeCode != RestaurantReject) return null;
            var id = FindTarget(request.TargetStableIds, "food-order:", "SimulationFoodDeliveryStableIdInvalid");
            if (!foodDeliveries.TryGetValue(id, out var order))
                throw new SimulationNotFoundException("SimulationFoodDeliveryNotFound");
            var revision = FindTarget(request.TargetStableIds, "order-revision:", "SimulationFoodDeliveryRevisionInvalid");
            if (revision != "order-revision:" + order.Revision.ToString(CultureInfo.InvariantCulture))
                throw new SimulationConflictException("SimulationFoodDeliveryRevisionMismatch");
            if (!string.IsNullOrEmpty(order.RestaurantResponseDecisionStableId)
                || order.StateCode != 음식배달상태코드.주문대기
                || !tasks.TryGetValue(order.TaskStableId, out var submission)
                || submission.TaskTypeCode != (queued ? "FoodOrderNpcSubmission" : "FoodOrderInboxSubmission"))
                throw new SimulationConflictException("SimulationRestaurantOrderNotPending");
            if (!npcActors.TryGetValue(request.ActorStableId, out var actor)
                || actor.HomeFacilityStableId != order.RestaurantFacilityStableId
                || !npcCapabilityGrants.Values.Any(grant => grant.Active
                    && grant.ActorStableId == actor.ActorStableId && grant.OrganizationStableId == actor.OrganizationStableId
                    && grant.FacilityStableId == order.RestaurantFacilityStableId
                    && grant.CapabilityCode == SimulationNpcCapabilityCodes.RestaurantCooking))
                throw new SimulationContractException("SimulationRestaurantOperatorNotAuthorized");
            var accept = queued || request.DecisionTypeCode == RestaurantAccept;
            var reason = accept ? string.Empty : FindTarget(request.TargetStableIds, "reason:", "SimulationRestaurantRejectionReasonRequired");
            if (request.Task.FacilityStableId != order.RestaurantFacilityStableId
                || request.Task.TaskTypeCode != (queued ? "FoodOrderCookingQueued" : accept ? "FoodPreparationOnly" : "FoodOrderRejected")
                || request.Task.DurationTicks != (queued ? 1 : accept ? order.PreparationDurationTicks : 1)
                || request.Task.AssignedCapacity != order.Quantity
                || request.Task.AssignedCapacityUnitCode != order.UnitCode
                || !request.Task.InputLotStableIds.SequenceEqual(new[] { order.FoodOrderStableId })
                || !request.Task.OutputCandidateCodes.SequenceEqual(new[] { queued ? "FoodOrderCookingQueued" : accept ? 음식배달상태코드.픽업대기 : 음식배달상태코드.거절 }))
                throw new SimulationContractException("SimulationRestaurantResponseTaskInvalid");
            return new 음식점응답준비(order, accept, reason);
        }

        private void ScheduleRestaurantResponse(음식점응답준비? response, SimulationDecisionSnapshot decision, SimulationTaskSnapshot task)
        {
            if (response == null) return;
            var order = response.Order;
            order.RestaurantResponseDecisionStableId = decision.DecisionStableId;
            order.RejectionReasonCode = response.Reason;
            order.TaskStableId = task.TaskStableId;
            order.TotalDurationTicks = task.ExpectedEndTick - task.ScheduledStartTick + 1;
            if (!response.Accept) TransitionFoodDelivery(order, 음식배달상태코드.거절, CurrentTick, task.TaskStableId);
            else order.Revision++;
            음식점응답연결(order.FoodOrderStableId, response.Accept);
        }

        private void AdvanceRestaurantAcceptance()
        {
            if (CurrentTick >= DurationTicks) return;
            foreach (var order in foodDeliveries.Values
                .Where(value => string.IsNullOrEmpty(value.RestaurantResponseDecisionStableId)
                    && value.AcceptedTick < CurrentTick && tasks.TryGetValue(value.TaskStableId, out var submission)
                    && submission.TaskTypeCode == "FoodOrderNpcSubmission")
                .OrderBy(value => value.AcceptedTick).ThenBy(value => value.FoodOrderStableId, StringComparer.Ordinal).ToArray())
            {
                var actor = npcActors.Values.Where(value => value.HomeFacilityStableId == order.RestaurantFacilityStableId
                    && npcCapabilityGrants.Values.Any(grant => grant.Active && grant.ActorStableId == value.ActorStableId
                        && grant.OrganizationStableId == value.OrganizationStableId && grant.FacilityStableId == value.HomeFacilityStableId
                        && grant.CapabilityCode == SimulationNpcCapabilityCodes.RestaurantCooking))
                    .OrderBy(value => value.ActorStableId, StringComparer.Ordinal).FirstOrDefault();
                if (actor == null || !LifeCanAccept(actor.ActorStableId) || (NeighborhoodLifeEnabled && !Life.AutomationEnabled)) continue;
                ConfirmRestaurantResponseCore(new Simulation음식점응답Request
                {
                    CommandId = "command:restaurant-auto-accept:" + order.FoodOrderStableId,
                    ExpectedRevision = Revision, FoodOrderStableId = order.FoodOrderStableId,
                    FoodOrderRevision = order.Revision, ActorStableId = actor.ActorStableId, Accept = true,
                }, true);
            }
        }

        private sealed class 음식점응답준비
        {
            public Simulation음식배달Snapshot Order { get; }
            public bool Accept { get; }
            public string Reason { get; }
            public 음식점응답준비(Simulation음식배달Snapshot order, bool accept, string reason)
            { Order = order; Accept = accept; Reason = reason; }
        }
    }
}
