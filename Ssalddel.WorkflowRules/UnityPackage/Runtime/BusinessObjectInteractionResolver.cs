using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WorkflowRules
{
    /// <summary>
    /// 객체 원형과 WI 참여 관계를 확인한 뒤 업무 흐름 규칙 엔진에 위임한다.
    /// 분류 메타데이터는 판정 결과에 동봉할 뿐 허용 여부에는 사용하지 않는다.
    /// </summary>
    public sealed class BusinessObjectInteractionResolver
        : IBusinessObjectInteractionResolver
    {
        private readonly IBusinessWorkflowRuleEngine engine;
        private readonly IReadOnlyDictionary<string, BusinessObjectDefinition> definitions;
        private readonly IReadOnlyDictionary<string, BusinessInteractionProfile> profilesByWi;
        private readonly BusinessObjectRoleBinding[] bindings;

        public BusinessObjectInteractionResolver(
            IEnumerable<BusinessObjectDefinition> definitions,
            IEnumerable<BusinessInteractionProfile> profiles,
            IEnumerable<BusinessObjectRoleBinding> bindings,
            IBusinessWorkflowRuleEngine? engine = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            var definitionArray = definitions.ToArray();
            var profileArray = profiles.ToArray();
            this.bindings = bindings.ToArray();
            this.engine = engine ?? BusinessWorkflowRuleEngine.기본;

            ValidateUnique(definitionArray.Select(value => value.ObjectArchetypeStableId),
                "업무 객체 원형 ID는 비어 있거나 중복될 수 없습니다.");
            ValidateUnique(profileArray.Select(value => value.ProfileStableId),
                "업무 상호작용 Profile ID는 비어 있거나 중복될 수 없습니다.");
            ValidateUnique(profileArray.Select(value => value.WiId),
                "하나의 WI에는 하나의 업무 상호작용 Profile만 결속할 수 있습니다.");
            ValidateUnique(this.bindings.Select(value => value.BindingStableId),
                "업무 객체 결속 ID는 비어 있거나 중복될 수 없습니다.");

            this.definitions = definitionArray.ToDictionary(
                value => value.ObjectArchetypeStableId, StringComparer.Ordinal);
            profilesByWi = profileArray.ToDictionary(value => value.WiId, StringComparer.Ordinal);

            var profileIds = new HashSet<string>(
                profileArray.Select(value => value.ProfileStableId), StringComparer.Ordinal);
            foreach (var definition in definitionArray)
            {
                if (definition.IsExecutionAuthority)
                    throw new ArgumentException(
                        "객체 원형은 실행 권위를 소유할 수 없습니다.", nameof(definitions));
            }

            foreach (var profile in profileArray)
            {
                if (profile.IsExecutionAuthority)
                    throw new ArgumentException(
                        "상호작용 Profile은 실행 권위를 소유할 수 없습니다.", nameof(profiles));
            }

            foreach (var binding in this.bindings)
            {
                if (!profileIds.Contains(binding.ProfileStableId))
                    throw new ArgumentException(
                        "객체 결속이 존재하지 않는 Profile을 참조합니다.", nameof(bindings));
                if (!this.definitions.ContainsKey(binding.ObjectArchetypeStableId))
                    throw new ArgumentException(
                        "객체 결속이 존재하지 않는 원형을 참조합니다.", nameof(bindings));
                if (!BusinessObjectParticipationKindCodes.전체조회()
                    .Contains(binding.ParticipationKindCode))
                    throw new ArgumentException(
                        "지원하지 않는 객체 참여 종류입니다.", nameof(bindings));
            }

            foreach (var profile in profileArray)
            {
                var profileBindings = this.bindings.Where(value => string.Equals(
                    value.ProfileStableId, profile.ProfileStableId,
                    StringComparison.Ordinal)).ToArray();
                if (!profileBindings.Any(value =>
                    value.ParticipationKindCode == BusinessObjectParticipationKindCodes.행위
                    && value.Required))
                    throw new ArgumentException(
                        "상호작용 Profile에는 필수 행위 주체 결속이 필요합니다.",
                        nameof(bindings));
            }
        }

        public BusinessObjectInteractionDecision 판정(
            BusinessObjectInteractionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!profilesByWi.TryGetValue(
                request.WiId?.Trim() ?? string.Empty, out var profile))
                return Blocked(request, null,
                    BusinessObjectBindingBlockReasonCodes.InteractionProfile미확인);
            if (!string.Equals(
                profile.WorkflowCode, request.WorkflowCode?.Trim(),
                StringComparison.Ordinal))
                return Blocked(request, profile,
                    BusinessObjectBindingBlockReasonCodes.업무흐름불일치);

            var profileBindings = bindings.Where(value => string.Equals(
                value.ProfileStableId, profile.ProfileStableId,
                StringComparison.Ordinal)).ToArray();
            var resolvedBindings = new List<BusinessObjectRoleBinding>();
            var resolvedDefinitions = new List<BusinessObjectDefinition>();

            if (!TryResolve(
                request.Subject, profileBindings,
                BusinessObjectParticipationKindCodes.행위,
                out var subjectDefinition, out var subjectBinding))
            {
                var reason = request.Subject == null
                    || string.IsNullOrWhiteSpace(request.Subject.InstanceStableId)
                    ? BusinessObjectBindingBlockReasonCodes.주체미확인
                    : BusinessObjectBindingBlockReasonCodes.주체행위미결속;
                return Blocked(request, profile, reason);
            }
            resolvedDefinitions.Add(subjectDefinition!);
            resolvedBindings.Add(subjectBinding!);

            var requiredTargets = profileBindings.Where(value =>
                value.Required
                && value.ParticipationKindCode == BusinessObjectParticipationKindCodes.대상)
                .ToArray();
            if (request.Target == null && requiredTargets.Length > 0)
                return Blocked(request, profile,
                    BusinessObjectBindingBlockReasonCodes.대상미확인);
            if (request.Target != null)
            {
                if (!TryResolve(
                    request.Target, profileBindings,
                    BusinessObjectParticipationKindCodes.대상,
                    out var targetDefinition, out var targetBinding))
                    return Blocked(request, profile,
                        BusinessObjectBindingBlockReasonCodes.대상결속불일치);
                resolvedDefinitions.Add(targetDefinition!);
                resolvedBindings.Add(targetBinding!);
            }

            var tools = request.ToolsOrFacilities
                ?? Array.Empty<BusinessObjectReference>();
            var toolKinds = new[]
            {
                BusinessObjectParticipationKindCodes.작용,
                BusinessObjectParticipationKindCodes.보조,
            };
            foreach (var required in profileBindings.Where(value =>
                value.Required && toolKinds.Contains(value.ParticipationKindCode)))
            {
                if (!tools.Any(item => Matches(item, required)))
                    return Blocked(request, profile,
                        BusinessObjectBindingBlockReasonCodes.필수도구시설누락);
            }

            foreach (var tool in tools)
            {
                var matched = profileBindings.FirstOrDefault(binding =>
                    toolKinds.Contains(binding.ParticipationKindCode)
                    && Matches(tool, binding));
                if (matched == null
                    || !definitions.TryGetValue(
                        tool.ObjectArchetypeStableId, out var toolDefinition))
                    return Blocked(request, profile,
                        BusinessObjectBindingBlockReasonCodes.도구시설결속불일치);
                resolvedBindings.Add(matched);
                resolvedDefinitions.Add(toolDefinition);
            }

            if (resolvedDefinitions.Any(value =>
                !value.WorkflowModuleCodes.Contains(
                    profile.RequestedModuleCode, StringComparer.Ordinal)))
                return Blocked(request, profile,
                    BusinessObjectBindingBlockReasonCodes.객체업무Module불일치);

            var engineDecision = engine.판정(new BusinessWorkflowTransitionRequest
            {
                요청ModuleCode = profile.RequestedModuleCode,
                업무흐름코드 = request.WorkflowCode ?? string.Empty,
                현재상태코드 = request.CurrentStateCode ?? string.Empty,
                목표상태코드 = request.TargetStateCode ?? string.Empty,
            });
            if (!engineDecision.허용여부)
                return FromEngine(
                    request, profile, resolvedDefinitions,
                    resolvedBindings, engineDecision);
            if (!string.Equals(
                engineDecision.판정ModuleCode,
                profile.RequestedModuleCode,
                StringComparison.Ordinal))
                return Blocked(request, profile,
                    BusinessObjectBindingBlockReasonCodes.Engine판정Module불일치);

            return FromEngine(
                request, profile, resolvedDefinitions,
                resolvedBindings, engineDecision);
        }

        private bool TryResolve(
            BusinessObjectReference? reference,
            IReadOnlyList<BusinessObjectRoleBinding> profileBindings,
            string participationKind,
            out BusinessObjectDefinition? definition,
            out BusinessObjectRoleBinding? binding)
        {
            definition = null;
            binding = null;
            if (reference == null
                || string.IsNullOrWhiteSpace(reference.InstanceStableId)
                || !definitions.TryGetValue(
                    reference.ObjectArchetypeStableId, out definition))
                return false;
            binding = profileBindings.FirstOrDefault(value =>
                value.ParticipationKindCode == participationKind
                && Matches(reference, value));
            return binding != null;
        }

        private static bool Matches(
            BusinessObjectReference reference,
            BusinessObjectRoleBinding binding)
            => string.Equals(
                    reference.ObjectArchetypeStableId,
                    binding.ObjectArchetypeStableId,
                    StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(binding.AuthorityCode)
                    || (reference.AuthorityCodes ?? Array.Empty<string>())
                        .Contains(binding.AuthorityCode, StringComparer.Ordinal));

        private static BusinessObjectInteractionDecision FromEngine(
            BusinessObjectInteractionRequest request,
            BusinessInteractionProfile profile,
            IEnumerable<BusinessObjectDefinition> resolvedDefinitions,
            IEnumerable<BusinessObjectRoleBinding> resolvedBindings,
            BusinessWorkflowTransitionDecision engineDecision)
        {
            var definitions = resolvedDefinitions.ToArray();
            var sources = profile.SourceStableIds
                .Concat(definitions.SelectMany(value => value.SourceStableIds))
                .Concat(engineDecision.SourceStableIds)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var classification = definitions
                .Select(value => value.ClassificationMetadata)
                .Concat(profile.ClassificationMetadata
                    ?? Array.Empty<WorkflowClassificationMetadata>())
                .Append(engineDecision.ClassificationMetadata)
                .Where(value => value != null)
                .Select(value => BusinessWorkflowRuleEngine.Clone(value))
                .Where(value => value != null)
                .Cast<WorkflowClassificationMetadata>()
                .ToArray();

            return new BusinessObjectInteractionDecision
            {
                허용여부 = engineDecision.허용여부,
                멱등재시도여부 = engineDecision.멱등재시도여부,
                ProfileStableId = profile.ProfileStableId,
                WiId = profile.WiId,
                판정ModuleCode = engineDecision.판정ModuleCode,
                MeaningRevision = engineDecision.MeaningRevision,
                RuleRevision = engineDecision.RuleRevision,
                ClassificationRevision = profile.ClassificationRevision,
                ResolvedBindingStableIds = resolvedBindings
                    .Select(value => value.BindingStableId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                차단사유코드목록 = engineDecision.차단사유코드목록.ToArray(),
                SourceStableIds = sources,
                ClassificationMetadata = classification,
                IsExecutionAuthority = false,
            };
        }

        private static BusinessObjectInteractionDecision Blocked(
            BusinessObjectInteractionRequest request,
            BusinessInteractionProfile? profile,
            params string[] reasons)
            => new BusinessObjectInteractionDecision
            {
                WiId = request.WiId ?? string.Empty,
                ProfileStableId = profile?.ProfileStableId ?? string.Empty,
                판정ModuleCode = profile?.RequestedModuleCode ?? string.Empty,
                ClassificationRevision = profile?.ClassificationRevision ?? string.Empty,
                차단사유코드목록 = reasons,
                SourceStableIds = profile?.SourceStableIds.ToArray()
                    ?? Array.Empty<string>(),
                ClassificationMetadata = (profile?.ClassificationMetadata
                        ?? Array.Empty<WorkflowClassificationMetadata>())
                    .Select(BusinessWorkflowRuleEngine.Clone)
                    .Where(value => value != null)
                    .Cast<WorkflowClassificationMetadata>()
                    .ToArray(),
                IsExecutionAuthority = false,
            };

        private static void ValidateUnique(
            IEnumerable<string> values,
            string message)
        {
            var array = values.ToArray();
            if (array.Any(string.IsNullOrWhiteSpace)
                || array.Distinct(StringComparer.Ordinal).Count() != array.Length)
                throw new ArgumentException(message);
        }
    }
}
