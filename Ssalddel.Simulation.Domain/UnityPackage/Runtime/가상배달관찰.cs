using System;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private 가상배달기사Snapshot? syntheticCourier;
        internal bool ReplayingSyntheticRecords { get; set; }
        private bool SyntheticDeliveryEnabled => ScenarioStableId == "scenario:synthetic-delivery.r1"
            && ScenarioDataRevision == "synthetic-delivery.r1";

        [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
            "합성 프로필의 단일 기사·두 주문·도로 이동·인계를 공통 Tick 안에서 실행한다.",
            Boundary = "기존 표본과 운영 상태는 변경하지 않는다. 실제 Unity 검증은 별도다.")]
        private void AdvanceSyntheticDelivery()
        {
            if (WaitingFleetEnabled) { AdvanceWaitingFleet(); return; }
            if (!SyntheticDeliveryEnabled) return;
            var driver = syntheticCourier ??= new 가상배달기사Snapshot();
            if (driver.Stage == "Idle")
            {
                var next = foodDeliveries.Values.Where(o => o.DeliveryScopeStableId == "delivery-scope:synthetic"
                    && o.ReceivedTick == null && o.StateCode != 음식배달상태코드.거절)
                    .OrderBy(o => o.AcceptedTick).ThenBy(o => o.FoodOrderStableId, StringComparer.Ordinal).FirstOrDefault();
                if (next == null)
                {
                    // 새 묶음이 세션 경계를 걸쳐 미완료로 남지 않도록 여유를 검사한다.
                    if (DurationTicks - CurrentTick < 90) return;
                    driver.Batch++;
                    foreach (var residence in new[] { "a", "b" })
                    {
                        var request = new Simulation음식배달PreviewRequest
                        {
                            FoodOrderStableId = "food-order:synthetic:" + driver.Batch + ":" + residence,
                            MenuItemStableId = "menu-item:sim.potato-stew-1",
                            RestaurantFacilityStableId = "facility:sim.restaurant-1",
                            DestinationFacilityStableId = "facility:synthetic:" + residence,
                            OrdererStableId = "participant:synthetic:" + residence,
                            ActorStableId = "participant:synthetic:" + residence,
                            DeliveryScopeStableId = "delivery-scope:synthetic", Quantity = 1, UnitCode = "serving",
                            RestaurantPreparationOnly = true, AwaitRestaurantResponse = true, NpcAutoAccept = true,
                            PreparationDurationTicks = 2, DeliveryDurationTicks = 2,
                            SourceStableIds = new[] { "source:synthetic-delivery.r1" }
                        };
                        ConfirmDecisionCore(new SimulationDecisionConfirmRequest
                        {
                            CommandId = "command:auto:" + request.FoodOrderStableId,
                            ExpectedRevision = Revision,
                            Preview = CreateFoodDeliveryDecisionRequest(request, CreateFoodDeliveryPreview(request))
                        }, false, _ => { });
                    }
                    return;
                }
                driver.OrderStableId = next.FoodOrderStableId;
                RecordSynthetic("ASSIGN", "Assigned");
                SetSyntheticStage("DriveRestaurant");
                return;
            }
            var order = foodDeliveries[driver.OrderStableId];
            var homeX = order.DestinationFacilityStableId.EndsWith(":a", StringComparison.Ordinal) ? 30d : 10d;
            switch (driver.Stage)
            {
                case "DriveRestaurant": MoveSynthetic(new[] { (0d, 0d), (10d, 0d) }, 5, true, "WalkRestaurant"); break;
                case "WalkRestaurant": MoveSynthetic(new[] { (10d, 0d), (10d, 4d) }, 1, false, "WaitFood"); break;
                case "WaitFood":
                    if (order.ReadyForPickupTick != null)
                    {
                        TransitionFoodDelivery(order, 음식배달상태코드.기사배정, CurrentTick, driver.ActorStableId);
                        order.DispatchCandidateTick = CurrentTick;
                        SetSyntheticStage("Pickup");
                    }
                    break;
                case "Pickup":
                    TransitionFoodDelivery(order, 음식배달상태코드.픽업완료, CurrentTick, driver.ActorStableId);
                    order.PickedUpTick = CurrentTick; driver.Carrying = true;
                    RecordSynthetic("PICKUP", "PickedUp");
                    SetSyntheticStage("WalkRestaurantOut"); break;
                case "WalkRestaurantOut": MoveSynthetic(new[] { (10d, 4d), (10d, 0d) }, 1, false, "DriveHome"); break;
                case "DriveHome": MoveSynthetic(new[] { (10d, 0d), (20d, 0d), (20d, 20d), (homeX, 20d) }, 5, true, "WalkHome"); break;
                case "WalkHome": MoveSynthetic(new[] { (homeX, 20d), (homeX, 24d) }, 1, false, "Deliver"); break;
                case "Deliver":
                    TransitionFoodDelivery(order, 음식배달상태코드.전달완료, CurrentTick, driver.ActorStableId);
                    order.DeliveredTick = CurrentTick; driver.Carrying = false;
                    RecordSynthetic("DELIVER", "Delivered");
                    SetSyntheticStage("Receive"); break;
                case "Receive":
                    TransitionFoodDelivery(order, 음식배달상태코드.수령확인, CurrentTick, order.OrdererStableId);
                    order.ReceivedTick = CurrentTick;
                    RecordSynthetic("RECEIVE", "Received");
                    SetSyntheticStage("WalkHomeOut"); break;
                case "WalkHomeOut": MoveSynthetic(new[] { (homeX, 24d), (homeX, 20d) }, 1, false, "Return"); break;
                case "Return": MoveSynthetic(new[] { (homeX, 20d), (20d, 20d), (20d, 0d), (10d, 0d), (0d, 0d) }, 5, true, "Idle"); break;
                default: throw new SimulationContractException("SyntheticDeliveryStageInvalid");
            }
        }

        private void SetSyntheticStage(string stage)
        {
            syntheticCourier!.Stage = stage;
            syntheticCourier.Distance = 0;
            if (stage == "Idle") syntheticCourier.OrderStableId = string.Empty;
        }

        private void MoveSynthetic((double x, double z)[] points, double speed, bool vehicle, string next)
        {
            var driver = syntheticCourier!;
            var total = 0d;
            for (var i = 1; i < points.Length; i++) total += Segment(points[i - 1], points[i]);
            driver.Distance = Math.Min(total, driver.Distance + speed);
            var remaining = driver.Distance;
            for (var i = 1; i < points.Length; i++)
            {
                var length = Segment(points[i - 1], points[i]);
                if (remaining > length) { remaining -= length; continue; }
                var ratio = length == 0 ? 1 : remaining / length;
                driver.X = points[i - 1].x + (points[i].x - points[i - 1].x) * ratio;
                driver.Z = points[i - 1].z + (points[i].z - points[i - 1].z) * ratio;
                break;
            }
            if (vehicle) { driver.VehicleX = driver.X; driver.VehicleZ = driver.Z; }
            RecordSynthetic("MOVE", "PositionAdvanced");
            if (driver.Distance >= total && next == "Idle") RecordSynthetic("RETURN", "Returned");
            if (driver.Distance >= total) SetSyntheticStage(next);
        }

        private void RecordSynthetic(string action, string outcome)
        {
            var before = Revision;
            Revision++;
            // V28 행위 원장은 바깥 저장 계층에서 복원한다. 기본 명령 재생은 revision만 동일하게 진행한다.
            if (ReplayingSyntheticRecords) return;
            var driver = syntheticCourier!;
            AppendActionManifestationAndProgression(new Simulation행위발현Record
            {
                WorldStableId = "world:synthetic-delivery", SessionStableId = SessionStableId,
                WorldInteractionId = "WI-CITY-SYNTHETIC-" + action,
                CommandId = "command:synthetic:" + CurrentTick + ":" + action + (WaitingFleetEnabled ? ":" + driver.ActorStableId : ""),
                TriggerSourceCode = SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
                InitiatorStableId = driver.ActorStableId, ActorStableId = driver.ActorStableId,
                ActorKindCode = "NpcActor", TargetStableIds = new[] { driver.OrderStableId },
                OutcomeStableId = "outcome:synthetic:" + CurrentTick + ":" + action + (WaitingFleetEnabled ? ":" + driver.ActorStableId : ""),
                PrimaryOutcomeCode = outcome, 결과분류Code = Simulation행위결과분류Codes.성공,
                SourceReferenceIds = new[] { "source:" + ScenarioDataRevision, driver.OrderStableId },
                BeforeWorldRevision = before, AfterWorldRevision = Revision,
                AppliedWorldTick = CurrentTick, RuleRevision = ScenarioDataRevision,
                SpatialRevision = MartEnabled ? "synthetic-spatial.r3" : WaitingFleetEnabled ? "synthetic-spatial.r2" : "synthetic-spatial.r1", DataRevision = ScenarioDataRevision
            });
        }

        private static double Segment((double x, double z) a, (double x, double z) b)
            => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }
}
