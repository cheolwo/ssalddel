$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manager = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-e7-vertical-work-order.ps1'
$artifactDirectory = Join-Path $repositoryRoot 'artifacts/local/validation/e7-native-work-order'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$catalogRef = 'eng/execution-ledgers/subject-interaction-development.json'
$orderRefs = @(
    'eng/execution-ledgers/work-orders/restaurant-auto-accept.e7-work-order.json',
    'eng/execution-ledgers/work-orders/restaurant-cooking.e7-work-order.json'
)
function Read-Json([string] $Reference) {
    Get-Content -LiteralPath (Join-Path $repositoryRoot $Reference) -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Save-Fixture([string] $Name, [object] $Value) {
    $path = Join-Path $artifactDirectory "$Name.json"
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 60), [Text.UTF8Encoding]::new($false))
    [IO.Path]::GetRelativePath($repositoryRoot, $path).Replace('\', '/')
}
function Reject([string] $Name, [scriptblock] $Mutate, [string] $Code) {
    $order = Read-Json $orderRefs[0]
    $catalog = Read-Json $catalogRef
    $goal = @($catalog.nativeInteractionGoals | Where-Object goalStableId -eq $order.interactionGoalStableId)[0]
    & $Mutate $order $catalog $goal
    $inputRef = Save-Fixture "$Name.order" $order
    $goalsRef = Save-Fixture "$Name.goals" $catalog
    $rejected = $false
    try { & $manager -InputPath $inputRef -InteractionGoalPath $goalsRef | Out-Null }
    catch { $rejected = $_.Exception.Message.Contains($Code); if (-not $rejected) { throw } }
    if (-not $rejected) { throw "NativeWorkOrderExpectedFailureMissing:${Name}:$Code" }
}

# 기존 Loop 양식과 검사는 계속 유지한다. 실제 Loop들의 현행 단계 불일치는 별도 검사한다.
& $manager | Out-Null
foreach ($reference in $orderRefs) {
    $order = Read-Json $reference
    if ($null -ne $order.PSObject.Properties['playableUnitStableId']) { throw "NativeOrderHasSyntheticLoop:$reference" }
    & $manager -InputPath $reference | Out-Null
}
Reject 'unknown-goal' { param($order, $catalog, $goal) $order.interactionGoalStableId = 'interaction-goal:unknown' } 'InteractionGoalUnknown'
Reject 'wrong-wi' { param($order, $catalog, $goal) $order.activeWorldInteractionId = 'WI-UNKNOWN' } 'InteractionGoalWiMismatch'
Reject 'unknown-wi' { param($order, $catalog, $goal) $order.activeWorldInteractionId = $goal.worldInteractionId = 'WI-UNKNOWN' } 'InteractionGoalWiUnknown'
Reject 'wrong-subject' { param($order, $catalog, $goal) $order.subjectBindingRefs = @('subject:unknown') } 'InteractionSubjectsMismatch'
Reject 'unready-subject' {
    param($order, $catalog, $goal)
    $subjects = Read-Json $catalog.subjectCatalogPath
    @($subjects.items | Where-Object subjectStableId -eq $order.subjectBindingRefs[0])[0].statusCode = 'Blocked'
    $catalog.subjectCatalogPath = Save-Fixture 'unready.subjects' $subjects
} 'InteractionSubjectNotReady'
Reject 'unapproved' { param($order, $catalog, $goal) $order.planningGate.statusCode = 'Asked' } 'InteractionPlanningNotApproved'
Reject 'changed-hash' {
    param($order, $catalog, $goal)
    $order.planningGate.designHashSha256 = $goal.planningGate.designHashSha256 = ('0' * 64)
} 'InteractionDesignHashMismatch'
Reject 'wrong-order' { param($order, $catalog, $goal) $order.workOrderId = 'E7-WO-WRONG' } 'InteractionWorkOrderMismatch'
Reject 'delivery-cap' { param($order, $catalog, $goal) $order.trackPlans.logic.currentEvidenceStage = 'E3'; $goal.deliveryCap = 'E2' } 'InteractionDeliveryCapExceeded'
Reject 'missing-loop' { param($order, $catalog, $goal) $goal.optionalPlayableLoopValidationRef = 'loop:unknown' } 'InteractionOptionalLoopMismatch'
Reject 'malformed-track' { param($order, $catalog, $goal) $order.trackPlans.logic.downwardPlan[0].code = 'E6' } 'DownwardOrderInvalid'
Write-Output 'E7NativeWorkOrderTestsPassed:LoopTemplate=1;Native=2;Rejected=11;Promotion=False'
