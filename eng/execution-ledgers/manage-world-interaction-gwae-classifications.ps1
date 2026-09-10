param(
    [ValidateSet('Write', 'Check', 'Validate')]
    [string] $Mode = 'Validate',
    [string] $InputPath = 'eng/execution-ledgers/world-interaction-gwae-classifications.json',
    [string] $WorkflowLineagePath = 'eng/work-areas/simulation-unity.json',
    [string] $JsonOutputPath = 'docs/AI/generated/world-interaction-gwae-classifications.json',
    [string] $MarkdownOutputPath = 'docs/AI/generated/world-interaction-gwae-classifications.md'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Resolve-RepositoryPath([string] $Path) {
    return Join-Path $repositoryRoot $Path
}

function Require([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Json([string] $Path) {
    $fullPath = Resolve-RepositoryPath $Path
    Require (Test-Path -LiteralPath $fullPath) "MissingFile:$Path"
    return Get-Content -Raw -Encoding UTF8 -LiteralPath $fullPath | ConvertFrom-Json
}

function Has-Property($Object, [string] $Name) {
    return $null -ne $Object.PSObject.Properties[$Name]
}

function Get-OptionalValue($Override, $Default, [string] $Name) {
    if ($null -ne $Override -and (Has-Property $Override $Name)) {
        return [string] $Override.$Name
    }
    if ($null -ne $Default -and (Has-Property $Default $Name)) {
        return [string] $Default.$Name
    }
    return $null
}

function Get-OptionalArray($Override, $Default, [string] $Name) {
    if ($null -ne $Override -and (Has-Property $Override $Name)) {
        return @($Override.$Name | ForEach-Object { [string] $_ })
    }
    if ($null -ne $Default -and (Has-Property $Default $Name)) {
        return @($Default.$Name | ForEach-Object { [string] $_ })
    }
    return @()
}

function ConvertTo-CanonicalJson($Value) {
    return ($Value | ConvertTo-Json -Depth 20) + "`n"
}

function Write-Utf8NoBom([string] $Path, [string] $Content) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

$source = Read-Json $InputPath
Require ([string] $source.schemaVersion -eq 'mirror-world-interaction-gwae-classifications.v3') 'SchemaInvalid'
$wiCatalog = Read-Json ([string] $source.worldInteractionCatalogPath)
Require ([string] $wiCatalog.schemaVersion -eq '5') 'WorldInteractionCatalogSchemaInvalid'
$workflowLineage = Read-Json $WorkflowLineagePath
$workflowLineageFeatures = @($workflowLineage.features | Where-Object { [string] $_.key -eq 'food-workflow-lineage' })
Require ($workflowLineageFeatures.Count -eq 1) 'FoodWorkflowLineageFeatureMissingOrDuplicate'
$workflowLineageFeature = $workflowLineageFeatures[0]
$workflowLineageStepKeys = @($workflowLineageFeature.requiredStepKeys | ForEach-Object { [string] $_ })
Require ($workflowLineageStepKeys.Count -gt 0) 'FoodWorkflowLineageStepsMissing'
Require (@($workflowLineageStepKeys | Sort-Object -Unique).Count -eq $workflowLineageStepKeys.Count) 'FoodWorkflowLineageStepsDuplicate'

$e5GatePolicy = $source.e5RoleObjectActionGatePolicy
Require ($null -ne $e5GatePolicy) 'E5RoleObjectActionGatePolicyMissing'
Require ([string] $e5GatePolicy.requiredFromEvidenceStageCode -eq 'E5') 'E5RoleObjectActionGateStageInvalid'
Require ([string] $e5GatePolicy.applicabilityRuleCode -eq 'AnyWorldObjectRoleOwningOrCausingAuthorityStateTransition') 'E5RoleObjectActionApplicabilityInvalid'
Require ([string] $e5GatePolicy.worldObjectScopeCode -eq 'AllWorldObjects') 'E5RoleObjectScopeInvalid'
Require ([string] $e5GatePolicy.defaultRoleObjectBindingModeCode -eq 'ResolveFromAuthorityExecutionContext') 'E5RoleObjectBindingModeInvalid'
foreach ($requiredRoleObjectKindCode in @('PlayerActor', 'NpcActor', 'Vehicle', 'Equipment', 'Facility', 'NaturalObject', 'Environment', 'WorldRule', 'ResolvedExecutionObject')) {
    Require (@($e5GatePolicy.allowedRoleObjectKindCodes) -contains $requiredRoleObjectKindCode) "E5RoleObjectKindMissing:$requiredRoleObjectKindCode"
}
foreach ($applicabilityCode in @('Required', 'NotApplicable')) {
    Require (@($e5GatePolicy.allowedApplicabilityCodes) -contains $applicabilityCode) "E5RoleObjectApplicabilityMissing:$applicabilityCode"
}
Require ((@($e5GatePolicy.requiredTransitionFields) -join ',') -eq 'actionCode,startStateCodes,completionStateCodes,effectCodes') 'E5RoleObjectTransitionFieldsInvalid'
foreach ($requiredTextProperty in @('passiveObjectRule', 'notApplicableRule', 'promotionBoundary')) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $e5GatePolicy.$requiredTextProperty)) "E5RoleObjectPolicyTextMissing:$requiredTextProperty"
}

