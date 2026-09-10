$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$catalogPath = Join-Path $repositoryRoot 'eng/execution-ledgers/hex03-h-visual-catalog.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$managerPath = Join-Path $repositoryRoot 'eng/execution-ledgers/manage-hex03-h-visual-catalog.ps1'

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "Hex03HVisualCatalogInvalid:$Code" }
}

Require ([string] $catalog.schemaVersion -eq 'mirror-hex03-h-visual-catalog.v1') 'SchemaVersion'
Require ([string] $catalog.campaignPlanId -eq 'PLAN-STORY-HEX03-CAMPAIGN-001') 'Campaign'
Require ([string] $catalog.evidenceBoundary -eq 'PresentationE4Only') 'EvidenceBoundary'
Require (@($catalog.items).Count -eq 9) 'ItemCount'

$ids = @($catalog.items | ForEach-Object { [string] $_.hStableId })
Require (@($ids | Select-Object -Unique).Count -eq $ids.Count) 'DuplicateHStableId'

$lastLevel = 0
foreach ($item in @($catalog.items)) {
    $level = [int](([string] $item.levelCode).Substring(1))
    Require ($level -ge $lastLevel) "HierarchyOrder:$($item.hStableId)"
    $lastLevel = $level

    if (-not [string]::IsNullOrWhiteSpace([string] $item.repoImageRef)) {
        $imagePath = Join-Path $repositoryRoot ([string] $item.repoImageRef)
        Require (Test-Path -LiteralPath $imagePath -PathType Leaf) "ImageMissing:$($item.hStableId)"
        $actualHash = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash
        Require ($actualHash -eq [string] $item.repoImageSha256) "ImageHashMismatch:$($item.hStableId)"
    }
}

Require ([bool] $catalog.principles.blenderOrImageDoesNotProveUnityE5) 'UnityE5Boundary'
Require ([bool] $catalog.principles.onlyH1VisualsCurrentlyAccepted) 'H1OnlyVisualBoundary'
$activeVisualItems = @($catalog.items | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_.repoImageRef) })
Require ($activeVisualItems.Count -eq 3) 'ActiveVisualCount'
Require (@($activeVisualItems | Where-Object levelCode -ne 'H1').Count -eq 0) 'HigherLevelVisualLeaked'
Require (@($catalog.items | Where-Object { $_.levelCode -in @('H2', 'H3') -and $_.statusCode -ne 'VisualUndecided' }).Count -eq 0) 'HigherLevelVisualStatus'

$graphPath = Join-Path $repositoryRoot 'eng/world-seedbeds/graph-maps/hans-farm-hex03-campaign.v1.json'
$placementPath = Join-Path $repositoryRoot 'eng/world-seedbeds/placement-map-profiles/hans-farm-hex03-development-handoff.v1.json'
$graph = Get-Content -LiteralPath $graphPath -Raw -Encoding UTF8 | ConvertFrom-Json
$placement = Get-Content -LiteralPath $placementPath -Raw -Encoding UTF8 | ConvertFrom-Json
Require (@($graph.lineBindings).Count -eq 6) 'GraphLineBindingCount'
Require (@($graph.nodes.nodeStableId | Select-Object -Unique).Count -eq @($graph.nodes).Count) 'GraphNodeDuplicate'
Require (@($graph.edges.edgeStableId | Select-Object -Unique).Count -eq @($graph.edges).Count) 'GraphEdgeDuplicate'
Require (-not [bool] $graph.validationBoundary.unityVerified) 'GraphUnityBoundary'
Require ([string] $placement.graphMapRef -eq 'eng/world-seedbeds/graph-maps/hans-farm-hex03-campaign.v1.json') 'PlacementGraphRef'
Require (@($placement.requiredHRefs).Count -eq 9) 'PlacementHCount'
Require (@($placement.acceptedVisualHRefs).Count -eq 3) 'PlacementAcceptedVisualCount'
Require (@($placement.pendingVisualHRefs).Count -eq 4) 'PlacementPendingVisualCount'
Require ([string] $placement.stateCode -eq 'H1VisualCandidatesOnly_H2H3Undecided') 'PlacementVisualState'
Require ([bool] $placement.presentationOnly -and -not [bool] $placement.isOperationalState) 'PlacementAuthorityBoundary'

& $managerPath -Mode Check | Out-Null

Write-Output 'Hex03HVisualCatalogTests:OK:Items=9:PortableImages=3:H2H3VisualUndecided:GraphLines=6:PlacementH=9'
