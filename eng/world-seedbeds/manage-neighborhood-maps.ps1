[CmdletBinding()]
param(
    [ValidateSet('Check','Write')][string] $Mode = 'Check',
    [string] $GraphPath = 'eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json',
    [string] $PlacementPath = 'eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'GraphMapTooling.ps1') -RepositoryRoot $root -ErrorPrefix 'NeighborhoodMapInvalid'
$graph = Read-Json $GraphPath
$layout = Read-Json $PlacementPath
Require ($graph.schemaVersion -eq 'simulation-world-graph-map.v1') 'GraphSchema'
Require ($layout.schemaVersion -eq 'simulation-world-placement-map-preparation-profile.v1') 'PlacementSchema'
Require ($graph.placementMapRef -eq $PlacementPath -and $layout.graphMapRef -eq $GraphPath) 'CrossReference'
Require (Test-Path -LiteralPath (Resolve-RepoPath $graph.planningRef)) 'PlanningMissing'
Require ($graph.planningRef -eq $layout.planningRef) 'PlanningMismatch'
Require-Unique @($graph.nodes) { param($x) $x.nodeStableId } 'DuplicateNode'
Require-Unique @($graph.edges) { param($x) $x.edgeStableId } 'DuplicateRelation'
Require-Unique @($layout.instances) { param($x) $x.id } 'DuplicateInstance'
Require-Unique @($layout.anchors) { param($x) $x.id } 'DuplicateAnchor'
Require-Unique @($layout.actors) { param($x) $x.actorId } 'DuplicateActor'
Require-Unique @($layout.paths) { param($x) $x.id } 'DuplicatePath'
$nodes = @{}; foreach ($n in $graph.nodes) { $nodes[$n.nodeStableId] = $n }
$anchors = @{}; foreach ($a in $layout.anchors) { $anchors[$a.id] = $a }
foreach ($edge in $graph.edges) { Require ($nodes.ContainsKey($edge.fromNodeRef) -and $nodes.ContainsKey($edge.toNodeRef)) 'RelationNodeMissing' }
function Number([object] $value) {
    $n = [double]::Parse([string]$value,[Globalization.CultureInfo]::InvariantCulture)
    Require (-not [double]::IsNaN($n) -and -not [double]::IsInfinity($n)) 'NonFiniteCoordinate'
    return $n.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
}
foreach ($item in @($layout.instances) + @($layout.anchors)) {
    Require ($item.id -cmatch '^[a-zA-Z0-9:._-]+$') 'UnsafeIdentifier'
    $null = Number $item.x; $null = Number $item.z
}
foreach ($instance in $layout.instances) {
    Require ($nodes.ContainsKey($instance.nodeRef)) 'InstanceNodeMissing'
    Require ([double]$instance.width -gt 0 -and [double]$instance.depth -gt 0) 'InvalidBounds'
}
foreach ($anchor in $layout.anchors) { Require ($nodes.ContainsKey($anchor.ownerRef)) 'AnchorOwnerMissing' }
$adj = @{}; foreach($a in $layout.anchors) { $adj[$a.id] = [Collections.Generic.List[string]]::new() }
foreach ($path in $layout.paths) {
    Require ($path.id -cmatch '^[a-zA-Z0-9:._-]+$') 'UnsafePathIdentifier'
    Require (@($path.anchorRefs).Count -ge 2) 'EmptyPath'
    foreach ($id in $path.anchorRefs) { Require ($anchors.ContainsKey($id)) 'PathAnchorMissing' }
    for($i=1;$i -lt $path.anchorRefs.Count;$i++) {
        $a=$path.anchorRefs[$i-1]; $b=$path.anchorRefs[$i]
        $adj[$a].Add($b); if($path.bidirectional) { $adj[$b].Add($a) }
    }
}
foreach ($actor in $layout.actors) {
    Require ($nodes.ContainsKey($actor.actorId) -and $nodes[$actor.actorId].nodeKindCode -eq 'Subject') 'ActorNodeMissing'
    Require ($nodes.ContainsKey($actor.workNodeRef) -and $anchors.ContainsKey($actor.initialAnchorRef)) 'ActorBindingMissing'
    if($actor.homeNodeRef) { Require ($nodes.ContainsKey($actor.homeNodeRef)) 'HomeNodeMissing' }
    if($actor.role -eq 'Resident') { continue }
    $seen=[Collections.Generic.HashSet[string]]::new(); $pending=[Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($actor.initialAnchorRef)
    while($pending.Count) { $id=$pending.Dequeue(); if(!$seen.Add($id)){continue}; foreach($next in $adj[$id]) {$pending.Enqueue($next)} }
    Require ($seen.Contains('junction-20')) "WorkAccessMissing:$($actor.actorId)"
    if($actor.homeNodeRef) { $s=([string]$actor.homeNodeRef).Substring(([string]$actor.homeNodeRef).Length-1); Require ($seen.Contains("home-$s-receive")) "HomeAccessMissing:$($actor.actorId)" }
}
$warnings=[Collections.Generic.List[string]]::new()
$facilities=@($layout.instances | Where-Object kind -eq 'Facility')
for($i=0;$i -lt $facilities.Count;$i++) { for($j=$i+1;$j -lt $facilities.Count;$j++) {
    $a=$facilities[$i];$b=$facilities[$j]
    Require (-not ([Math]::Abs($a.x-$b.x) -lt ($a.width+$b.width)/2 -and [Math]::Abs($a.z-$b.z) -lt ($a.depth+$b.depth)/2)) "FacilityOverlap:$($a.id):$($b.id)"
} }
foreach($a in $layout.anchors | Where-Object {$_.role -in @('Work','Inbound')}) {
    $f=$facilities | Where-Object id -eq $a.ownerRef
    if($f -and ([Math]::Abs($a.x-$f.x) -gt $f.width/2 -or [Math]::Abs($a.z-$f.z) -gt $f.depth/2)) {
        Require (@($layout.knownGaps | Where-Object {$a.id -in $_.anchorRefs}).Count -gt 0) "UndocumentedOutsideAnchor:$($a.id)"
        $warnings.Add("OutsideFootprint:$($a.id)")
    }
}
# 형식이 다른 기존 전역 맵 생성기에 억지로 편입하지 않고 공유 경로/JSON/중복 검사만 재사용한다.
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('// Generated by eng/world-seedbeds/manage-neighborhood-maps.ps1. Do not edit.')
$lines.Add('// 기존 합성 XZ 배치 기준. 실제 World 배치 승인이나 운영 상태가 아니다.')
$lines.Add('using System;')
$lines.Add('namespace Ssalddel.Simulation.Contracts { public static class 가상동네배치기준 {')
Require ($graph.revision -cmatch '^[a-zA-Z0-9:._-]+$') 'UnsafeRevision'
$lines.Add('public const string Revision = "'+$graph.revision+'";')
$lines.Add('public static (double x, double z) 기준점(string id) { switch(id) {')
foreach($a in $layout.anchors) { $lines.Add('case "'+$a.id+'": return ('+(Number $a.x)+'d,'+(Number $a.z)+'d);') }
$lines.Add('default: throw new ArgumentException("NeighborhoodAnchorMissing", nameof(id)); } }')
$lines.Add('public static (double x, double z, double width, double depth) 시설(string id) { switch(id) {')
foreach($a in $layout.instances) { $lines.Add('case "'+$a.id+'": return ('+(Number $a.x)+'d,'+(Number $a.z)+'d,'+(Number $a.width)+'d,'+(Number $a.depth)+'d);') }
$lines.Add('default: throw new ArgumentException("NeighborhoodInstanceMissing", nameof(id)); } }')
$lines.Add('public static (double x, double z)[] 대기점() => new[] { 기준점("waiting-0"),기준점("waiting-1"),기준점("waiting-2"),기준점("waiting-3"),기준점("waiting-4") };')
$lines.Add('public static (double x, double z)[] 경로(string id) { switch(id) {')
foreach($p in $layout.paths) { $items=@($p.anchorRefs | ForEach-Object {'기준점("'+$_+'")'}); $lines.Add('case "'+$p.id+'": return new[] { '+($items -join ',')+' };') }
$lines.Add('default: throw new ArgumentException("NeighborhoodPathMissing", nameof(id)); } }')
$lines.Add('} }')
$extension = Read-Json 'eng/world-seedbeds/placement-map-profiles/neighborhood-market-extension.v1.json'
. (Join-Path $PSScriptRoot 'NeighborhoodGeographicProjection.ps1')
$points = @(Get-NeighborhoodMarketPoints $extension)
Require ($extension.baseProfileRef -eq 'eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json') 'ExtensionBase'
foreach($f in $layout.instances) {
    Require (-not ([Math]::Abs($f.x-$extension.offsetX) -lt $f.width/2+$extension.halfExtentMeters -and [Math]::Abs($f.z-$extension.offsetZ) -lt $f.depth/2+$extension.halfExtentMeters)) 'ExtensionOverlapsBase'
}
$lines.Add('namespace Ssalddel.Simulation.Contracts { public static class 동네좌표참고확장 {')
$lines.Add('public const string 원본Hash = "'+$extension.rawSha256+'";')
$lines.Add('public static (double x, double z, double size) 구역 => ('+(Number $extension.offsetX)+'d,'+(Number $extension.offsetZ)+'d,'+(Number ($extension.halfExtentMeters*2))+'d);')
$lines.Add('public static (string id, string name, double x, double z)[] 위치() => new (string, string, double, double)[] {')
foreach($point in $points) {
    $name=([string]$point.name).Replace('\','\\').Replace('"','\"').Replace("`r",'\r').Replace("`n",'\n')
    $lines.Add('("'+$point.id+'","'+$name+'",'+(Number $point.x)+'d,'+(Number $point.z)+'d),')
}
$lines.Add('}; } }')
$expected=Normalize-Text ($lines -join "`n")
$output=Resolve-RepoPath 'Ssalddel.Simulation.Contracts/UnityPackage/Runtime/가상동네배치기준.generated.cs'
if($Mode -eq 'Write') { [IO.File]::WriteAllText($output,$expected,[Text.UTF8Encoding]::new($false)) }
else { Require (Test-Path -LiteralPath $output) 'GeneratedCodeMissing'; Require ((Normalize-Text (Get-Content -LiteralPath $output -Raw -Encoding UTF8)) -ceq $expected) 'GeneratedCodeStale' }
[pscustomobject]@{ Status='CodeBaselineChecked'; Nodes=$graph.nodes.Count; Relations=$graph.edges.Count; Instances=$layout.instances.Count; Anchors=$layout.anchors.Count; Paths=$layout.paths.Count; Actors=$layout.actors.Count; PlacementHash=(File-Hash $PlacementPath); Warnings=@($warnings); SceneReady=$false; OpenGaps=@($layout.knownGaps.id); Mode=$Mode } | ConvertTo-Json -Depth 4
