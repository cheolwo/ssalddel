[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Export','Check')][string] $Mode,
    [Parameter(Mandatory)][string] $GoalId,
    [string] $WorkItemId = '',
    [string] $UnityProjectRoot = '',
    [string] $BaselinePath = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../common/development-handoff.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($UnityProjectRoot) { $UnityProjectRoot = [IO.Path]::GetFullPath($UnityProjectRoot) }
$policy = Read-HandoffJson $root 'eng/execution-ledgers/subject-interaction-development.json'
$ledger = Read-HandoffJson $root $policy.legacyGoalLedgerPath
$selection = Resolve-HandoffSelection $policy $ledger $GoalId $WorkItemId
$goal = $selection.goal; $work = $selection.work
$orderRef = if ($work) { [string]$work.workOrderRef } else { [string]$goal.workOrderRef }
$order = Read-HandoffJson $root $orderRef
$gate = Get-ParallelWorkField $order 'planningGate'
$notes = Get-ParallelWorkField $order 'handoffNotes'
$blockers = [Collections.Generic.List[string]]::new()
$warnings = [Collections.Generic.List[string]]::new()
$refs = [Collections.Generic.List[string]]::new()
foreach ($reference in @('AGENTS.md', 'docs/AGENTS.md', 'docs/Architecture/CodexPlayableLoopGoal운영체계.md',
    $orderRef, (Get-ParallelWorkField $gate 'designDocumentRef'),
    (Get-ParallelWorkField $gate 'approvalEvidenceRef'), (Get-ParallelWorkField $order 'implementationScopeRef'),
    (Get-ParallelWorkField $order 'detailedWorkOrderRef')) + @(Get-ParallelWorkField $notes 'readRefs' @())) {
    if ($reference) { $refs.Add([string]$reference) }
}
foreach ($code in @(Test-HandoffDescription $notes)) { $blockers.Add($code) }
$owner = [string](Get-ParallelWorkField $work 'ownerThreadId' (Get-ParallelWorkField $order 'ownerThreadId' ''))
if (-not $owner) { $blockers.Add('OwnerMissing') }
if ((Get-ParallelWorkField $goal 'goalStateCode') -ne 'Active') { $blockers.Add('GoalNotActive') }
if ((Get-ParallelWorkField $gate 'statusCode') -ne 'Approved') { $blockers.Add('PlanningNotApproved') }
if ((Get-ParallelWorkField $order 'activeWorldInteractionId') -ne $selection.wi) { $blockers.Add('WorkOrderWiMismatch') }
$cap = Get-ParallelWorkField $order 'deliveryCap'
if ($cap -isnot [string]) { $cap = Get-ParallelWorkField $cap 'currentDispatchTargetStage' }
if ($cap -notmatch '^E[1-7]$') { $blockers.Add('DeliveryCapMissing') }

# 기존 검증기의 읽기 전용 경로만 호출한다. 오류를 숨기거나 통과로 바꾸지 않는다.
try { & (Join-Path $PSScriptRoot 'manage-subject-interaction-development.ps1') -Mode Validate | Out-Null }
catch { $warnings.Add("CatalogValidation:$($_.Exception.Message)") }
try { & (Join-Path $PSScriptRoot 'manage-e7-vertical-work-order.ps1') -InputPath $orderRef -UnityProjectRoot $UnityProjectRoot | Out-Null }
catch { $blockers.Add("WorkOrderValidation:$($_.Exception.Message)") }

foreach ($code in @(Test-HandoffResearch $root $order)) { $blockers.Add($code) }
foreach ($binding in @(Get-ParallelWorkField $order 'requiredResearch' @())) {
    $reference = [string](Get-ParallelWorkField $binding 'documentRef')
    if ($reference) { $refs.Add($reference) }
}

$writes = @(Get-ParallelWorkField $work 'writePaths' (Get-ParallelWorkField $order 'writePaths' @()))
if ($writes.Count -eq 0) { $blockers.Add('WritePathsMissing') }
$resolvedWrites = @()
foreach ($reference in $writes) {
    try {
        $resolved = Resolve-HandoffFile $root $UnityProjectRoot $reference
        $resolvedWrites += $resolved
        $refs.Add([string]$reference)
        if ([IO.Path]::IsPathRooted($reference)) { $warnings.Add('LegacyAbsoluteUnityPath:normalize-on-next-scope-revision') }
    } catch { $blockers.Add($_.Exception.Message) }
}
if ($work) {
    $loops = Read-HandoffJson $root $ledger.playableLoopCatalogPath
    try {
        $results = @(Test-ParallelDevelopmentWorkItems $ledger $loops $root)
        $checked = @($results | Where-Object workItemId -eq $work.workItemId)
        if ($checked.Count -ne 1) { $blockers.Add('WorkItemValidationMissing') }
        else {
            foreach ($code in $checked[0].blockerCodes) { $blockers.Add($code) }
            if (-not $checked[0].canExecute) { $blockers.Add('WorkItemNotExecutable') }
        }
    } catch { $blockers.Add("WorkItemValidation:$($_.Exception.Message)") }
    foreach ($file in @(Get-ParallelWorkField $work 'baselineFiles' @())) { $refs.Add([string]$file.path) }
} else {
    if (@(Get-ParallelWorkField $order 'dependsOnWorkItemIds' @()).Count -gt 0) { $blockers.Add('DependencyRequiresWorkItemBinding') }
}

# 같은 WI라도 다른 작업이 같은 경로·계약을 소유하면 자동으로 합치지 않는다.
$others = @(Get-ParallelDevelopmentWorkItems $ledger | Where-Object {
    $_.statusCode -in @('Active','ReadyForIntegration') -and $_.workItemId -ne (Get-ParallelWorkField $work 'workItemId')
})
foreach ($otherGoal in @($policy.nativeInteractionGoals | Where-Object { $_.goalStableId -ne $GoalId -and $_.goalStateCode -eq 'Active' })) {
    $otherOrder = Read-HandoffJson $root $otherGoal.workOrderRef
    $others += [pscustomobject]@{ workItemId=$otherGoal.goalStableId; writePaths=@(Get-ParallelWorkField $otherOrder 'writePaths' @()); sharedContractKeys=@(Get-ParallelWorkField $otherOrder 'sharedContractKeys' @()) }
}
foreach ($other in $others) {
    foreach ($reference in @(Get-ParallelWorkField $other 'writePaths' @())) {
        try { $right = Resolve-HandoffFile $root $UnityProjectRoot $reference } catch { continue }
        foreach ($left in $resolvedWrites) {
            if (Test-HandoffOverlap $left.fullPath $right.fullPath) { $blockers.Add("WriteOwnershipConflict:$($other.workItemId):$($left.repository):$($left.path)") }
        }
    }
    foreach ($key in @(Get-ParallelWorkField $work 'sharedContractKeys' (Get-ParallelWorkField $order 'sharedContractKeys' @()))) {
        if (@(Get-ParallelWorkField $other 'sharedContractKeys' @()) -contains $key) { $blockers.Add("SharedContractConflict:$key") }
    }
}
$files = @()
foreach ($reference in @($refs | Sort-Object -Unique)) {
    try {
        $inventory = @(Get-HandoffInventory $root $UnityProjectRoot @($reference))
        $files += $inventory
        if ($reference -notin $writes -and @($inventory | Where-Object sha256 -eq 'Missing').Count -gt 0) { $blockers.Add("InputMissing:$reference") }
    } catch { $blockers.Add($_.Exception.Message) }
}
$repositories = @()
foreach ($pair in @(@('hongdal',$root), @('unity',$UnityProjectRoot))) {
    if (-not $pair[1] -or -not (Test-Path -LiteralPath $pair[1] -PathType Container)) { continue }
    $head = & git -C $pair[1] rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { $blockers.Add("GitRepositoryInvalid:$($pair[0])"); continue }
    $dirty = @(& git -C $pair[1] status --porcelain --untracked-files=normal)
    if ($dirty.Count -gt 0) { $warnings.Add("DirtyWorkingTree:$($pair[0]):$($dirty.Count);unrelated-files-preserved") }
    $repositories += [ordered]@{ repository=$pair[0]; head=[string]$head }
}
$packet = [ordered]@{
    schemaVersion='development-handoff.v1'; goalId=$GoalId
    workItemId=[string](Get-ParallelWorkField $work 'workItemId' '')
    worldInteractionId=$selection.wi; ownerThreadId=$owner; deliveryCap=$cap
    workOrderRef=$orderRef; designDocumentRef=(Get-ParallelWorkField $gate 'designDocumentRef')
    selectionHash=(Get-HandoffHash @($goal,$work)); notes=$notes
    writePaths=@($resolvedWrites | ForEach-Object { "$($_.repository):$($_.path)" })
    files=@($files | Sort-Object repository,path -Unique); repositories=$repositories
    statusCode=''; blockerCodes=@(); warningCodes=@($warnings | Sort-Object -Unique)
}
if ($Mode -eq 'Check') {
    if (-not $BaselinePath) { throw 'DevelopmentHandoffInvalid:BaselinePathRequired' }
    $previous = Get-Content -LiteralPath $BaselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($code in @(Test-HandoffBaseline $previous $packet)) { $blockers.Add($code) }
    foreach ($repo in $repositories) {
        $old = @($previous.repositories | Where-Object repository -eq $repo.repository)
        if ($old.Count -ne 1 -or $old[0].head -ne $repo.head) { $warnings.Add("RepositoryHeadChanged:$($repo.repository);review-input-hashes") }
    }
}
$packet.blockerCodes = @($blockers | Sort-Object -Unique)
$packet.warningCodes = @($warnings | Sort-Object -Unique)
$packet.statusCode = if ($blockers.Count) { 'Blocked' } else { 'ChecksPassedNotExecutionApproval' }
if ($Mode -eq 'Export') {
    $slug = ($GoalId + $(if ($packet.workItemId) { '-' + $packet.workItemId })) -replace '[^a-zA-Z0-9.-]', '-'
    $directory = Join-Path $root "artifacts/local/development-handoffs/$slug"
    Assert-HandoffPhysicalPath $directory
    Assert-HandoffPhysicalPath (Join-Path $directory 'handoff.json')
    Assert-HandoffPhysicalPath (Join-Path $directory 'handoff.md')
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'handoff.json'), ($packet | ConvertTo-Json -Depth 90)+"`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $directory 'handoff.md'), (ConvertTo-HandoffMarkdown $packet), [Text.UTF8Encoding]::new($false))
}
$packet | ConvertTo-Json -Depth 90
# Check는 파일을 쓰지 않고 프로세스 호출 시 차단 여부를 종료 코드로도 전달한다.
if ($Mode -eq 'Check' -and $blockers.Count -gt 0) { throw 'DevelopmentHandoffCheckBlocked:see-json-blockerCodes' }
