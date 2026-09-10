using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class BusinessObjectParticipationKindCodes
    {
        public const string 행위 = "Action";
        public const string 대상 = "Target";
        public const string 작용 = "Operation";
        public const string 보조 = "Support";

        public static string[] 전체조회() => new[] { 행위, 대상, 작용, 보조 };
    }

    public static class BusinessObjectBindingBlockReasonCodes
    {
        public const string InteractionProfile미확인 = "BusinessObjectInteractionProfileUnresolved";
        public const string 업무흐름불일치 = "BusinessObjectWorkflowMismatch";
        public const string 주체미확인 = "BusinessObjectSubjectUnresolved";
        public const string 주체행위미결속 = "BusinessObjectSubjectActionBindingMissing";
        public const string 대상미확인 = "BusinessObjectTargetUnresolved";
        public const string 대상결속불일치 = "BusinessObjectTargetBindingMismatch";
        public const string 필수도구시설누락 = "BusinessObjectRequiredToolOrFacilityMissing";
        public const string 도구시설결속불일치 = "BusinessObjectToolOrFacilityBindingMismatch";
        public const string 객체업무Module불일치 = "BusinessObjectWorkflowModuleMismatch";
        public const string Engine판정Module불일치 = "BusinessObjectWorkflowBindingMismatch";
    }

    public sealed class BusinessObjectDefinition
    {
        public string ObjectArchetypeStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string[] WorkflowModuleCodes { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
        public bool IsExecutionAuthority { get; set; }
    }

    public sealed class BusinessInteractionProfile
    {
        public string ProfileStableId { get; set; } = string.Empty;
        public string WiId { get; set; } = string.Empty;
        public string WorkflowCode { get; set; } = string.Empty;
        public string RequestedModuleCode { get; set; } = string.Empty;
        public string ClassificationRevision { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata[] ClassificationMetadata { get; set; } =
            Array.Empty<WorkflowClassificationMetadata>();
        public bool IsExecutionAuthority { get; set; }
    }

    public sealed class BusinessObjectRoleBinding
    {
        public string BindingStableId { get; set; } = string.Empty;
        public string ProfileStableId { get; set; } = string.Empty;
        public string ObjectArchetypeStableId { get; set; } = string.Empty;
        public string ParticipationKindCode { get; set; } = string.Empty;
        public string AuthorityCode { get; set; } = string.Empty;
        public WorkflowClassificationMetadata? ClassificationMetadata { get; set; }
        public bool Required { get; set; }
    }

    public sealed class BusinessObjectReference
    {
        public string InstanceStableId { get; set; } = string.Empty;
        public string ObjectArchetypeStableId { get; set; } = string.Empty;
        public string[] AuthorityCodes { get; set; } = Array.Empty<string>();
    }

    public sealed class BusinessObjectInteractionRequest
    {
        public string WiId { get; set; } = string.Empty;
        public string WorkflowCode { get; set; } = string.Empty;
        public string CurrentStateCode { get; set; } = string.Empty;
        public string TargetStateCode { get; set; } = string.Empty;
        public BusinessObjectReference? Subject { get; set; }
        public BusinessObjectReference? Target { get; set; }
        public BusinessObjectReference[] ToolsOrFacilities { get; set; } =
            Array.Empty<BusinessObjectReference>();
    }

    public sealed class BusinessObjectInteractionDecision
    {
        public bool 허용여부 { get; set; }
        public bool 멱등재시도여부 { get; set; }
        public string ProfileStableId { get; set; } = string.Empty;
        public string WiId { get; set; } = string.Empty;
        public string 판정ModuleCode { get; set; } = string.Empty;
        public string MeaningRevision { get; set; } = string.Empty;
        public string RuleRevision { get; set; } = string.Empty;
        public string ClassificationRevision { get; set; } = string.Empty;
        public string[] ResolvedBindingStableIds { get; set; } = Array.Empty<string>();
        public string[] 차단사유코드목록 { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public WorkflowClassificationMetadata[] ClassificationMetadata { get; set; } =
            Array.Empty<WorkflowClassificationMetadata>();
        public bool IsExecutionAuthority { get; set; }
    }

    public interface IBusinessObjectInteractionResolver
    {
        BusinessObjectInteractionDecision 판정(BusinessObjectInteractionRequest request);
    }
}
