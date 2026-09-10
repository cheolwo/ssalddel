$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$checker = Join-Path $root 'world-seedbeds/manage-myeonmok-reference-maps.ps1'
$graphSource = Join-Path $root 'world-seedbeds/graph-maps/jungnang-myeonmok-reference.v1.json'
$placementSource = Join-Path $root 'world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v1.json'
$manifestV2 = 'eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v2.json'
$graphV2 = 'eng/world-seedbeds/graph-maps/jungnang-myeonmok-reference.v2.json'
$placementV2 = 'eng/world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v2.json'
$null = & $checker -Mode Check -SkipSourceFreshness
$v2 = & $checker -Mode Check -ManifestPath $manifestV2 -GraphPath $graphV2 -PlacementPath $placementV2 | ConvertFrom-Json
if ($v2.DioramaOverlayBuildings -ne 204 -or $v2.Revision -ne 'jungnang-myeonmok-reference.r2') { throw 'V2DioramaBaselineMismatch' }
$sourceFreshness = 'SkippedPrivateSourcesUnavailable'
try {
    $null = & $checker -Mode Check
    $sourceFreshness = 'FrozenSourcesVerified'
}
catch {
    if ($_.Exception.Message -notlike '*SourceBytesMissing:*') { throw }
}
$testRoot = Join-Path $root ('../artifacts/local/validation/myeonmok-reference-map-cases/' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot -Force
$cases = @(
    @{ Name='duplicate-node'; Error='DuplicateNode'; Change={ param($g,$p) $g.nodes += $g.nodes[0] } },
    @{ Name='missing-relation-node'; Error='RelationNodeMissing'; Change={ param($g,$p) $g.edges[0].toNodeRef='missing' } },
    @{ Name='missing-overlay'; Error='SyntheticOverlayRelationMissing'; Change={ param($g,$p) $g.edges=@($g.edges | Where-Object relationCode -ne 'ScenarioOverlayOf') } },
    @{ Name='missing-tile'; Error='TileSetMismatch'; Change={ param($g,$p) $p.instances=@($p.instances | Select-Object -Skip 1) } },
    @{ Name='invalid-tile-size'; Error='TileGeometryInvalid'; Change={ param($g,$p) $p.instances[0].width=499 } },
    @{ Name='summary-total'; Error='SummaryTotals'; Change={ param($g,$p) $p.tileSummaries[0].shopPointCandidates++ } },
    @{ Name='unplaced-total'; Error='AddressUnplacedTotals'; Change={ param($g,$p) $p.unplacedCatalog[0].count++ } },
    @{ Name='authority'; Error='AuthorityBoundary'; Change={ param($g,$p) $p.isOperationalState=$true } }
)
foreach ($case in $cases) {
    $graph = Get-Content -LiteralPath $graphSource -Raw -Encoding UTF8 | ConvertFrom-Json
    $placement = Get-Content -LiteralPath $placementSource -Raw -Encoding UTF8 | ConvertFrom-Json
    $graphPath = Join-Path $testRoot ($case.Name + '-graph.json')
    $placementPath = Join-Path $testRoot ($case.Name + '-placement.json')
    $graphRelative = [IO.Path]::GetRelativePath((Split-Path $root -Parent), $graphPath).Replace('\','/')
    $placementRelative = [IO.Path]::GetRelativePath((Split-Path $root -Parent), $placementPath).Replace('\','/')
    $graph.placementMapRef = $placementRelative
    $placement.graphMapRef = $graphRelative
    & $case.Change $graph $placement
    [IO.File]::WriteAllText($graphPath, (($graph | ConvertTo-Json -Depth 100) -replace "`r`n","`n") + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($placementPath, (($placement | ConvertTo-Json -Depth 100) -replace "`r`n","`n") + "`n", [Text.UTF8Encoding]::new($false))
    $caught = ''
    try { $null = & $checker -Mode Check -GraphPath $graphRelative -PlacementPath $placementRelative -SkipSourceFreshness }
    catch { $caught = $_.Exception.Message }
    if ($caught -notlike ('*' + $case.Error + '*')) { throw "CaseFailed:$($case.Name):$caught" }
    Write-Output "PASS $($case.Name)"
}
$v2Cases = @(
    @{ Name='missing-privacy-edge'; Error='DioramaGraphContract'; Change={ param($g,$p) $g.edges=@($g.edges | Where-Object relationCode -ne 'PrivacyProjectionOf') } },
    @{ Name='official-link-inflation'; Error='DioramaLayerSummary'; Change={ param($g,$p) ($p.sourceLayers | Where-Object nodeRef -eq 'layer:myeonmok:diorama-presentation').officialBuildingLinkCount=1 } }
)
foreach ($case in $v2Cases) {
    $graph = Get-Content -LiteralPath (Join-Path (Split-Path $root -Parent) $graphV2) -Raw -Encoding UTF8 | ConvertFrom-Json
    $placement = Get-Content -LiteralPath (Join-Path (Split-Path $root -Parent) $placementV2) -Raw -Encoding UTF8 | ConvertFrom-Json
    $graphPath = Join-Path $testRoot ($case.Name + '-graph.json')
    $placementPath = Join-Path $testRoot ($case.Name + '-placement.json')
    $graphRelative = [IO.Path]::GetRelativePath((Split-Path $root -Parent), $graphPath).Replace('\','/')
    $placementRelative = [IO.Path]::GetRelativePath((Split-Path $root -Parent), $placementPath).Replace('\','/')
    $graph.placementMapRef = $placementRelative
    $placement.graphMapRef = $graphRelative
    & $case.Change $graph $placement
    [IO.File]::WriteAllText($graphPath, (($graph | ConvertTo-Json -Depth 100) -replace "`r`n","`n") + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($placementPath, (($placement | ConvertTo-Json -Depth 100) -replace "`r`n","`n") + "`n", [Text.UTF8Encoding]::new($false))
    $caught = ''
    try { $null = & $checker -Mode Check -ManifestPath $manifestV2 -GraphPath $graphRelative -PlacementPath $placementRelative -SkipSourceFreshness }
    catch { $caught = $_.Exception.Message }
    if ($caught -notlike ('*' + $case.Error + '*')) { throw "CaseFailed:$($case.Name):$caught" }
    Write-Output "PASS $($case.Name)"
}
Write-Output "PASS v1 baseline + v2 diorama baseline + 10 rejection cases; sourceFreshness=$sourceFreshness; no DB, Scene or Unity changes"
