$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$checker=Join-Path $root 'eng/world-seedbeds/manage-neighborhood-maps.ps1'
$graphSource=Join-Path $root 'eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json'
$layoutSource=Join-Path $root 'eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json'
$null=& $checker -Mode Check
$testRoot='artifacts/local/validation/neighborhood-map-cases/'+[Guid]::NewGuid().ToString('N')
$null=New-Item -ItemType Directory -Path (Join-Path $root $testRoot) -Force
$cases=@(
    @{Name='duplicate-node'; Error='DuplicateNode'; Change={param($g,$p) $g.nodes+= $g.nodes[0]}},
    @{Name='missing-node'; Error='RelationNodeMissing'; Change={param($g,$p) $g.edges[0].toNodeRef='missing'}},
    @{Name='duplicate-actor'; Error='DuplicateActor'; Change={param($g,$p) $p.actors+= $p.actors[0]}},
    @{Name='missing-anchor'; Error='PathAnchorMissing'; Change={param($g,$p) $p.paths[0].anchorRefs[0]='missing'}},
    @{Name='broken-work-access'; Error='WorkAccessMissing'; Change={param($g,$p) $p.paths=@($p.paths | Where-Object id -ne 'restaurant-exit')}},
    @{Name='facility-overlap'; Error='FacilityOverlap'; Change={param($g,$p) $p.instances[1].x=$p.instances[2].x}},
    @{Name='undocumented-outside'; Error='UndocumentedOutsideAnchor'; Change={param($g,$p) $p.anchors[0].x=500}},
    @{Name='stale-code'; Error='GeneratedCodeStale'; Change={param($g,$p) $p.anchors[1].z=3.9}}
)
foreach($case in $cases) {
    $g=Get-Content -LiteralPath $graphSource -Raw -Encoding UTF8 | ConvertFrom-Json
    $p=Get-Content -LiteralPath $layoutSource -Raw -Encoding UTF8 | ConvertFrom-Json
    $gp=$testRoot+'/'+$case.Name+'-graph.json'; $pp=$testRoot+'/'+$case.Name+'-placement.json'
    $g.placementMapRef=$pp;$p.graphMapRef=$gp
    & $case.Change $g $p
    [IO.File]::WriteAllText((Join-Path $root $gp),($g | ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $root $pp),($p | ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
    $caught=''
    try { $null=& $checker -Mode Check -GraphPath $gp -PlacementPath $pp }
    catch { $caught=$_.Exception.Message }
    if($caught -notlike ('*'+$case.Error+'*')) { throw "CaseFailed:$($case.Name):$caught" }
    Write-Output "PASS $($case.Name)"
}
Write-Output 'PASS baseline + 8 rejection cases; no Scene changes'
