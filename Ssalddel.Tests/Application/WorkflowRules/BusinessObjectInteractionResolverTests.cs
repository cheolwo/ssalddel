using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorkflowRules;

public sealed class BusinessObjectInteractionResolverTests
{
    [Fact]
    public void 객체역할과상태전이가맞으면_분류차이와무관하게허용한다()
    {
        var resolver = CreateResolver(
            new[]
            {
                Definition("object:restaurant-operator", BusinessWorkflowModuleCodes.음식점,
                    FiveElementClassificationCodes.화리),
                Definition("object:restaurant", BusinessWorkflowModuleCodes.음식점,
                    FiveElementClassificationCodes.화리),
                Definition("object:cooking-station", BusinessWorkflowModuleCodes.음식점,
                    FiveElementClassificationCodes.화리),
            },
            Profile("WI-CITY-RESTAURANT-ACCEPT", BusinessWorkflowModuleCodes.음식점,
                FiveElementClassificationCodes.수감),
            new[]
            {
                Binding("WI-CITY-RESTAURANT-ACCEPT", "subject",
                    "object:restaurant-operator", BusinessObjectParticipationKindCodes.행위,
                    FiveElementClassificationCodes.목진, true),
                Binding("WI-CITY-RESTAURANT-ACCEPT", "target",
                    "object:restaurant", BusinessObjectParticipationKindCodes.대상,
                    FiveElementClassificationCodes.토간, true),
                Binding("WI-CITY-RESTAURANT-ACCEPT", "station",
                    "object:cooking-station", BusinessObjectParticipationKindCodes.작용,
                    FiveElementClassificationCodes.금태, true, "Cook"),
            });

        var result = resolver.판정(new BusinessObjectInteractionRequest
        {
            WiId = "WI-CITY-RESTAURANT-ACCEPT",
            WorkflowCode = 업무흐름코드.음식배달,
            CurrentStateCode = 음식배달상태코드.주문대기,
            TargetStateCode = 음식배달상태코드.조리중,
            Subject = Object("actor:restaurant", "object:restaurant-operator"),
            Target = Object("facility:restaurant", "object:restaurant"),
            ToolsOrFacilities = new[]
            {
                Object("facility:cook", "object:cooking-station", "Cook"),
            },
        });

        Assert.True(result.허용여부);
        Assert.Equal(BusinessWorkflowModuleCodes.음식점, result.판정ModuleCode);
        Assert.NotEmpty(result.ClassificationMetadata);
        Assert.False(result.IsExecutionAuthority);
    }

    [Fact]
    public void 필수시설이없으면_Engine호출전에차단한다()
    {
        var engine = new CountingEngine();
        var resolver = CreateResolver(
            new[]
            {
                Definition("object:restaurant-operator", BusinessWorkflowModuleCodes.음식점),
                Definition("object:cooking-station", BusinessWorkflowModuleCodes.음식점),
            },
            Profile("WI-CITY-RESTAURANT-COOK", BusinessWorkflowModuleCodes.음식점),
            new[]
            {
                Binding("WI-CITY-RESTAURANT-COOK", "subject",
                    "object:restaurant-operator", BusinessObjectParticipationKindCodes.행위,
                    "", true),
                Binding("WI-CITY-RESTAURANT-COOK", "station",
                    "object:cooking-station", BusinessObjectParticipationKindCodes.작용,
                    "", true, "Cook"),
            },
            engine);

        var result = resolver.판정(new BusinessObjectInteractionRequest
        {
            WiId = "WI-CITY-RESTAURANT-COOK",
            WorkflowCode = 업무흐름코드.음식배달,
            CurrentStateCode = 음식배달상태코드.조리중,
            TargetStateCode = 음식배달상태코드.픽업대기,
            Subject = Object("actor:restaurant", "object:restaurant-operator"),
        });

        Assert.False(result.허용여부);
        Assert.Contains(BusinessObjectBindingBlockReasonCodes.필수도구시설누락,
            result.차단사유코드목록);
        Assert.Equal(0, engine.CallCount);
    }

    [Fact]
    public void 객체가요청Module을지원하지않으면차단한다()
    {
        var resolver = CreateResolver(
            new[] { Definition("object:courier", BusinessWorkflowModuleCodes.배송) },
            Profile("WI-CITY-SYNTHETIC-ASSIGN", BusinessWorkflowModuleCodes.배차),
            new[]
            {
                Binding("WI-CITY-SYNTHETIC-ASSIGN", "courier", "object:courier",
                    BusinessObjectParticipationKindCodes.행위, "", true),
            });

        var result = resolver.판정(new BusinessObjectInteractionRequest
        {
            WiId = "WI-CITY-SYNTHETIC-ASSIGN",
            WorkflowCode = 업무흐름코드.음식배달,
            CurrentStateCode = 음식배달상태코드.픽업대기,
            TargetStateCode = 음식배달상태코드.기사배정,
            Subject = Object("actor:courier", "object:courier"),
        });

        Assert.False(result.허용여부);
        Assert.Contains(BusinessObjectBindingBlockReasonCodes.객체업무Module불일치,
            result.차단사유코드목록);
    }

