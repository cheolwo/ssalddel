[CmdletBinding()]
param(
    [ValidateSet("Validate", "Write")]
    [string] $Mode = "Validate",
    [string] $PlayableLoopPath = "eng/execution-ledgers/playable-loops.json",
    [string] $GoalLedgerPath = "eng/execution-ledgers/codex-playable-loop-goals.json",
    [string] $OutputPath = "docs/AI/generated/playable-loop-topic-planning.md"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../common/deterministic-text-output.ps1")
. (Join-Path $PSScriptRoot "../common/parallel-development-work.ps1")

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "PlayableLoopTopicPlanningInvalid:$Code" }
}
function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}
function Escape-Cell([object] $Value) {
    return ([string] $Value).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}
function Resolve-RepoPath([string] $Path) { Join-Path $repositoryRoot $Path }

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$loops = Get-Content -LiteralPath (Resolve-RepoPath $PlayableLoopPath) -Raw -Encoding UTF8 | ConvertFrom-Json
$goals = Get-Content -LiteralPath (Resolve-RepoPath $GoalLedgerPath) -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $loops.schemaVersion -eq "ssalddel-playable-loop-catalog.v6") "SchemaInvalid"
$policy = $loops.designDocumentationPolicy
Require ([bool] $policy.topicPlanningRequiredForRegisteredPlayableUnits) "RegisteredPlayableLoopTopicPlanningRequirementMissing"
Require ([bool] $policy.registeredPlayableLoopKeepsDedicatedTopicGate) "RegisteredPlayableLoopDedicatedTopicGateMissing"
Require ([bool] $policy.interactionGoalMayUseIndependentPlanningGate) "IndependentInteractionPlanningGateMissing"
Require-Text $policy.detailedDesignRoot "DetailedDesignRootMissing"
Require-Text $policy.independentPlanningRoot "IndependentPlanningRootMissing"
Require (@($policy.independentPlanningRequiredHeadings).Count -gt 0) "IndependentPlanningHeadingsMissing"
Require-Text $policy.topicDesignTemplateRef "TopicDesignTemplateMissing"
Require (Test-Path -LiteralPath (Resolve-RepoPath ([string] $policy.topicDesignTemplateRef))) "TopicDesignTemplateNotFound"
$allowed = @($policy.allowedPlanningStatusCodes | ForEach-Object { [string] $_ })
Require (($allowed -join ",") -eq "NotStarted,Draft,ReadyForReview,Approved,LegacyActiveMigration") "PlanningStatusesInvalid"
$storyPolicyProperty = $policy.PSObject.Properties["storyBeatBindingPolicy"]
Require ($null -ne $storyPolicyProperty) "StoryBeatBindingPolicyMissing"
$storyPolicy = $policy.storyBeatBindingPolicy
Require ([string] $storyPolicy.bindingCardinalityCode -eq "ExplicitManyToMany") "StoryBeatCardinalityInvalid"
Require ([bool] $storyPolicy.bindingOptionalForIndependentGameplay) "StoryBeatIndependentGameplayBoundaryMissing"
Require ([bool] $storyPolicy.acceptedBindingRequiredToClaimStoryDrivenDevelopment) "StoryBeatDevelopmentClaimBoundaryMissing"
Require ([bool] $storyPolicy.bindingDoesNotReplaceApprovedPlanningGate) "StoryBeatTopicBoundaryMissing"
Require ([string] $storyPolicy.graphMapRoutingCode -eq "RequiredOnlyWhenSpatialImpactElseNoImpact") "StoryBeatGraphMapRoutingInvalid"
Require ([bool] $storyPolicy.acceptedBindingRequiresApprovedLineStory) "StoryBeatApprovedLineGateMissing"
$allowedStoryRoles = @($storyPolicy.allowedRoleCodes | ForEach-Object { [string] $_ })
Require (($allowedStoryRoles -join ",") -eq "PrimaryExperience,SupportingExperience,AlternativeExperience") "StoryBeatRoleCodesInvalid"
$allowedStoryStatuses = @($storyPolicy.allowedStatusCodes | ForEach-Object { [string] $_ })
Require (($allowedStoryStatuses -join ",") -eq "Candidate,Accepted") "StoryBeatStatusCodesInvalid"
Require-Text $storyPolicy.hexagramStoryProductionLedgerRef "HexagramStoryLedgerRefMissing"
$hexagramLedgerPath = Resolve-RepoPath ([string] $storyPolicy.hexagramStoryProductionLedgerRef)
Require (Test-Path -LiteralPath $hexagramLedgerPath) "HexagramStoryLedgerNotFound"
$hexagramLedger = Get-Content -LiteralPath $hexagramLedgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require ([string] $hexagramLedger.schemaVersion -eq "mirror-hexagram-story-production.v4") "HexagramStoryLedgerSchemaInvalid"
$hexagramsById = @{}
$lineStoriesById = @{}
foreach ($hexagram in @($hexagramLedger.hexagrams)) {
    $hexagramId = [string] $hexagram.stableId
    $hexagramsById[$hexagramId] = $hexagram
    foreach ($lineStory in @($hexagram.lineStories)) {
        $lineStoriesById[[string] $lineStory.stableId] = [PSCustomObject]@{
            Hexagram = $hexagram
            LineStory = $lineStory
        }
    }
}

