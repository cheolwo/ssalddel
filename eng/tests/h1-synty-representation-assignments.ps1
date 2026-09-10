$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "H1SyntyRepresentationAssignmentTestFailed:$Code" }
}
function Invoke-ExpectedFailure([string] $LedgerPath, [string] $ExpectedCode) {
    try {
        & $manager -Mode Check -LedgerPath $LedgerPath -OutputPath 'docs/AI/generated/h1-synty-representation-assignments.md' -MachineOutputPath 'docs/AI/generated/h1-synty-representation-assignments.json' 2>&1 | Out-Null
        throw "ExpectedFailureMissing:$ExpectedCode"
    }
    catch {
        Require ($_.Exception.Message -like "*$ExpectedCode*") "UnexpectedFailure:${ExpectedCode}:$($_.Exception.Message)"
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manager = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-h1-synty-representation-assignments.ps1'
$ledgerPath = Join-Path $repositoryRoot 'eng/execution-ledgers/h1-synty-representation-assignments.json'
$unityRoot = 'C:/Users/user/ssalddel'

& $manager -Mode Check | Out-Null
$checks = 1
if (Test-Path -LiteralPath (Join-Path $unityRoot 'Assets/Synty') -PathType Container) {
    & $manager -Mode Check -UnityProjectRoot $unityRoot | Out-Null
    $checks++
    $beforeSurveyHash = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash
    & $manager -Mode Survey -UnityProjectRoot $unityRoot | Out-Null
    Require ((Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash -ceq $beforeSurveyHash) 'SurveyIsDeterministic'
    $checks++
}

$h1Query = & $manager -Mode Query -QueryKind H1 -QueryValue 'h1-stock:farm-residential-home' | ConvertFrom-Json
Require ([int] $h1Query.count -eq 1) 'FarmResidentialHomeQueryCount'
Require ([string] $h1Query.items[0].assignmentStatusCode -eq 'BlenderRequired') 'FarmResidentialHomeBlenderStatus'
Require (@($h1Query.items[0].representationSet).Count -gt 0) 'FarmResidentialHomeCandidateMissing'
$checks += 3

$h2Query = & $manager -Mode Query -QueryKind H2 -QueryValue 'h2-candidate:farm-harvest-throughput' | ConvertFrom-Json
Require ([int] $h2Query.count -eq 1) 'FarmH2QueryCount'
Require (-not [bool] $h2Query.items[0].directPrefabAssignmentAllowed) 'FarmH2DirectPrefabBoundary'
$checks += 2

$source = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('h1-synty-assignment-test-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
try {
    $duplicate = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $duplicate.h1Assignments = @($duplicate.h1Assignments) + @($duplicate.h1Assignments[0])
    $duplicatePath = Join-Path $temporaryDirectory 'duplicate.json'
    [IO.File]::WriteAllText($duplicatePath, ($duplicate | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectedFailure $duplicatePath 'H1AssignmentCoverageMismatch'
    $checks++

    $directH2 = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $directH2.h2Compositions[0].directPrefabAssignmentAllowed = $true
    $directH2Path = Join-Path $temporaryDirectory 'direct-h2.json'
    [IO.File]::WriteAllText($directH2Path, ($directH2 | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectedFailure $directH2Path 'H2DirectPrefabForbidden'
    $checks++

    $missingState = Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $target = @($missingState.h1Assignments | Where-Object { $_.stateTransition.statusCode -eq 'PendingStateContract' } | Select-Object -First 1)[0]
    $target.stateTransition.active = @()
    $missingStatePath = Join-Path $temporaryDirectory 'missing-state.json'
    [IO.File]::WriteAllText($missingStatePath, ($missingState | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectedFailure $missingStatePath 'StateTriadMissing'
    $checks++
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force }
}

Write-Output "H1SyntyRepresentationAssignmentTestsPassed:$checks"