$definitionByCode = @{}
foreach ($definition in @($source.gwaeDefinitions)) {
    $code = [string] $definition.code
    Require (-not [string]::IsNullOrWhiteSpace($code)) 'GwaeCodeMissing'
    Require (-not $definitionByCode.ContainsKey($code)) "DuplicateGwaeCode:$code"
    Require ([string] $definition.symbol -in @('☳', '☱', '☲', '☵', '☶')) "GwaeSymbolInvalid:$code"
    Require ([string] $definition.element -in @('금', '목', '수', '화', '토')) "GwaeElementInvalid:$code"
    $definitionByCode[$code] = $definition
}
Require ($definitionByCode.Count -eq 5) 'GwaeDefinitionCountInvalid'

$relationByCode = @{}
foreach ($relation in @($source.elementRelationDefinitions)) {
    $relationCode = [string] $relation.code
    Require ($relationCode -in @('SHENG', 'KE')) "ElementRelationCodeInvalid:$relationCode"
    Require (-not $relationByCode.ContainsKey($relationCode)) "DuplicateElementRelationCode:$relationCode"
    Require (@($relation.sequence).Count -eq 5) "ElementRelationSequenceInvalid:$relationCode"
    $relationByCode[$relationCode] = $relation
}
Require ($relationByCode.Count -eq 2) 'ElementRelationDefinitionCountInvalid'

