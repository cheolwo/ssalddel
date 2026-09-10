$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$rulePath = Join-Path $repositoryRoot "eng/world-seedbeds/placement-map-profiles/farm-cultivation-plot-seed-alignment.v1.json"
$h1CatalogPath = Join-Path $repositoryRoot "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json"
$planningPath = Join-Path $repositoryRoot "docs/AI/Planning/표현/PLAN-PRESENTATION-H1-SYNTY-STATE-001/README.md"

function Require([bool] $condition, [string] $code) {
    if (-not $condition) { throw "FarmCultivationPlotSeedAlignmentInvalid:$code" }
}

$rule = Get-Content -LiteralPath $rulePath -Raw -Encoding UTF8 | ConvertFrom-Json
$catalog = Get-Content -LiteralPath $h1CatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$planning = Get-Content -LiteralPath $planningPath -Raw -Encoding UTF8

Require ([string] $rule.schemaVersion -eq "simulation-world-h1-placement-rule.v1") "Schema"
Require ([string] $rule.ruleStableId -eq "placement-rule:farm:cultivation-plot-seed-center.v1") "StableId"
Require ([string] $rule.targetH1Ref -eq "h1-stock:farm-production") "H1Ref"
Require (@($catalog.h1InteractionDefinitionRefs.stableId) -contains [string] $rule.targetH1Ref) "UnknownH1"
Require ([double] $rule.plotDimensionsMeters.width -eq 2.5 -and [double] $rule.plotDimensionsMeters.length -eq 2.5) "Dimensions"
Require ([double] $rule.primarySeedAnchor.u -eq 0.5 -and [double] $rule.primarySeedAnchor.v -eq 0.5) "CenterAnchor"
Require ([bool] $rule.alignmentRules.snapToNearestFurrowCenterline) "FurrowSnap"
Require ([string] $rule.alignmentRules.multiSeedExpansionCode -eq "SymmetricFromCenterAlongFurrow") "Expansion"
Require (-not [bool] $rule.alignmentRules.directEdgePlacementAllowed) "EdgePlacement"
Require (-not [bool] $rule.alignmentRules.outsidePlotPlacementAllowed) "OutsidePlacement"
Require ([string] $rule.validation.stateCode -eq "ApprovedPlanningRule_ImplementationNotBound") "ImplementationBoundary"
Require ([string] $rule.validation.requiredAtPresentationEvidence -eq "E4") "EvidenceBoundary"
Require ($planning.Contains([string] $rule.ruleStableId)) "PlanningTrace"

Write-Output "FarmCultivationPlotSeedAlignmentTestsPassed:Checks=13;Unity=0;E5=0"
