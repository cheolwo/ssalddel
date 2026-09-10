using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class BusinessWorkflowModuleCodes
    {
        public const string 주문 = "Order";
        public const string 음식점 = "Restaurant";
        public const string 배차 = "Dispatch";
        public const string 배송 = "Delivery";
        public const string 창고 = "Warehouse";

        public static string[] 전체조회() => new[]
        {
            주문,
            음식점,
            배차,
            배송,
            창고,
        };
    }

    public static class WorkflowClassificationSchemeCodes
    {
        public const string FiveElementGwae = "FiveElementGwae";
    }

    /// <summary>
    /// 업무 실행과 분리된 설명용 분류 메타데이터다.
    /// 이 값은 모듈 선택, 상태 전이, 저장 또는 실행 권위를 바꾸지 않는다.
    /// </summary>
    public sealed class WorkflowClassificationMetadata
    {
        public string SchemeCode { get; set; } = string.Empty;
        public string ScopeCode { get; set; } = string.Empty;
        public string PrimaryCode { get; set; } = string.Empty;
        public string[] SupportCodes { get; set; } = Array.Empty<string>();
        public string ElementCode { get; set; } = string.Empty;
        public string DisplayToken { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public bool IsExecutionAuthority { get; set; }
    }

    public static class FiveElementClassificationCodes
    {
        public const string 목진 = "JIN";
        public const string 화리 = "RI";
        public const string 토간 = "GAN";
        public const string 수감 = "GAM";
        public const string 금태 = "TAE";
    }

    public static class BusinessWorkflowRuleBlockReasonCodes
    {
        public const string Module미확인 = "BusinessWorkflowModuleUnresolved";
        public const string Module불일치 = "BusinessWorkflowModuleMismatch";
    }

    public sealed class BusinessWorkflowModuleDescriptor
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string[] WorkflowCodes { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
        public bool IsExecutionAuthority { get; set; }
    }

    public sealed class BusinessWorkflowEngineDescriptor
    {
        public string EngineCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
        public bool IsExecutionAuthority { get; set; }
    }

    public sealed class BusinessWorkflowTransitionRequest
    {
        public string 요청ModuleCode { get; set; } = string.Empty;
        public string 업무흐름코드 { get; set; } = string.Empty;
        public string 현재상태코드 { get; set; } = string.Empty;
        public string 목표상태코드 { get; set; } = string.Empty;
    }

    public sealed class BusinessWorkflowTransitionDecision
    {
        public bool 허용여부 { get; set; }
        public bool 멱등재시도여부 { get; set; }
        public string 판정ModuleCode { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string RuleRevision { get; set; } = string.Empty;
        public string[] 차단사유코드목록 { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
    }

    public interface IBusinessWorkflowRuleModule
    {
        BusinessWorkflowModuleDescriptor Descriptor { get; }
        bool 처리대상인가(BusinessWorkflowTransitionRequest request);
        BusinessWorkflowTransitionDecision 판정(BusinessWorkflowTransitionRequest request);
    }

    public interface IBusinessWorkflowRuleEngine
    {
        BusinessWorkflowEngineDescriptor Engine정보조회();
        BusinessWorkflowModuleDescriptor[] 전체Module조회();
        BusinessWorkflowTransitionDecision 판정(BusinessWorkflowTransitionRequest request);
    }
}