$engineCompositionProfile = $source.engineCompositionProfile
Require ($null -ne $engineCompositionProfile) 'EngineCompositionProfileMissing'
foreach ($requiredTextProperty in @('engineCode', 'meaningRevision', 'displayName', 'stablePurpose')) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $engineCompositionProfile.$requiredTextProperty)) "EngineCompositionProfileTextMissing:$requiredTextProperty"
}
Require ([string] $engineCompositionProfile.engineCode -eq 'FiveElementWorkflow') 'EngineCompositionCodeInvalid'
$enginePrimaryGwae = [string] $engineCompositionProfile.primaryGwae
$engineSupportGwae = [string] $engineCompositionProfile.supportGwae
Require ($definitionByCode.ContainsKey($enginePrimaryGwae)) "EngineCompositionPrimaryGwaeInvalid:$enginePrimaryGwae"
Require ($definitionByCode.ContainsKey($engineSupportGwae)) "EngineCompositionSupportGwaeInvalid:$engineSupportGwae"
Require ($enginePrimaryGwae -ne $engineSupportGwae) 'EngineCompositionGwaeRolesMustDiffer'
Require ((Has-Property $engineCompositionProfile 'isExecutionAuthority') -and $engineCompositionProfile.isExecutionAuthority -is [bool]) 'EngineCompositionAuthorityFlagInvalid'
Require (-not [bool] $engineCompositionProfile.isExecutionAuthority) 'EngineCompositionCannotOwnExecution'
$engineSourceStableIds = @($engineCompositionProfile.sourceStableIds | ForEach-Object { [string] $_ })
Require ($engineSourceStableIds.Count -gt 0) 'EngineCompositionSourceStableIdsMissing'
Require (@($engineSourceStableIds | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) 'EngineCompositionSourceStableIdEmpty'
Require (@($engineSourceStableIds | Sort-Object -Unique).Count -eq $engineSourceStableIds.Count) 'EngineCompositionSourceStableIdsDuplicate'

$workflowMeaningProfileIds = @{}
$workflowMeaningStepOwners = @{}
$workflowMeaningProfiles = [System.Collections.Generic.List[object]]::new()
foreach ($profile in @($source.workflowMeaningProfiles)) {
    $profileId = [string] $profile.profileId
    Require (-not [string]::IsNullOrWhiteSpace($profileId)) 'WorkflowMeaningProfileIdMissing'
    Require (-not $workflowMeaningProfileIds.ContainsKey($profileId)) "WorkflowMeaningProfileDuplicate:$profileId"
    $workflowMeaningProfileIds[$profileId] = $true
    foreach ($requiredTextProperty in @('meaningRevision', 'displayName', 'stablePurpose')) {
        Require (-not [string]::IsNullOrWhiteSpace([string] $profile.$requiredTextProperty)) "WorkflowMeaningProfileTextMissing:${profileId}:$requiredTextProperty"
    }
    $primaryGwae = [string] $profile.primaryGwae
    Require ($definitionByCode.ContainsKey($primaryGwae)) "WorkflowMeaningProfileGwaeInvalid:${profileId}:$primaryGwae"
    Require ((Has-Property $profile 'isExecutionAuthority') -and $profile.isExecutionAuthority -is [bool]) "WorkflowMeaningAuthorityFlagInvalid:$profileId"
    Require (-not [bool] $profile.isExecutionAuthority) "WorkflowMeaningProfileCannotOwnExecution:$profileId"
    $codeStepKeys = @($profile.codeStepKeys | ForEach-Object { [string] $_ })
    Require ($codeStepKeys.Count -gt 0) "WorkflowMeaningProfileStepsMissing:$profileId"
    Require (@($codeStepKeys | Sort-Object -Unique).Count -eq $codeStepKeys.Count) "WorkflowMeaningProfileStepsDuplicate:$profileId"
    foreach ($stepKey in $codeStepKeys) {
        Require ($workflowLineageStepKeys -contains $stepKey) "WorkflowMeaningProfileStepUnknown:${profileId}:$stepKey"
        Require (-not $workflowMeaningStepOwners.ContainsKey($stepKey)) "WorkflowMeaningProfileStepClaimedTwice:${profileId}:$stepKey"
        $workflowMeaningStepOwners[$stepKey] = $profileId
    }
    $workflowMeaningProfiles.Add($profile)
}
Require ($workflowMeaningProfiles.Count -eq 5) 'WorkflowMeaningProfileCountInvalid'

$overrideByWiId = @{}
foreach ($override in @($source.overrides)) {
    $wiId = [string] $override.wiId
    Require (-not [string]::IsNullOrWhiteSpace($wiId)) 'OverrideWiIdMissing'
    Require (-not $overrideByWiId.ContainsKey($wiId)) "DuplicateOverride:$wiId"
    $overrideByWiId[$wiId] = $override
}

$wiIds = @($wiCatalog.items | ForEach-Object { [string] $_.id })
foreach ($overrideId in $overrideByWiId.Keys) {
    Require ($overrideId -in $wiIds) "UnknownOverrideWiId:$overrideId"
}

$resolvedItems = [System.Collections.Generic.List[object]]::new()
foreach ($wi in @($wiCatalog.items)) {
    $groupCode = [string] $wi.groupCode
    $groupDefaultProperty = $source.groupDefaults.PSObject.Properties[$groupCode]
    Require ($null -ne $groupDefaultProperty) "GroupDefaultMissing:$groupCode"
    $groupDefault = $groupDefaultProperty.Value
    $override = if ($overrideByWiId.ContainsKey([string] $wi.id)) { $overrideByWiId[[string] $wi.id] } else { $null }

    $actionGwae = Get-OptionalValue $override $groupDefault 'actionGwae'
    $operationGwae = Get-OptionalValue $override $groupDefault 'operationGwae'
    $targetGwae = Get-OptionalValue $override $groupDefault 'targetGwae'
    $supportGwae = Get-OptionalValue $override $groupDefault 'supportGwae'
    $targetMode = Get-OptionalValue $override $groupDefault 'targetMode'
    if ([string]::IsNullOrWhiteSpace($targetMode)) { $targetMode = 'Fixed' }
    $operationMode = Get-OptionalValue $override $groupDefault 'operationMode'
    if ([string]::IsNullOrWhiteSpace($operationMode)) {
        $operationMode = if ([string]::IsNullOrWhiteSpace($operationGwae)) { 'None' } else { 'Fixed' }
    }
    $supportMode = Get-OptionalValue $override $groupDefault 'supportMode'
    if ([string]::IsNullOrWhiteSpace($supportMode)) {
        $supportMode = if ([string]::IsNullOrWhiteSpace($supportGwae)) { 'None' } else { 'Fixed' }
    }
    $additionalTargetGwae = Get-OptionalArray $override $groupDefault 'additionalTargetGwae'
    $operationGwaeByActionCode = if ($null -ne $override -and (Has-Property $override 'operationGwaeByActionCode')) { $override.operationGwaeByActionCode } else { $null }
    $elementRelations = if ($null -ne $override -and (Has-Property $override 'elementRelations')) { @($override.elementRelations) } else { @() }

    Require ($definitionByCode.ContainsKey($actionGwae)) "ActionGwaeInvalid:$($wi.id):$actionGwae"
    Require ($targetMode -in @('Fixed', 'InheritFromTargetObject', 'InheritFromActiveInteraction')) "TargetModeInvalid:$($wi.id):$targetMode"
    Require ($operationMode -in @('None', 'Fixed', 'InheritFromTargetObject', 'ByActionCode')) "OperationModeInvalid:$($wi.id):$operationMode"
    Require ($supportMode -in @('None', 'Fixed', 'Contextual')) "SupportModeInvalid:$($wi.id):$supportMode"
    if ($targetMode -eq 'Fixed') {
        Require ($definitionByCode.ContainsKey($targetGwae)) "TargetGwaeInvalid:$($wi.id):$targetGwae"
    }
    if ($operationMode -eq 'Fixed') {
        Require ($definitionByCode.ContainsKey($operationGwae)) "OperationGwaeInvalid:$($wi.id):$operationGwae"
    }
    if ($operationMode -eq 'ByActionCode') {
        Require ($null -ne $operationGwaeByActionCode) "OperationGwaeByActionCodeMissing:$($wi.id)"
        foreach ($property in $operationGwaeByActionCode.PSObject.Properties) {
            Require ($definitionByCode.ContainsKey([string] $property.Value)) "OperationGwaeByActionCodeInvalid:$($wi.id):$($property.Name)"
        }
    }
    foreach ($optionalCode in @($operationGwae, $supportGwae)) {
        if (-not [string]::IsNullOrWhiteSpace($optionalCode)) {
            Require ($definitionByCode.ContainsKey($optionalCode)) "OptionalGwaeInvalid:$($wi.id):$optionalCode"
        }
    }
    foreach ($additionalCode in $additionalTargetGwae) {
        Require ($definitionByCode.ContainsKey($additionalCode)) "AdditionalTargetGwaeInvalid:$($wi.id):$additionalCode"
    }
    foreach ($elementRelation in $elementRelations) {
        Require ($relationByCode.ContainsKey([string] $elementRelation.relationCode)) "ElementRelationTypeInvalid:$($wi.id)"
        Require ($definitionByCode.ContainsKey([string] $elementRelation.sourceGwae)) "ElementRelationSourceInvalid:$($wi.id)"
        Require ($definitionByCode.ContainsKey([string] $elementRelation.targetGwae)) "ElementRelationTargetInvalid:$($wi.id)"
        $expectedPair = "{0}>{1}" -f [string] $elementRelation.sourceGwae, [string] $elementRelation.targetGwae
        Require (@($relationByCode[[string] $elementRelation.relationCode].sequence) -contains $expectedPair) "ElementRelationPairInvalid:$($wi.id):$expectedPair"
        Require (-not [string]::IsNullOrWhiteSpace([string] $elementRelation.displayName)) "ElementRelationDisplayNameMissing:$($wi.id)"
        Require ([string] $elementRelation.applicationMode -in @('RequiredInput', 'RequiredConstraint', 'ConditionalCare', 'Outcome')) "ElementRelationApplicationModeInvalid:$($wi.id)"
        Require (-not [string]::IsNullOrWhiteSpace([string] $elementRelation.functionalMeaning)) "ElementRelationMeaningMissing:$($wi.id)"
    }

    if ($operationMode -ne 'Fixed') { $operationGwae = $null }
    if ($targetMode -ne 'Fixed') { $targetGwae = $null }

    $subjectKind = switch ([string] $wi.controlPolicyCode) {
        'PlayerDirect' { 'Player' }
        'NpcRoutine' { 'Npc' }
        'WorldAutomatic' { 'World' }
        default { 'Variable' }
    }
    $roleObjectKindCode = switch ($subjectKind) {
        'Player' { 'PlayerActor' }
        'Npc' { 'NpcActor' }
        'World' { 'WorldRule' }
        default { 'ResolvedExecutionObject' }
    }
    Require (-not [string]::IsNullOrWhiteSpace([string] $wi.actionCode)) "E5RoleActionCodeMissing:$($wi.id)"
    Require (@($wi.startStateCodes).Count -gt 0) "E5RoleStartStateMissing:$($wi.id)"
    Require (@($wi.completionStateCodes).Count -gt 0) "E5RoleCompletionStateMissing:$($wi.id)"
    Require (@($wi.effectCodes).Count -gt 0) "E5RoleEffectMissing:$($wi.id)"

    $resolvedItems.Add([ordered]@{
        wiId = [string] $wi.id
        groupCode = $groupCode
        title = [string] $wi.title
        subjectKind = $subjectKind
        actionGwae = $actionGwae
        operationMode = $operationMode
        operationGwae = $operationGwae
        operationGwaeByActionCode = $operationGwaeByActionCode
        targetMode = $targetMode
        targetGwae = $targetGwae
        additionalTargetGwae = $additionalTargetGwae
        supportMode = $supportMode
        supportGwae = $supportGwae
        elementRelations = @($elementRelations)
        e5RoleObjectActionGate = [ordered]@{
            applicabilityCode = 'Required'
            worldObjectScopeCode = [string] $e5GatePolicy.worldObjectScopeCode
            roleObjectKindCode = $roleObjectKindCode
            roleObjectBindingModeCode = [string] $e5GatePolicy.defaultRoleObjectBindingModeCode
            actionCode = [string] $wi.actionCode
            startStateCodes = @($wi.startStateCodes | ForEach-Object { [string] $_ })
            completionStateCodes = @($wi.completionStateCodes | ForEach-Object { [string] $_ })
            effectCodes = @($wi.effectCodes | ForEach-Object { [string] $_ })
            actorRequirementRefs = @($wi.actorRequirements | ForEach-Object { [string] $_ })
            notApplicableReason = ''
        }
        classificationStatus = if ($null -ne $override) { 'ReviewedExplicit' } else { 'ReviewedByMeaningRule' }
        reviewRule = Get-OptionalValue $override $groupDefault 'reviewRule'
        reason = if ($null -ne $override) { [string] $override.reason } else { "검토된 영역 의미 규칙 '$([string] $groupDefault.reviewRule)'을 적용했다." }
    })
}

Require ($resolvedItems.Count -eq $wiIds.Count) 'ResolvedCountMismatch'
Require (@($resolvedItems.wiId | Sort-Object -Unique).Count -eq $wiIds.Count) 'ResolvedWiIdDuplicate'

$wiById = @{}
foreach ($wi in @($wiCatalog.items)) { $wiById[[string] $wi.id] = $wi }
$profileIds = @{}
$roleObjectActionProfiles = [System.Collections.Generic.List[object]]::new()
foreach ($profile in @($source.roleObjectActionProfiles)) {
    $profileId = [string] $profile.profileId
    Require (-not [string]::IsNullOrWhiteSpace($profileId)) 'RoleObjectActionProfileIdMissing'
    Require (-not $profileIds.ContainsKey($profileId)) "RoleObjectActionProfileDuplicate:$profileId"
    $profileIds[$profileId] = $true
    Require ([string] $profile.e5ReadinessCode -in @('Ready', 'Conditional', 'Blocked')) "RoleObjectActionProfileReadinessInvalid:$profileId"
    Require (@($profile.steps).Count -gt 0) "RoleObjectActionProfileStepsMissing:$profileId"
    $expectedSequence = 0
    foreach ($step in @($profile.steps)) {
        $expectedSequence++
        Require ([int] $step.sequence -eq $expectedSequence) "RoleObjectActionProfileSequenceInvalid:${profileId}:$expectedSequence"
        $stepWiId = [string] $step.wiId
        Require ($wiById.ContainsKey($stepWiId)) "RoleObjectActionProfileWiUnknown:${profileId}:$stepWiId"
        Require (@($e5GatePolicy.allowedRoleObjectKindCodes) -contains [string] $step.roleObjectKindCode) "RoleObjectActionProfileRoleObjectKindInvalid:${profileId}:$expectedSequence"
        Require (-not [string]::IsNullOrWhiteSpace([string] $step.roleCode)) "RoleObjectActionProfileRoleMissing:${profileId}:$expectedSequence"
        Require ([string] $step.bindingKindCode -in @('Action', 'ResultState', 'Transition', 'Gap')) "RoleObjectActionProfileBindingKindInvalid:${profileId}:$expectedSequence"
        Require ($definitionByCode.ContainsKey([string] $step.gwaeCode)) "RoleObjectActionProfileGwaeInvalid:${profileId}:$expectedSequence"
        $authorityCode = [string] $step.authorityCode
        if ([string] $step.bindingKindCode -eq 'Gap') {
            Require ([string]::IsNullOrWhiteSpace($authorityCode)) "RoleObjectActionProfileGapAuthorityInvented:${profileId}:$expectedSequence"
            Require (-not [string]::IsNullOrWhiteSpace([string] $step.openGapRef)) "RoleObjectActionProfileGapRefMissing:${profileId}:$expectedSequence"
        }
        else {
            Require (-not [string]::IsNullOrWhiteSpace($authorityCode)) "RoleObjectActionProfileAuthorityMissing:${profileId}:$expectedSequence"
            $stepWi = $wiById[$stepWiId]
            $knownAuthorityCodes = @([string] $stepWi.actionCode) + @($stepWi.startStateCodes | ForEach-Object { [string] $_ }) + @($stepWi.completionStateCodes | ForEach-Object { [string] $_ }) + @($stepWi.effectCodes | ForEach-Object { [string] $_ })
            Require ($knownAuthorityCodes -contains $authorityCode) "RoleObjectActionProfileAuthorityUnknown:${profileId}:${expectedSequence}:$authorityCode"
        }
    }
    Require (-not [string]::IsNullOrWhiteSpace([string] $profile.boundary)) "RoleObjectActionProfileBoundaryMissing:$profileId"
    $roleObjectActionProfiles.Add($profile)
}
Require ($roleObjectActionProfiles.Count -gt 0) 'RoleObjectActionProfilesMissing'

$output = [ordered]@{
    schemaVersion = 'mirror-world-interaction-gwae-classification-output.v3'
    sourceRevision = [string] $source.revision
    worldInteractionCatalogRevision = [string] $wiCatalog.revision
    workflowLineagePath = $WorkflowLineagePath
    workflowLineageFeatureKey = [string] $workflowLineageFeature.key
    classificationMeaning = [string] $source.classificationMeaning
    displayFormat = [string] $source.displayFormat
    e5RoleObjectActionGatePolicy = $e5GatePolicy
    gwaeDefinitions = @($source.gwaeDefinitions)
    elementRelationDefinitions = @($source.elementRelationDefinitions)
    engineCompositionProfile = $engineCompositionProfile
    counts = [ordered]@{
        total = $resolvedItems.Count
        reviewedExplicit = @($resolvedItems | Where-Object classificationStatus -eq 'ReviewedExplicit').Count
        reviewedByMeaningRule = @($resolvedItems | Where-Object classificationStatus -eq 'ReviewedByMeaningRule').Count
        e5RoleObjectActionRequired = @($resolvedItems | Where-Object { $_.e5RoleObjectActionGate.applicabilityCode -eq 'Required' }).Count
        roleObjectActionProfiles = $roleObjectActionProfiles.Count
        workflowMeaningProfiles = $workflowMeaningProfiles.Count
        engineCompositionProfiles = 1
    }
    workflowMeaningProfiles = $workflowMeaningProfiles
    items = $resolvedItems
    roleObjectActionProfiles = $roleObjectActionProfiles
}
$json = ConvertTo-CanonicalJson $output

$lines = [System.Collections.Generic.List[string]]::new()
function Format-Gwae([string] $Code) {
    if ([string]::IsNullOrWhiteSpace($Code)) { return '-' }
    $definition = $definitionByCode[$Code]
    return "$($definition.element)($($definition.name))"
}
$lines.Add('# WI 괘성 분류 목록')
$lines.Add('')
$lines.Add("- 분류 입력 판본: ``$($source.revision)``")
$lines.Add("- WI 대장 판본: ``$($wiCatalog.revision)``")
$lines.Add("- 전체: $($resolvedItems.Count), 개별 의미 명시 검토: $($output.counts.reviewedExplicit), 검토된 영역 의미 규칙 적용: $($output.counts.reviewedByMeaningRule)")
$lines.Add("- E5 역할 객체·행위 정의 필수: $($output.counts.e5RoleObjectActionRequired), 역할 객체 표본 프로필: $($output.counts.roleObjectActionProfiles)")
$lines.Add("- 업무 의미 대표 괘 프로필: $($output.counts.workflowMeaningProfiles) (코드 계보: ``$($workflowLineageFeature.key)``)")
$lines.Add("- 오행 업무 엔진 조립 프로필: $($output.counts.engineCompositionProfiles), 주축 $(Format-Gwae $enginePrimaryGwae), 보조 $(Format-Gwae $engineSupportGwae)")
$lines.Add('- 권위 상태 변화를 소유하거나 일으키는 모든 세계 객체의 역할·행위 정의는 E5 진입 필수 조건이다. 이 목록만으로 E5를 자동 승격하지 않는다.')
$lines.Add('')
$lines.Add('## 오행 업무 엔진 조립 의미')
$lines.Add('')
$lines.Add('| 엔진 | 의미 판본 | 주 괘 | 보조 괘 | 안정 목적 | 실행 권위 |')
$lines.Add('| --- | --- | --- | --- | --- | --- |')
$lines.Add("| ``$($engineCompositionProfile.engineCode)`` | ``$($engineCompositionProfile.meaningRevision)`` | $(Format-Gwae $enginePrimaryGwae) | $(Format-Gwae $engineSupportGwae) | $($engineCompositionProfile.stablePurpose) | ``$($engineCompositionProfile.isExecutionAuthority)`` |")
$lines.Add('')
$lines.Add('> 엔진 조립 괘는 모듈 경계와 상태 사본 흐름을 설명하는 비권위 메타데이터다. 개별 전이의 모듈·대표 괘나 실행 권위를 바꾸지 않는다.')
$lines.Add('')
$lines.Add('## 업무 의미 대표 괘')
$lines.Add('')
$lines.Add('| 프로필 | 의미 판본 | 업무 | 대표 괘 | 코드 계보 | 안정 목적 | 실행 권위 |')
$lines.Add('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($profile in $workflowMeaningProfiles) {
    $codeStepDisplay = @($profile.codeStepKeys | ForEach-Object { "``$_``" }) -join '<br>'
    $lines.Add("| ``$($profile.profileId)`` | ``$($profile.meaningRevision)`` | $($profile.displayName) | $(Format-Gwae ([string] $profile.primaryGwae)) | $codeStepDisplay | $($profile.stablePurpose) | ``$($profile.isExecutionAuthority)`` |")
}
$lines.Add('')
$lines.Add('> 업무 의미 대표 괘는 변하지 않는 역할 의미와 코드 계보를 찾기 위한 비권위 메타데이터다. 원자 WI의 행위·작용·대상 괘를 덮어쓰지 않으며 상생·상극은 명령 라우팅이나 권한 부여에 사용하지 않는다.')
$lines.Add('')
$lines.Add('| WI | 제목 | E5 역할 객체 | 권위 행위·전환 | 행위괘 | 작용괘 | 대상괘 | 보조괘 | 오행 관계 | 상태 |')
$lines.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |')
foreach ($item in $resolvedItems) {
    $operationDisplay = switch ($item.operationMode) {
        'ByActionCode' { '작업 코드별' }
        'InheritFromTargetObject' { '대상 객체 승계' }
        default { Format-Gwae $item.operationGwae }
    }
    $targetDisplay = switch ($item.targetMode) {
        'InheritFromTargetObject' { '대상 객체 승계' }
        'InheritFromActiveInteraction' { '진행 WI 대상 승계' }
        default { Format-Gwae $item.targetGwae }
    }
    $supportDisplay = if ($item.supportMode -eq 'Contextual') { "상황별 $(Format-Gwae $item.supportGwae)" } else { Format-Gwae $item.supportGwae }
    $relationDisplay = if (@($item.elementRelations).Count -eq 0) { '-' } else { @($item.elementRelations | ForEach-Object { "$($_.displayName)[$($_.applicationMode)]: $($_.functionalMeaning)" }) -join '<br>' }
    $roleObjectDisplay = "``$($item.e5RoleObjectActionGate.applicabilityCode)/$($item.e5RoleObjectActionGate.roleObjectKindCode)``"
    $transitionDisplay = "``$($item.e5RoleObjectActionGate.actionCode)``<br>$(@($item.e5RoleObjectActionGate.startStateCodes) -join ', ') → $(@($item.e5RoleObjectActionGate.completionStateCodes) -join ', ')<br>효과: $(@($item.e5RoleObjectActionGate.effectCodes) -join ', ')"
    $lines.Add("| ``$($item.wiId)`` | $($item.title) | $roleObjectDisplay | $transitionDisplay | $(Format-Gwae $item.actionGwae) | $operationDisplay | $targetDisplay | $supportDisplay | $relationDisplay | ``$($item.classificationStatus)`` |")
}
$lines.Add('')
$lines.Add('## 역할 객체·행위 E5 표본')
$lines.Add('')
$lines.Add('| 프로필 | 상태 | 순서 | 열린 결손 |')
$lines.Add('| --- | --- | --- | --- |')
foreach ($profile in $roleObjectActionProfiles) {
    $stepDisplay = @($profile.steps | ForEach-Object { "$($_.sequence). $($_.roleCode) / $($_.bindingKindCode) / $(Format-Gwae ([string] $_.gwaeCode)) / $($_.wiId)" }) -join '<br>'
    $gapDisplay = if (@($profile.openGapRefs).Count -eq 0) { '-' } else { @($profile.openGapRefs) -join '<br>' }
    $lines.Add("| ``$($profile.profileId)``<br>$($profile.title) | ``$($profile.e5ReadinessCode)`` | $stepDisplay | $gapDisplay |")
}
$lines.Add('')
$lines.Add('> 역할 객체 표본은 기존 WI와 권위 코드를 대조한 E4 준비 자료다. `Gap` 단계나 실제 객체·상태·표현 결속이 남아 있으면 E5가 아니다.')
$markdown = ($lines -join "`n") + "`n"

$jsonOutputFullPath = Resolve-RepositoryPath $JsonOutputPath
$markdownOutputFullPath = Resolve-RepositoryPath $MarkdownOutputPath

if ($Mode -eq 'Write') {
    Write-Utf8NoBom $jsonOutputFullPath $json
    Write-Utf8NoBom $markdownOutputFullPath $markdown
}
elseif ($Mode -eq 'Check') {
    Require (Test-Path -LiteralPath $jsonOutputFullPath) "GeneratedFileMissing:$JsonOutputPath"
    Require (Test-Path -LiteralPath $markdownOutputFullPath) "GeneratedFileMissing:$MarkdownOutputPath"
    Require ((Get-Content -Raw -Encoding UTF8 -LiteralPath $jsonOutputFullPath) -ceq $json) "GeneratedFileStale:$JsonOutputPath"
    Require ((Get-Content -Raw -Encoding UTF8 -LiteralPath $markdownOutputFullPath) -ceq $markdown) "GeneratedFileStale:$MarkdownOutputPath"
}

Write-Output ('WorldInteractionGwaeClassification:{0}:Passed:{1}' -f $Mode, $resolvedItems.Count)