$goalByLoop = @{}
foreach ($goal in @($goals.items)) { $goalByLoop[[string] $goal.loopStableId] = $goal }
$activeLoopId = [string] $goals.activeGoal.loopStableId
$workItems = @(Get-ParallelDevelopmentWorkItems -Ledger $goals)
$activeLoopIds = @(@($goals.items | Where-Object { $_.goalStateCode -eq 'Active' } | ForEach-Object { $_.loopStableId }) + @($workItems | Where-Object { $_.statusCode -in @('Active','ReadyForIntegration') } | ForEach-Object { $_.loopStableId }) | Sort-Object -Unique)
$legacyLoopId = [string] $policy.legacyActiveMigrationLoopStableId
$seenTopics = @{}
$units = @($loops.items | Where-Object loopLevelCode -eq "PlayableUnit")
$legacyCount = 0
$storyBindingCount = 0
$acceptedStoryBindingCount = 0
$requiredHeadings = @(
    "## 식별과 근거", "## 플레이어 약속과 재미", "## 반복 폐루프",
    "## 선택·대가·성공·실패·회복", "## WI 단일 책임 후보",
    "## 논리·표현 요구", "## H 공간과 자산 요구",
    "## 저장·권위·외부 경계", "## 제외 범위와 승인")

foreach ($loop in @($loops.items)) {
    $id = [string] $loop.loopStableId
    $gateProperty = $loop.PSObject.Properties["planningGate"]
    $storyBindingsProperty = $loop.PSObject.Properties["storyBeatBindings"]
    if ([string] $loop.loopLevelCode -ne "PlayableUnit") {
        Require ($null -eq $gateProperty) "AggregateHasPlanningGate:$id"
        Require ($null -eq $storyBindingsProperty) "AggregateHasStoryBeatBindings:$id"
        continue
    }
    Require ($null -ne $gateProperty) "PlanningGateMissing:$id"
    $gate = $loop.planningGate
    $topicId = [string] $gate.topicStableId
    Require-Text $topicId "TopicStableIdMissing:$id"
    Require (-not $seenTopics.ContainsKey($topicId)) "TopicStableIdDuplicate:$topicId"
    $expectedTopicId = ([string] $id).Replace("playable-loop:", "topic:")
    Require ($topicId -eq $expectedTopicId) "TopicStableIdDrift:$id"
    $seenTopics[$topicId] = $id
    $status = [string] $gate.statusCode
    Require ($allowed -contains $status) "PlanningStatusInvalid:$id"

    if ($status -eq "NotStarted") {
        foreach ($field in @("designDocumentRef", "designRevision", "designHashSha256", "approvalEvidenceRef")) {
            Require ([string]::IsNullOrWhiteSpace([string] $gate.$field)) "NotStartedFieldMustBeEmpty:${id}:$field"
        }
    }
    elseif ($status -eq "LegacyActiveMigration") {
        $legacyCount++
        Require ($id -eq $legacyLoopId) "LegacyMigrationTransferred:$id"
        Require ($activeLoopIds -contains $id) "LegacyMigrationIsNotActive:$id"
        Require ($goalByLoop.ContainsKey($id) -and [string] $goalByLoop[$id].goalStateCode -eq "Active") "LegacyMigrationGoalMustRemainActive:$id"
    }
    else {
        foreach ($field in @("designDocumentRef", "designRevision", "designHashSha256")) {
            Require-Text $gate.$field "PlanningFieldMissing:${id}:$field"
        }
        $documentRef = [string] $gate.designDocumentRef
        $normalizedDocumentRef = $documentRef.Replace("\", "/")
        $independentPlanningRoot = ([string] $policy.independentPlanningRoot).TrimEnd("/")
        $allowedDesignRoots = @(
            ([string] $policy.detailedDesignRoot).TrimEnd("/"),
            $independentPlanningRoot)
        Require (@($allowedDesignRoots | Where-Object {
            $normalizedDocumentRef.StartsWith($_ + "/", [StringComparison]::Ordinal)
        }).Count -gt 0) "DesignDocumentOutsideRoot:$id"
        $documentPath = Resolve-RepoPath $documentRef
        Require (Test-Path -LiteralPath $documentPath) "DesignDocumentNotFound:$id"
        $content = Get-Content -LiteralPath $documentPath -Raw -Encoding UTF8
        $documentRequiredHeadings = if ($normalizedDocumentRef.StartsWith(
            $independentPlanningRoot + "/", [StringComparison]::Ordinal)) {
            @($policy.independentPlanningRequiredHeadings)
        } else {
            $requiredHeadings
        }
        foreach ($heading in $documentRequiredHeadings) {
            Require ($content.Contains($heading)) "DesignHeadingMissing:${id}:$heading"
        }
        Require (@($loop.sourcePlanningDocumentRefs).Count -gt 0) "SourcePlanningDocumentMissing:$id"
        foreach ($sourceRef in @($loop.sourcePlanningDocumentRefs)) {
            Require (Test-Path -LiteralPath (Resolve-RepoPath ([string] $sourceRef))) "SourcePlanningDocumentNotFound:${id}:$sourceRef"
        }
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $documentPath).Hash
        Require ($hash.Equals([string] $gate.designHashSha256, [StringComparison]::OrdinalIgnoreCase)) "DesignHashMismatch:$id"
        if ($status -eq "Approved") {
            Require-Text $gate.approvalEvidenceRef "ApprovalEvidenceMissing:$id"
        }
        else {
            Require ([string]::IsNullOrWhiteSpace([string] $gate.approvalEvidenceRef)) "UnapprovedHasApprovalEvidence:$id"
        }
    }

    if ($null -ne $storyBindingsProperty) {
        $storyBindings = @($loop.storyBeatBindings)
        Require ($storyBindings.Count -gt 0) "StoryBeatBindingsEmpty:$id"
        $seenStoryBeatBindings = @{}
        foreach ($binding in $storyBindings) {
            $storyBeatId = [string] $binding.storyBeatStableId
            $hexagramId = [string] $binding.hexagramStableId
            $lineStoryId = [string] $binding.lineStoryStableId
            $roleCode = [string] $binding.bindingRoleCode
            $bindingStatus = [string] $binding.statusCode
            $storyDocumentRef = [string] $binding.storyDocumentRef
            $storyRevision = [string] $binding.storyRevision
            $storyHash = [string] $binding.storyHashSha256
            Require-Text $storyBeatId "StoryBeatStableIdMissing:$id"
            Require ($storyBeatId -match '^story-beat:[a-z0-9-]+\.v[0-9]+$') "StoryBeatStableIdInvalid:${id}:$storyBeatId"
            Require-Text $hexagramId "StoryBeatHexagramIdMissing:${id}:$storyBeatId"
            Require-Text $lineStoryId "StoryBeatLineStoryIdMissing:${id}:$storyBeatId"
            Require ($allowedStoryRoles -contains $roleCode) "StoryBeatBindingRoleInvalid:${id}:$storyBeatId"
            Require ($allowedStoryStatuses -contains $bindingStatus) "StoryBeatBindingStatusInvalid:${id}:$storyBeatId"
            $bindingKey = "$storyBeatId|$lineStoryId|$roleCode"
            Require (-not $seenStoryBeatBindings.ContainsKey($bindingKey)) "StoryBeatBindingDuplicate:${id}:$bindingKey"
            $seenStoryBeatBindings[$bindingKey] = $true
            Require ($hexagramsById.ContainsKey($hexagramId)) "StoryBeatHexagramUnknown:${id}:$hexagramId"
            Require ($lineStoriesById.ContainsKey($lineStoryId)) "StoryBeatLineStoryUnknown:${id}:$lineStoryId"
            $lineContext = $lineStoriesById[$lineStoryId]
            Require ([string] $lineContext.Hexagram.stableId -eq $hexagramId) "StoryBeatLineHexagramMismatch:${id}:$lineStoryId"
            $lineStatus = [string] $lineContext.LineStory.storyStatusCode
            Require ($lineStatus -in @("Draft", "Reviewed", "Approved", "PrototypeReference")) "StoryBeatLineNotMapped:${id}:$lineStoryId"
            $registeredStoryBeatIds = @(
                @([string] $lineContext.LineStory.primaryStoryBeatRef) +
                @($lineContext.LineStory.supportingStoryBeatRefs | ForEach-Object { [string] $_ }) |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )
            Require ($registeredStoryBeatIds -contains $storyBeatId) "StoryBeatNotRegisteredInLineStory:${id}:$storyBeatId"
            Require-Text $storyDocumentRef "StoryBeatDocumentRefMissing:${id}:$storyBeatId"
            Require-Text $storyRevision "StoryBeatRevisionMissing:${id}:$storyBeatId"
            Require ($storyHash -match '^[A-Fa-f0-9]{64}$') "StoryBeatDocumentHashInvalid:${id}:$storyBeatId"
            $storyDocumentPath = Resolve-RepoPath $storyDocumentRef
            Require (Test-Path -LiteralPath $storyDocumentPath) "StoryBeatDocumentNotFound:${id}:$storyBeatId"
            $storyDocumentContent = Get-Content -LiteralPath $storyDocumentPath -Raw -Encoding UTF8
            Require ($storyDocumentContent.Contains($storyBeatId)) "StoryBeatIdNotInDocument:${id}:$storyBeatId"
            Require ($storyDocumentContent.Contains($storyRevision)) "StoryBeatRevisionNotInDocument:${id}:$storyBeatId"
            $actualStoryHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $storyDocumentPath).Hash
            Require ($actualStoryHash.Equals($storyHash, [StringComparison]::OrdinalIgnoreCase)) "StoryBeatDocumentHashMismatch:${id}:$storyBeatId"
            if ($bindingStatus -eq "Accepted") {
                Require ($lineStatus -eq "Approved") "AcceptedStoryBeatLineNotApproved:${id}:$lineStoryId"
                $acceptedStoryBindingCount++
            }
            $storyBindingCount++
        }
    }
}
Require ($legacyCount -le 1) "LegacyMigrationCountInvalid"

