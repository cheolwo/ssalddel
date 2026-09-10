using System;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Warehouse
{
    /// <summary>독립 음식점 로컬 예제의 입력. 실제 주문·DB나 권위 상태 사본을 만들지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "음식점 관찰용 명시적 모의 입력을 구성한다.",
        Boundary = "표본 생성은 실제 운영·Scene·플레이 증거가 아니다.")]
    public static class 음식점관찰표본
    {
        public const string ScenarioId = "scenario:restaurant-observer.v1";
        public const string SlotId = "restaurant-observer-r1-primary";
        public const string FacilityId = "facility:sim.restaurant-1";
        public const string ActorId = "actor:sim.restaurant-1";
        public const string OrganizationId = "organization:restaurant";
        private static string[] Sources() => new[] { "source:fixture:restaurant-observer.r1" };

        public static 경영SimulationSession생성Request Create(Guid requestId) => new()
        {
            ClientRequestId = requestId, ScenarioStableId = ScenarioId,
            ScenarioDataRevision = "restaurant-observer.r1", ScenarioSeed = 20260906,
            RuleRevision = "rule:r1", DurationTicks = 100,
            NpcWorkforce = new SimulationNpcWorkforceInitialStateRequest
            {
                Policies = new[] { new SimulationNpcWorkPolicyInitialRequest {
                    PolicyStableId = "policy:restaurant:cooking", OrganizationStableId = OrganizationId,
                    FacilityStableId = FacilityId, ActionCode = SimulationNpcActionCodes.RestaurantCooking,
                    RequiredCapabilityCode = SimulationNpcCapabilityCodes.RestaurantCooking,
                    CookingSlots = 1, WorkDurationTicks = 2, AutomationEnabled = true,
                    InteractionPointKey = "interaction:restaurant:cooking", ActionVisualKey = "visual:restaurant:cooking",
                    SourceStableIds = Sources() } },
                Organizations = new[] { new SimulationNpcOrganizationInitialRequest {
                    OrganizationStableId = OrganizationId, DisplayName = "관찰용 음식점",
                    FacilityStableIds = new[] { FacilityId },
                    AllowedCapabilityCodes = new[] { SimulationNpcCapabilityCodes.RestaurantCooking }, SourceStableIds = Sources() } },
                Actors = new[] { new SimulationNpcActorInitialRequest {
                    ActorStableId = ActorId, OrganizationStableId = OrganizationId, DisplayName = "음식점 NPC",
                    HomeFacilityStableId = FacilityId, ReferenceRoleCode = "RestaurantOperator",
                    AssignableCapabilityCodes = new[] { SimulationNpcCapabilityCodes.RestaurantCooking }, SourceStableIds = Sources() } },
                CapabilityGrants = new[] { new SimulationNpcCapabilityGrantInitialRequest {
                    GrantStableId = "grant:restaurant", OrganizationStableId = OrganizationId,
                    ActorStableId = ActorId, FacilityStableId = FacilityId,
                    CapabilityCode = SimulationNpcCapabilityCodes.RestaurantCooking, GrantedByActorStableId = ActorId,
                    SourceStableIds = Sources() } },
            },
            WorldContext = new SimulationWorldContext생성Request {
                FactionStableId = "faction:sim:restaurant", TerritoryStableId = "territory:sim:restaurant",
                SettlementStableId = "settlement:sim:restaurant",
                GameDateStartsOn = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero) },
        };

        public static Simulation음식배달ConfirmRequest 주문(string commandId, long revision, string orderId) => new()
        {
            CommandId = commandId, ExpectedRevision = revision,
            FoodDelivery = new Simulation음식배달PreviewRequest {
                FoodOrderStableId = orderId, MenuItemStableId = "menu-item:sim.potato-stew-1",
                RestaurantFacilityStableId = FacilityId, DestinationFacilityStableId = "facility:sim.residence-1",
                DeliveryScopeStableId = "delivery-scope:sim.town-1", OrdererStableId = "participant:sim.orderer-1",
                ActorStableId = "participant:sim.orderer-1", Quantity = 1m, UnitCode = "serving",
                PreparationDurationTicks = 2, DeliveryDurationTicks = 2,
                RestaurantPreparationOnly = true, AwaitRestaurantResponse = true, NpcAutoAccept = true,
                SourceStableIds = Sources() },
        };
    }
}