    [Fact]
    public void 알수없는Wi는차단하고_분류값으로대체하지않는다()
    {
        var engine = new CountingEngine();
        var resolver = CreateResolver(
            new[] { Definition("object:orderer", BusinessWorkflowModuleCodes.주문) },
            Profile("WI-ORDER-01", BusinessWorkflowModuleCodes.주문),
            new[]
            {
                Binding("WI-ORDER-01", "orderer", "object:orderer",
                    BusinessObjectParticipationKindCodes.행위, "", true),
            },
            engine);

        var result = resolver.판정(new BusinessObjectInteractionRequest
        {
            WiId = "WI-UNKNOWN",
            WorkflowCode = 업무흐름코드.음식배달,
        });

        Assert.False(result.허용여부);
        Assert.Contains(BusinessObjectBindingBlockReasonCodes.InteractionProfile미확인,
            result.차단사유코드목록);
        Assert.Equal(0, engine.CallCount);
    }

    private static BusinessObjectInteractionResolver CreateResolver(
        BusinessObjectDefinition[] definitions,
        BusinessInteractionProfile profile,
        BusinessObjectRoleBinding[] bindings,
        IBusinessWorkflowRuleEngine? engine = null)
        => new(definitions, new[] { profile }, bindings, engine);

    private static BusinessObjectDefinition Definition(
        string id, string module, string classification = "")
        => new()
        {
            ObjectArchetypeStableId = id,
            DisplayName = id,
            MeaningRevision = "object.r1",
            WorkflowModuleCodes = new[] { module },
            SourceStableIds = new[] { "test" },
            ClassificationMetadata = Metadata("ObjectRole", classification),
        };

    private static BusinessInteractionProfile Profile(
        string wiId, string module, string classification = "")
        => new()
        {
            ProfileStableId = "profile:" + wiId,
            WiId = wiId,
            WorkflowCode = module == BusinessWorkflowModuleCodes.창고
                ? 업무흐름코드.창고입고 : 업무흐름코드.음식배달,
            RequestedModuleCode = module,
            ClassificationRevision = "classification.r1",
            SourceStableIds = new[] { "test" },
            ClassificationMetadata = string.IsNullOrWhiteSpace(classification)
                ? Array.Empty<WorkflowClassificationMetadata>()
                : new[] { Metadata("Action", classification)! },
        };

    private static BusinessObjectRoleBinding Binding(
        string wiId, string suffix, string objectId, string kind,
        string classification, bool required, string authority = "")
        => new()
        {
            BindingStableId = "binding:" + wiId + ":" + suffix,
            ProfileStableId = "profile:" + wiId,
            ObjectArchetypeStableId = objectId,
            ParticipationKindCode = kind,
            AuthorityCode = authority,
            ClassificationMetadata = Metadata("BindingExpected", classification),
            Required = required,
        };

    private static WorkflowClassificationMetadata? Metadata(
        string scope, string code)
        => string.IsNullOrWhiteSpace(code) ? null : new()
        {
            SchemeCode = WorkflowClassificationSchemeCodes.FiveElementGwae,
            ScopeCode = scope,
            PrimaryCode = code,
            IsExecutionAuthority = false,
        };

    private static BusinessObjectReference Object(
        string instanceId, string archetypeId, params string[] authorityCodes)
        => new()
        {
            InstanceStableId = instanceId,
            ObjectArchetypeStableId = archetypeId,
            AuthorityCodes = authorityCodes,
        };

    private sealed class CountingEngine : IBusinessWorkflowRuleEngine
    {
        public int CallCount { get; private set; }

        public BusinessWorkflowEngineDescriptor Engine정보조회()
            => BusinessWorkflowRuleEngine.기본.Engine정보조회();

        public BusinessWorkflowModuleDescriptor[] 전체Module조회()
            => BusinessWorkflowRuleEngine.기본.전체Module조회();

        public BusinessWorkflowTransitionDecision 판정(
            BusinessWorkflowTransitionRequest request)
        {
            CallCount++;
            return BusinessWorkflowRuleEngine.기본.판정(request);
        }
    }
}