foreach ($activeId in $activeLoopIds) {
    Require ($goalByLoop.ContainsKey($activeId)) "ActiveGoalLoopUnknown:$activeId"
    $activeUnits = @($units | Where-Object loopStableId -eq $activeId)
    Require ($activeUnits.Count -eq 1) "ActiveGoalPlayableUnitMissing:$activeId"
    $status = [string] $activeUnits[0].planningGate.statusCode
    Require ($status -eq "Approved" -or ($status -eq "LegacyActiveMigration" -and $activeId -eq $legacyLoopId)) "ActiveGoalPlanningNotApproved:$activeId"
}
$workReadiness = @(Test-ParallelDevelopmentWorkItems -Ledger $goals -Loops $loops -RepositoryRoot $repositoryRoot)
foreach ($readiness in @($workReadiness | Where-Object { $_.statusCode -in @('Active','ReadyForIntegration') })) {
    Require ([bool] $readiness.canExecute) "ActiveWorkItemPlanningNotApproved:$($readiness.workItemId):$(@($readiness.blockerCodes) -join ',')"
}
$activePlanning = (@($units | Where-Object loopStableId -eq $activeLoopId | ForEach-Object { $_.planningGate.statusCode }) -join ',')

$nextGoal = @($goals.items | Where-Object goalStateCode -eq "Queued" | Sort-Object queueOrder | Select-Object -First 1)
$nextText = if ($nextGoal.Count -eq 0) { "없음" } else {
    $nextLoopId = [string] $nextGoal[0].loopStableId
    $nextLoop = @($units | Where-Object { [string] $_.loopStableId -eq $nextLoopId })[0]
    "$($nextGoal[0].loopStableId) / $($nextLoop.planningGate.statusCode)"
}

