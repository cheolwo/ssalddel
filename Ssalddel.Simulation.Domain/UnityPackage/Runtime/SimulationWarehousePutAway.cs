using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelCodeMetadata(
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeFeatureKeys.FoodWorkflowLineage,
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeLayer.Domain,
        "운영 창고의 검수 후 적치 의미를 세션 재고와 NPC 작업으로 재구성",
        StepKey = "domain.warehouse-put-away-adaptation", FlowOrder = 50,
        ExecutionStage = Ssalddel.Contracts.Common.Metadata.SsalddelCodeExecutionStage.Confirm,
        ReadsFrom = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        WritesTo = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        Effects = Ssalddel.Contracts.Common.Metadata.SsalddelCodeEffect.StateMutation,
        SourceCodeRefs = new[] { "Ssalddel/Controllers/Common/창고작업Controller.cs" },
        ReuseKind = "SemanticAdaptation",
        Adaptation = "운영 창고입고 상태 의미에 대응하지만 현재 구현은 공통 Catalog를 직접 호출하지 않는다. 운영 창고/사용자 CRUD·입고 원장·권한·EF 저장 대신 세션 inventory/task/effect를 사용한다.",
        Boundary = "검수된 Simulation 재고만 적치; 운영 입고·재고·재위탁 변경 없음")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private const string 창고적재수량EffectCode = "WarehousePutAwayQuantity";

        public SimulationDecisionPreviewSnapshot PreviewWarehousePutAway(
            SimulationWarehousePutAwayPreviewRequest request)
        {
            ValidateWarehousePutAwayRequest(request);
            lock (gate)
            {
                return CreateDecisionPreview(CreateWarehousePutAwayDecisionRequest(request, true));
            }
        }

        public 경영SimulationSessionSnapshot ConfirmWarehousePutAway(
            SimulationWarehousePutAwayConfirmRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            ValidateWarehousePutAwayRequest(request.PutAway);

            lock (gate)
            {
                var deterministicPreview = CreateWarehousePutAwayDecisionRequest(request.PutAway, false);
                if (!appliedDecisionCommands.ContainsKey(request.CommandId))
                {
                    var validationPreview = CreateWarehousePutAwayDecisionRequest(request.PutAway, true);
                    var block = validationPreview.BlockReasonCodes.FirstOrDefault();
                    if (block != null)
                        throw new SimulationConflictException(block);
                }
                return ConfirmDecision(new SimulationDecisionConfirmRequest
                {
                    CommandId = request.CommandId,
                    ExpectedRevision = request.ExpectedRevision,
                    Preview = deterministicPreview,
                });
            }
        }

        private SimulationDecisionPreviewRequest CreateWarehousePutAwayDecisionRequest(
            SimulationWarehousePutAwayPreviewRequest request,
            bool includeValidationBlocks)
        {
            if (!npcFacilityInventories.TryGetValue(request.InventoryStableId.Trim(), out var inventory))
                throw new SimulationNotFoundException("SimulationWarehouseInventoryNotFound");

            var blocks = new List<string>();
            if (includeValidationBlocks)
            {
                if (inventory.Revision != request.InventoryRevision)
                    blocks.Add("SimulationWarehouseInventoryRevisionMismatch");
                if (!string.Equals(
                        inventory.StateCode,
                        SimulationNpcInventoryStateCodes.StorageEligible,
                        StringComparison.Ordinal))
                {
                    blocks.Add("SimulationWarehouseInventoryNotPutAwayPending");
                }
                if (HasActivePutAwayTask(inventory.InventoryStableId))
                    blocks.Add("SimulationWarehousePutAwayAlreadyScheduled");
            }

            var sources = MergeSources(
                request.SourceStableIds,
                new[]
                {
                    inventory.InventoryStableId,
                    PyeongchangSimulationWorldStableIds.창고적재규칙,
                });
            return new SimulationDecisionPreviewRequest
            {
                DecisionStableId = "decision:warehouse-put-away:" + inventory.InventoryStableId,
                DecisionTypeCode = SimulationNpcActionCodes.WarehouseStorageMove,
                ActorStableId = request.ActorStableId.Trim(),
                TargetStableIds = new[]
                {
                    inventory.InventoryStableId,
                    inventory.LotStableId,
                    "warehouse-inventory-revision:" + request.InventoryRevision,
                },
                ExpectedEffects = new[]
                {
                    new SimulationValueProjection
                    {
                        ValueTypeCode = 창고적재수량EffectCode,
                        TargetLedgerStableId = inventory.InventoryStableId,
                        BeforeValue = 0m,
                        Delta = inventory.Quantity,
                        AfterValue = inventory.Quantity,
                        UnitCode = inventory.UnitCode,
                        SourceStableIds = sources,
                    },
                },
                BlockReasonCodes = blocks.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SourceStableIds = sources,
                Task = new SimulationTaskPlanRequest
                {
                    TaskStableId = "task:warehouse-put-away:" + inventory.InventoryStableId,
                    TaskTypeCode = "WarehousePutAway",
                    FacilityStableId = inventory.FacilityStableId,
                    ActionCode = SimulationNpcActionCodes.WarehouseStorageMove,
                    AssignedActorStableId = request.ActorStableId.Trim(),
                    PreferredSpatialStableId = request.PreferredSpatialStableId.Trim(),
                    AssignedCapacity = inventory.Quantity,
                    AssignedCapacityUnitCode = inventory.UnitCode,
                    DurationTicks = request.PutAwayDurationTicks,
                    InputLotStableIds = new[] { inventory.LotStableId },
                    OutputCandidateCodes = new[] { SimulationNpcInventoryStateCodes.PutAwayCompleted },
                    SourceStableIds = sources,
                },
            };
        }

        private bool HasActivePutAwayTask(string inventoryStableId)
            => tasks.Values.Any(task =>
                string.Equals(task.ActionCode, SimulationNpcActionCodes.WarehouseStorageMove, StringComparison.Ordinal)
                && task.StateCode != SimulationTaskStateCodes.Completed
                && task.StateCode != SimulationTaskStateCodes.Cancelled
                && decisions.TryGetValue(task.CausedByDecisionStableId, out var decision)
                && decision.TargetStableIds.Contains(inventoryStableId, StringComparer.Ordinal));

        private static void ValidateWarehousePutAwayRequest(
            SimulationWarehousePutAwayPreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.InventoryStableId, "SimulationWarehouseInventoryStableIdInvalid");
            if (request.InventoryRevision <= 0)
                throw new SimulationContractException("SimulationWarehouseInventoryRevisionInvalid");
            RequireStableId(request.ActorStableId, "SimulationActorStableIdInvalid");
            if (!string.IsNullOrWhiteSpace(request.PreferredSpatialStableId))
                RequireStableId(request.PreferredSpatialStableId,
                    "SimulationPreferredSpatialStableIdInvalid");
            if (request.PutAwayDurationTicks <= 0 || request.PutAwayDurationTicks > 7)
                throw new SimulationContractException("SimulationWarehousePutAwayDurationInvalid");
            ValidateIds(request.SourceStableIds, true, "SimulationWarehousePutAwaySourceStableIdsInvalid");
        }
    }
}
