using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorkflowRules;

public sealed class BusinessWorkflowRuleEngineTests
{
    [Fact]
    public void Engine정보는_보편업무이름과_비권위원형분류를분리한다()
    {
        var first = BusinessWorkflowRuleEngine.기본.Engine정보조회();

        Assert.Equal("BusinessWorkflow", first.EngineCode);
        Assert.Equal("business-workflow-engine.r1", first.MeaningRevision);
        Assert.False(first.IsExecutionAuthority);
        Assert.Equal(WorkflowClassificationSchemeCodes.FiveElementGwae,
            first.ClassificationMetadata!.SchemeCode);
        Assert.Equal(FiveElementClassificationCodes.토간,
            first.ClassificationMetadata.PrimaryCode);
        Assert.Contains(FiveElementClassificationCodes.수감,
            first.ClassificationMetadata.SupportCodes);
        Assert.False(first.ClassificationMetadata.IsExecutionAuthority);

        first.ClassificationMetadata.PrimaryCode = FiveElementClassificationCodes.화리;
        var second = BusinessWorkflowRuleEngine.기본.Engine정보조회();
        Assert.Equal(FiveElementClassificationCodes.토간,
            second.ClassificationMetadata!.PrimaryCode);
    }

    [Fact]
    public void 다섯업무Module은_보편업무코드와_선택분류를함께제공한다()
    {
        var modules = BusinessWorkflowRuleEngine.기본.전체Module조회();

        Assert.Collection(modules,
            module => AssertModule(module, BusinessWorkflowModuleCodes.주문,
                FiveElementClassificationCodes.목진, "order-workflow-meaning.r1"),
            module => AssertModule(module, BusinessWorkflowModuleCodes.음식점,
                FiveElementClassificationCodes.화리, "restaurant-workflow-meaning.r1"),
            module => AssertModule(module, BusinessWorkflowModuleCodes.배차,
                FiveElementClassificationCodes.토간, "dispatch-workflow-meaning.r2"),
            module => AssertModule(module, BusinessWorkflowModuleCodes.배송,
                FiveElementClassificationCodes.수감, "delivery-workflow-meaning.r1"),
            module => AssertModule(module, BusinessWorkflowModuleCodes.창고,
                FiveElementClassificationCodes.금태, "warehouse-workflow-meaning.r1"));
    }

    [Theory]
    [InlineData("FoodDelivery", "주문대기", "조리중", "Restaurant")]
    [InlineData("FoodDelivery", "픽업대기", "기사배정", "Dispatch")]
    [InlineData("FoodDelivery", "기사배정", "픽업완료", "Delivery")]
    [InlineData("FreightTransport", "배차대기", "상차지도착", "Delivery")]
    [InlineData("FreightTransport", "상차지도착", "상차완료", "Delivery")]
    [InlineData("WarehouseInbound", "Expected", "PendingInspection", "Warehouse")]
    public void 상태전이는_업무의미로Module을결정한다(
        string workflow, string current, string target, string expectedModule)
    {
        var result = BusinessWorkflowRuleEngine.기본.판정(
            new BusinessWorkflowTransitionRequest
            {
                업무흐름코드 = workflow,
                현재상태코드 = current,
                목표상태코드 = target,
            });

        Assert.True(result.허용여부);
        Assert.Equal(expectedModule, result.판정ModuleCode);
        Assert.NotNull(result.ClassificationMetadata);
    }

    [Fact]
    public void 명시Module과상태의미가다르면_보편오류로차단한다()
    {
        var result = BusinessWorkflowRuleEngine.기본.판정(
            new BusinessWorkflowTransitionRequest
            {
                요청ModuleCode = BusinessWorkflowModuleCodes.배송,
                업무흐름코드 = 업무흐름코드.음식배달,
                현재상태코드 = 음식배달상태코드.주문대기,
                목표상태코드 = 음식배달상태코드.조리중,
            });

        Assert.False(result.허용여부);
        Assert.Equal(BusinessWorkflowModuleCodes.배송, result.판정ModuleCode);
        Assert.Contains(BusinessWorkflowRuleBlockReasonCodes.Module불일치,
            result.차단사유코드목록);
    }

    [Fact]
    public void 분류Metadata가없거나달라도_동일업무판정은같다()
    {
        var without = new BusinessWorkflowRuleEngine(new[]
        {
            new FixedModule(null),
        });
        var withDifferent = new BusinessWorkflowRuleEngine(new[]
        {
            new FixedModule(new WorkflowClassificationMetadata
            {
                SchemeCode = WorkflowClassificationSchemeCodes.FiveElementGwae,
                PrimaryCode = FiveElementClassificationCodes.화리,
            }),
        });
        var request = new BusinessWorkflowTransitionRequest
        {
            요청ModuleCode = "Fixed",
            업무흐름코드 = "FixedWorkflow",
            현재상태코드 = "A",
            목표상태코드 = "B",
        };

        var first = without.판정(request);
        var second = withDifferent.판정(request);

        Assert.Equal(first.허용여부, second.허용여부);
        Assert.Equal(first.판정ModuleCode, second.판정ModuleCode);
        Assert.Empty(first.차단사유코드목록);
        Assert.Empty(second.차단사유코드목록);
    }

    private static void AssertModule(
        BusinessWorkflowModuleDescriptor module,
        string code,
        string classification,
        string revision)
    {
        Assert.Equal(code, module.ModuleCode);
        Assert.Equal(revision, module.MeaningRevision);
        Assert.Equal(classification, module.ClassificationMetadata!.PrimaryCode);
        Assert.False(module.IsExecutionAuthority);
    }

    private sealed class FixedModule : IBusinessWorkflowRuleModule
    {
        public FixedModule(WorkflowClassificationMetadata? metadata)
        {
            Descriptor = new BusinessWorkflowModuleDescriptor
            {
                ModuleCode = "Fixed",
                MeaningRevision = "fixed.r1",
                ClassificationMetadata = metadata,
            };
        }

        public BusinessWorkflowModuleDescriptor Descriptor { get; }

        public bool 처리대상인가(BusinessWorkflowTransitionRequest request)
            => request.업무흐름코드 == "FixedWorkflow";

        public BusinessWorkflowTransitionDecision 판정(
            BusinessWorkflowTransitionRequest request)
            => new()
            {
                허용여부 = true,
                판정ModuleCode = Descriptor.ModuleCode,
                MeaningRevision = Descriptor.MeaningRevision,
                ClassificationMetadata = Descriptor.ClassificationMetadata,
            };
    }
}
