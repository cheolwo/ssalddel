$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $root 'eng/common/development-handoff.ps1')
$script:passed = 0
function Assert([bool] $Condition, [string] $Name) {
    if (-not $Condition) { throw "DevelopmentHandoffTestFailed:$Name" }
    $script:passed++
}
function Reject([scriptblock] $Action, [string] $Code) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $_.Exception.Message.Contains($Code); if (-not $caught) { throw } }
    Assert $caught $Code
}
$policy = [pscustomobject]@{ nativeInteractionGoals=@([pscustomobject]@{
    goalStableId='interaction-goal:sample.v1'; worldInteractionId='WI-1'; workOrderRef='order.json'
}) }
$ledger = [pscustomobject]@{ schemaVersion='codex-playable-loop-goals.v4'; items=@(
    [pscustomobject]@{ loopStableId='playable-loop:old.v1'; nextWorldInteractionId='WI-2' }
); workItems=@([pscustomobject]@{
    workItemId='work:old'; worldInteractionId='WI-2'; loopStableId='playable-loop:old.v1'; workOrderRef='old.json'
}) }
$native = Resolve-HandoffSelection $policy $ledger 'interaction-goal:sample.v1' ''
Assert (-not $native.legacy -and $native.wi -eq 'WI-1') 'NativeSelection'
$old = Resolve-HandoffSelection $policy $ledger 'interaction-goal:old.wi-2.v1' 'work:old'
Assert ($old.legacy -and $old.work.workItemId -eq 'work:old') 'LegacySelection'
Reject { Resolve-HandoffSelection $policy $ledger 'unknown' '' } 'GoalSelection'
Reject { Resolve-HandoffSelection $policy $ledger 'interaction-goal:sample.v1' 'work:old' } 'WorkItemSelection'
$ledger.workItems += $ledger.workItems[0].PSObject.Copy()
$ledger.workItems[1].workItemId = 'work:old-second'
Reject { Resolve-HandoffSelection $policy $ledger 'interaction-goal:old.wi-2.v1' '' } 'WorkItemIdRequired'
Assert (Test-HandoffOverlap 'C:/repo/Code' 'c:/repo/code/a.cs') 'DirectoryOverlap'
Assert (-not (Test-HandoffOverlap 'C:/repo/Code' 'C:/repo/CodeOther/a.cs')) 'SiblingNotOverlap'
Reject { Resolve-HandoffFile $root '' 'unity:Assets/test.cs' } 'UnityRepositoryMissing'
Reject { Resolve-HandoffFile $root '' '../outside.cs' } 'PathOutsideRepository'
Reject { Resolve-HandoffFile $root '' 'src/*.cs' } 'RepositoryRelativePathRequired'
$notes = [pscustomobject]@{
    purpose='목적'; excludedScope='제외'; firstTask='첫 작업'; completionCriteria='완료'; decisionBoundary='반환'; returnInstructions='인계'
    moduleRelations=@('입력 → Core → Snapshot → 표시'); readRefs=@('AGENTS.md'); validationCommands=@('검사 명령')
}
Assert (@(Test-HandoffDescription $notes).Count -eq 0) 'CompleteDescription'
Assert (@(Test-HandoffDescription $null).Count -eq 9) 'MissingDescription'
$first = [pscustomobject]@{
    schemaVersion='development-handoff.v1'; goalId='g'; workItemId='w'; selectionHash='x'
    files=@([pscustomobject]@{repository='hongdal';path='test.cs';sha256='abc'})
}
$same = $first | ConvertTo-Json -Depth 10 | ConvertFrom-Json
Assert (@(Test-HandoffBaseline $first $same).Count -eq 0) 'UnchangedBaseline'
Assert ((Get-HandoffHash $first) -eq (Get-HandoffHash $same)) 'DeterministicHash'
$same.files[0].sha256 = 'dirty-change-with-same-head'
Assert (@(Test-HandoffBaseline $first $same) -contains 'InputChanged:hongdal:test.cs') 'DirtyInputChanged'
$same.files = @()
Assert (@(Test-HandoffBaseline $first $same) -contains 'InputChanged:hongdal:test.cs') 'DeletedInput'
$same.selectionHash = 'new-approval'
Assert (@(Test-HandoffBaseline $first $same) -contains 'BaselineChanged:selectionHash') 'SelectedRecordChanged'
$same.goalId = 'wrong-goal'
Assert (@(Test-HandoffBaseline $first $same) -contains 'BaselineChanged:goalId') 'WrongBaselineGoal'
$first.schemaVersion = 'future'
Assert (@(Test-HandoffBaseline $first $same) -contains 'BaselineSchemaInvalid') 'BaselineSchema'
$inventory = @(Get-HandoffInventory $root '' @('AGENTS.md','docs/AGENTS.md'))
Assert ($inventory.Count -eq 2 -and $inventory[0].sha256 -match '^[A-F0-9]{64}$') 'ActualFileHashes'
Assert ((Get-HandoffHash $inventory) -eq (Get-HandoffHash @(Get-HandoffInventory $root '' @('docs/AGENTS.md','AGENTS.md')))) 'InventoryOrdering'
$missing = @(Get-HandoffInventory $root '' @('artifacts/local/development-handoffs/nonexistent-test-file'))
Assert ($missing[0].sha256 -eq 'Missing') 'NewFileBaseline'
$researchOrder = [pscustomobject]@{ requiredResearch=@() }
Assert (@(Test-HandoffResearch $root $researchOrder).Count -eq 0) 'ResearchNotRequired'
Assert (@(Test-HandoffResearch $root ([pscustomobject]@{})) -contains 'ResearchDeclarationMissing') 'ResearchUndeclared'
$researchOrder.requiredResearch = @([pscustomobject]@{statusCode='Planned'; documentRef='AGENTS.md'; sha256=(Get-FileHash (Join-Path $root 'AGENTS.md')).Hash})
Assert (@(Test-HandoffResearch $root $researchOrder) -contains 'RequiredResearchNotAccepted') 'ResearchUnaccepted'
$researchOrder.requiredResearch[0].statusCode = 'Accepted'
Assert (@(Test-HandoffResearch $root $researchOrder).Count -eq 0) 'ResearchAcceptedBinding'
$researchOrder.requiredResearch[0].sha256 = '0' * 64
Assert (@(Test-HandoffResearch $root $researchOrder) -contains 'ResearchHashMismatch') 'ResearchStaleHash'
$researchOrder.requiredResearch = @('legacy-text-reference')
Assert (@(Test-HandoffResearch $root $researchOrder) -contains 'ResearchBindingNeedsReview') 'ResearchUnknownFormat'

