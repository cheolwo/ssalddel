[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../.." )).Path

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "ForestEdgeLivingFarmDefenseH4E4Invalid:$Code" }
}

function Read-Json([string] $RelativePath) {
    $path = Join-Path $repositoryRoot ($RelativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
    Require (Test-Path -LiteralPath $path) "Missing:$RelativePath"
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

$h4 = Read-Json "eng/world-seedbeds/placement-map-profiles/forest-edge-living-farm-defense-region.h4-e4.v1.json"
$instance = Read-Json "eng/world-seedbeds/placement-map-profiles/forest-edge-farm-hans-living-farm.v1.json"
$graph = Read-Json "eng/world-seedbeds/graph-maps/northern-life-hub-discovery.v1.json"

Require ([string] $h4.schemaVersion -eq "simulation-world-h4-e4-preparation-profile.v1") "Schema"
Require ([string] $h4.targetH4BlueprintStableId -eq [string] $instance.targetH4BlueprintStableId) "H4TargetMismatch"
Require (@($h4.requiredH3Roles).Count -eq 3) "H3RoleCount"
Require (@($h4.requiredH3Roles.roleCode | Sort-Object -Unique).Count -eq 3) "H3RoleDuplicate"
Require ((@($h4.requiredH3Roles.roleCode | Sort-Object) -join ",") -eq "AccessDefense,DamageRecoveryReturn,LivingProduction") "H3RoleCoverage"

$catalog = Read-Json "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json"
$knownH3 = @($catalog.h3DefinitionRefs.stableId)
foreach ($role in @($h4.requiredH3Roles)) {
    Require ($knownH3 -contains [string] $role.h3DefinitionRef) "H3Missing:$($role.h3DefinitionRef)"
}

$routeCodes = @($instance.graphRouteBindings.routeCode)
Require (@($routeCodes | Sort-Object -Unique).Count -eq 5) "RouteDuplicate"
foreach ($requiredRoute in @($h4.requiredGraphRouteCodes)) {
    Require ($routeCodes -contains [string] $requiredRoute) "RouteMissing:$requiredRoute"
}

$nodeIds = @($graph.level1.nodes.nodeId)
foreach ($route in @($instance.graphRouteBindings)) {
    foreach ($graphRef in @($route.graphRefs)) {
        Require ($nodeIds -contains [string] $graphRef) "GraphNodeMissing:$graphRef"
    }
}

$conditional = @($h4.conditionalRouteRules | Where-Object routeCode -eq "EnemyConditionalBreach")
Require ($conditional.Count -eq 1) "ConditionalBreachRule"
Require ([string] $conditional[0].fallbackRouteCode -eq "EnemyPrimaryIngress") "ConditionalBreachFallback"
Require ([string] $instance.h4AuthoringReview.unapprovedH1RepresentationCode -eq "TranslucentPlaceholder") "PlaceholderPolicy"
Require ([bool] $instance.h4AuthoringReview.unityGroundingRecalculationRequired) "UnityGrounding"
Require (-not [bool] $h4.isOperationalState) "OperationalAuthorityForbidden"

Write-Output "ForestEdgeLivingFarmDefenseH4E4Passed:H3=3;Routes=5;ConditionalBreach=1;E5=0"
