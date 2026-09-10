$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manager = Join-Path $repositoryRoot `
    "eng/execution-ledgers/manage-e7-vertical-work-order.ps1"
$workOrderRef = `
    "eng/execution-ledgers/work-orders/actor-item-equipment.e7-work-order.json"
$workOrder = Get-Content -LiteralPath (Join-Path $repositoryRoot $workOrderRef) `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$worldInteractionId = [string] $workOrder.activeWorldInteractionId
$classificationOutputPath = Join-Path $repositoryRoot `
    "docs/AI/generated/world-interaction-gwae-classifications.json"
$artifactDirectory = Join-Path $repositoryRoot `
    "artifacts/local/validation/e7-role-object-action-gate"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

function Write-Fixture([object] $Fixture, [string] $Name) {
    $path = Join-Path $artifactDirectory $Name
    [IO.File]::WriteAllText($path, ($Fixture | ConvertTo-Json -Depth 30),
        [Text.UTF8Encoding]::new($false))
    return $path.Substring($repositoryRoot.Length + 1).Replace('\', '/')
}

function Require-Rejection([string] $FixturePath, [string] $ExpectedCode) {
    $rejected = $false
    try {
        & $manager -InputPath $workOrderRef `
            -GwaeClassificationPath $FixturePath | Out-Null
    }
    catch {
        $rejected = $_.Exception.Message.Contains($ExpectedCode)
    }
    if (-not $rejected) { throw "E7RoleObjectActionGateAccepted:$ExpectedCode" }
}

$validResult = & $manager -InputPath $workOrderRef
if ([string] $validResult -notmatch '^E7VerticalWorkOrderValid:') {
    throw "E7RoleObjectActionValidClassificationRejected:$validResult"
}
$preE5Result = & $manager `
    -GwaeClassificationPath "artifacts/local/validation/e7-role-object-action-gate/not-created.json"
if ([string] $preE5Result -notmatch '^E7VerticalWorkOrderValid:E7-WO-TEMPLATE;') {
    throw "E7RoleObjectActionGateAppliedBeforeE5:$preE5Result"
}

$missingClassification = Get-Content -LiteralPath $classificationOutputPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$missingClassification.items = @($missingClassification.items | Where-Object {
    [string] $_.wiId -ne $worldInteractionId
})
$missingClassificationRef = Write-Fixture $missingClassification `
    "role-object-action-classification-missing.json"
Require-Rejection $missingClassificationRef `
    "RoleObjectActionClassificationMissing:$worldInteractionId"

$missingBinding = Get-Content -LiteralPath $classificationOutputPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$missingBindingItem = @($missingBinding.items | Where-Object {
    [string] $_.wiId -eq $worldInteractionId
})
if ($missingBindingItem.Count -ne 1) { throw "E7RoleObjectActionFixtureMissing" }
$missingBindingItem[0].e5RoleObjectActionGate.PSObject.Properties.Remove("actionCode")
$missingBindingRef = Write-Fixture $missingBinding `
    "role-object-action-binding-missing.json"
Require-Rejection $missingBindingRef `
    "RoleObjectActionBindingFieldMissing:${worldInteractionId}:actionCode"

$staleClassification = Get-Content -LiteralPath $classificationOutputPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$staleClassification.worldInteractionCatalogRevision = "stale-test-revision"
$staleClassificationRef = Write-Fixture $staleClassification `
    "role-object-action-classification-stale.json"
Require-Rejection $staleClassificationRef `
    "RoleObjectActionClassificationStale"

$staleSourceClassification = Get-Content -LiteralPath $classificationOutputPath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$staleSourceClassification.sourceRevision = "stale-source-revision"
$staleSourceClassificationRef = Write-Fixture $staleSourceClassification `
    "role-object-action-classification-source-stale.json"
Require-Rejection $staleSourceClassificationRef `
    "RoleObjectActionClassificationSourceStale"

Write-Output "E7RoleObjectActionGateTestsPassed:Valid=2;Rejected=4"