$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine("# PlayableLoop 주제 기획 상태")
[void] $builder.AppendLine()
[void] $builder.AppendLine("> 이 문서는 ``$PlayableLoopPath``와 ``$GoalLedgerPath``에서 자동 생성된다. 직접 수정하지 않는다.")
[void] $builder.AppendLine()
[void] $builder.AppendLine("- PlayableUnit: ``$($units.Count)``")
[void] $builder.AppendLine("- 승인됨: ``$(@($units | Where-Object { $_.planningGate.statusCode -eq 'Approved' }).Count)``")
[void] $builder.AppendLine("- 한시적 이전: ``$legacyCount``")
[void] $builder.AppendLine("- 사건 마디 결속: ``$storyBindingCount`` / Accepted ``$acceptedStoryBindingCount``")
[void] $builder.AppendLine("- 대표 표시 Goal: ``$activeLoopId`` / ``$activePlanning``")
[void] $builder.AppendLine("- 활성 Goal 범위: ``$($activeLoopIds -join ', ')``")
[void] $builder.AppendLine("- 병렬 작업: ``$($workItems.Count)`` / 실행 가능 ``$(@($workReadiness | Where-Object canExecute).Count)`` (고정 WIP 상한 없음)")
[void] $builder.AppendLine("- 다음 대기 Goal: ``$nextText``")
[void] $builder.AppendLine()
[void] $builder.AppendLine("| 주제 | PlayableLoop | 사건 마디 결속 | 기획 상태 | Goal | 현재 WI | Logic | Presentation | 통합 E | 다음 기획 책임 |")
[void] $builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
foreach ($loop in $units) {
    $gate = $loop.planningGate
    $goal = if ($goalByLoop.ContainsKey([string] $loop.loopStableId)) {
        $goalByLoop[[string] $loop.loopStableId]
    } else {
        $null
    }
    $responsibility = switch ([string] $gate.statusCode) {
        "NotStarted" { "기획서 작성" }
        "Draft" { "필수 절 보완·검토 요청" }
        "ReadyForReview" { "명시적 승인" }
        "Approved" { "Goal 활성화 가능" }
        "LegacyActiveMigration" { "현재 Goal 완료 전 승인 전환" }
    }
    $bindings = @(if ($null -eq $loop.PSObject.Properties["storyBeatBindings"]) { @() } else { @($loop.storyBeatBindings) })
    $bindingSummary = if ($bindings.Count -eq 0) { "독립/미결속" } else { @($bindings | ForEach-Object { "``$($_.storyBeatStableId)`` ($($_.statusCode))" }) -join "<br>" }
    $goalStateCode = if ($null -eq $goal) { "NotRegistered" } else { [string] $goal.goalStateCode }
    $nextWorldInteractionId = if ($null -eq $goal) { "" } else { [string] $goal.nextWorldInteractionId }
    [void] $builder.AppendLine("| ``$(Escape-Cell $gate.topicStableId)`` | ``$(Escape-Cell $loop.loopStableId)`` | $bindingSummary | $($gate.statusCode) | $goalStateCode | ``$(Escape-Cell $nextWorldInteractionId)`` | $($loop.maturityTracks.logic.currentStage) | $($loop.maturityTracks.presentation.currentStage) | $($loop.currentEvidenceStage) | $responsibility |")
}
$content = ConvertTo-DeterministicText $builder.ToString()
$resolvedOutput = Resolve-RepoPath $OutputPath
if ($Mode -eq "Write") {
    [void] (Write-DeterministicTextIfChanged -Path $resolvedOutput -Content $content)
}
Write-Output "PlayableLoopTopicPlanningValid:Units=$($units.Count);Approved=$(@($units | Where-Object { $_.planningGate.statusCode -eq 'Approved' }).Count);Legacy=$legacyCount;StoryBeatBindings=$storyBindingCount;AcceptedStoryBeatBindings=$acceptedStoryBindingCount;Active=$activeLoopId"