# 기존 승인/연구/상태 판정은 실제 검증기를 통과해야 한다. 원본은 읽기만 한다.
& (Join-Path $root 'eng/tests/e7-native-work-order.ps1') | Out-Null
Assert $true 'ExistingNativeApprovalSubjectHashAndCapTests'

$manager = Join-Path $root 'eng/execution-ledgers/manage-development-handoff.ps1'
$goalId = 'interaction-goal:restaurant-cooking.v1'
$unityRoot = 'C:/Users/user/ssalddel'
$inputs = @('eng/execution-ledgers/subject-interaction-development.json', 'eng/execution-ledgers/codex-playable-loop-goals.json',
    'eng/execution-ledgers/work-orders/restaurant-cooking.e7-work-order.json')
$before = Get-HandoffHash @(Get-HandoffInventory $root '' $inputs)
$packet = & $manager -Mode Export -GoalId $goalId -UnityProjectRoot $unityRoot | ConvertFrom-Json
Assert ($packet.blockerCodes -contains 'OwnerMissing') 'NoInventedOwner'
Assert (@($packet.blockerCodes | Where-Object { $_ -like 'WriteOwnershipConflict:*' }).Count -gt 0) 'ActualSharedFileConflict'
Assert ($packet.blockerCodes -notcontains 'ResearchDeclarationMissing') 'ExplicitEmptyResearch'
$artifact = Join-Path $root 'artifacts/local/development-handoffs/interaction-goal-restaurant-cooking.v1/handoff.json'
$hash = (Get-FileHash -LiteralPath $artifact).Hash
& $manager -Mode Export -GoalId $goalId -UnityProjectRoot $unityRoot | Out-Null
Assert ($hash -eq (Get-FileHash -LiteralPath $artifact).Hash) 'ExportDeterministic'
Reject { & $manager -Mode Check -GoalId $goalId -UnityProjectRoot $unityRoot -BaselinePath $artifact } 'DevelopmentHandoffCheckBlocked'
Assert ($hash -eq (Get-FileHash -LiteralPath $artifact).Hash) 'CheckDoesNotRewriteBaseline'
Assert ($before -eq (Get-HandoffHash @(Get-HandoffInventory $root '' $inputs))) 'LedgersAndWorkOrderUnchanged'
Write-Output "DevelopmentHandoffTestsPassed:$script:passed;GameExecution=False;Promotion=False"
