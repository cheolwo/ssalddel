$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$profilePath = Join-Path $repositoryRoot "eng/world-seedbeds/placement-map-profiles/forest-edge-farm-hans-living-farm.v1.json"
$planningPath = Join-Path $repositoryRoot "docs/AI/숲경계농장-H1-H2-배치맵-기획-2026-09-02.md"
$functionalCatalogPath = Join-Path $repositoryRoot "eng/execution-ledgers/synty-asset-functional-modules.json"
$loopCatalogPath = Join-Path $repositoryRoot "eng/execution-ledgers/playable-loop-synty-expression-modules.json"
$spatialCatalogPath = Join-Path $repositoryRoot "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json"

function Require([bool] $condition, [string] $code) {
    if (-not $condition) { throw "ForestEdgeFarmAssetSurveyInvalid:$code" }
}

function Require-Unique([object[]] $items, [scriptblock] $selector, [string] $code) {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $items) {
        $value = [string] (& $selector $item)
        Require (-not [string]::IsNullOrWhiteSpace($value)) "$code`:Empty"
        Require ($seen.Add($value)) "$code`:Duplicate:$value"
    }
}

$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
$graphPath = Join-Path $repositoryRoot ([string] $profile.graphMapRef)
Require (Test-Path -LiteralPath $graphPath) "GraphMapMissing"
$graph = Get-Content -LiteralPath $graphPath -Raw -Encoding UTF8 | ConvertFrom-Json
$functionalCatalog = Get-Content -LiteralPath $functionalCatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$loopCatalog = Get-Content -LiteralPath $loopCatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$spatialCatalog = Get-Content -LiteralPath $spatialCatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $profile.schemaVersion -eq "simulation-world-placement-map-preparation-profile.v1") "Schema"
Require ([int] $profile.revision -eq 5) "Revision"
Require ([string] $profile.planningRevision -eq "forest-edge-farm-placement-map-planning.r25") "PlanningRevision"
Require ([string] $profile.planningSha256 -eq "4DE86716F33D5512EC5BFDB1710640DB86AF66A80A4AD9D82B9FCD3A724C5373") "PlanningHash"
Require ((Get-FileHash -Algorithm SHA256 -LiteralPath $planningPath).Hash -eq [string] $profile.planningSha256) "PlanningFreshness"
Require ([int] $profile.lineage.previousProfileRevision -eq 4) "PreviousProfileRevision"
Require ([string] $profile.lineage.previousPlanningRevision -eq "forest-edge-farm-placement-map-planning.r24") "PreviousPlanningRevision"
Require ([string] $profile.lineage.previousPlanningSha256 -eq "71DC5B47F34F23E5FC5119650C18EA8C699D92729EBF61569F13C18A89C27113") "PreviousPlanningHash"
Require ([string] $profile.lineage.approvalReportSha256 -eq "302A5DF82F5C1B7E0C1267184A144349DAE6556F6A08628401F0115E88CA3805") "ApprovalReportHash"
Require ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $repositoryRoot ([string] $profile.lineage.approvalReportRef))).Hash -eq [string] $profile.lineage.approvalReportSha256) "ApprovalReportFreshness"
Require ([string] $profile.primarySyntyPackCode -eq "Farm") "PrimaryPack"
Require ([string] $profile.firstImpressionCode -eq "AgedButOperatingLivingFarm") "FirstImpression"
Require ([string] $profile.validationResultCode -eq "Blocked") "ValidationBoundary"
Require (-not [bool] $profile.assetSurveyBoundary.inventoryMatchIsAssignment) "InventoryAssignmentBoundary"
Require ([bool] $profile.assetSurveyBoundary.visualReviewCompleted) "VisualReviewBoundary"
Require ([bool] $profile.assetSurveyBoundary.staticCompatibilityVerified) "StaticCompatibilityBoundary"
Require (-not [bool] $profile.assetSurveyBoundary.unityVerified) "UnityBoundary"
Require (-not [bool] $profile.assetSurveyBoundary.automaticPrefabAssignment) "AutomaticAssignmentBoundary"
Require (-not [bool] $profile.assetSurveyBoundary.syntySourceModificationAllowed) "SyntySourceBoundary"
Require ([bool] $profile.assetSurveyBoundary.blenderExecutionApproved) "BlenderExecutionBoundary"
Require ([string] $profile.candidateBinding.stateCode -eq "UnapprovedCandidate") "CandidateState"
Require ([string] $profile.candidateBinding.visualApprovalStateCode -eq "Pending") "CandidateVisualApproval"
Require (-not [bool] $profile.candidateBinding.unitySceneValidated) "CandidateUnityBoundary"
Require ([string] $profile.targetH4RegistrationStateCode -eq "E4PreparationOnly") "NoEvidencePromotion"
Require ([string] $graph.schemaVersion -eq "simulation-world-graph-map.v1") "GraphSchema"
Require ([string] $graph.revision -eq "hans-farm-hex03-campaign.r2") "GraphRevision"
Require ([string] $graph.placementMapBinding.stateCode -eq "UnapprovedCandidate") "GraphCandidateState"
Require ([string] $graph.placementMapBinding.visualApprovalStateCode -eq "Pending") "GraphVisualApproval"
Require (-not [bool] $graph.placementMapBinding.unitySceneValidated) "GraphUnityBoundary"
Require ([string] $graph.placementMapBinding.planningRevision -eq [string] $profile.planningRevision) "GraphPlanningRevision"
Require ([string] $graph.placementMapBinding.planningSha256 -eq [string] $profile.planningSha256) "GraphPlanningHash"
Require ([string] $graph.lineage.previousRevision -eq "hans-farm-hex03-campaign.r1") "GraphPreviousRevision"
Require ([string] $graph.lineage.previousFileSha256 -eq "635BFCE36938BB6455CF3FD86FB7BF23096DCB5D0B2029FBFF75863A2C2E27AE") "GraphPreviousHash"

$supportPacks = @($profile.supportPackPolicies)
Require-Unique $supportPacks { param($x) $x.packCode } "SupportPack"
Require (($supportPacks.packCode -join ",") -eq "Construction,Nature,AlpineMountain") "SupportPackOrder"
Require (@($supportPacks | Where-Object packCode -eq "Construction" | Where-Object supportRoleCode -eq "DamageRepairRecoveryState").Count -eq 1) "ConstructionRole"
Require (@($supportPacks | Where-Object packCode -eq "Nature" | Where-Object supportRoleCode -eq "ForestBoundaryBuffer").Count -eq 1) "NatureRole"
Require (@($supportPacks | Where-Object packCode -eq "AlpineMountain" | Where-Object supportRoleCode -eq "OptionalNorthernDistantTerrain").Count -eq 1) "AlpineRole"

$instances = @($profile.requiredPlacementInstances)
Require ($instances.Count -eq 5) "InstanceCount"
Require-Unique $instances { param($x) $x.placementInstanceId } "PlacementInstance"
$expectedInstanceIds = @(
    "placement-instance:forest-edge-farm:hans-residential-home",
    "placement-instance:forest-edge-farm:mixed-crop-field",
    "placement-instance:forest-edge-farm:damaged-fence-entry",
    "placement-instance:forest-edge-farm:barn-work-yard",
    "placement-instance:forest-edge-farm:forest-buffer"
)
Require (($instances.placementInstanceId -join ",") -eq ($expectedInstanceIds -join ",")) "InstanceOrder"

$knownModules = @($functionalCatalog.functionalModules.moduleCode)
$knownPacks = @($functionalCatalog.sourcePacks.packCode)
$knownFamilies = @()
$knownLoopModules = @($loopCatalog.loopModules.moduleStableId)
foreach ($module in @($loopCatalog.loopModules)) {
    foreach ($slot in @($module.slots)) { $knownFamilies += @($slot.assetFamilyIds) }
}
$knownFamilies = @($knownFamilies | Sort-Object -Unique)
$knownSpatialRefs = @(
    $spatialCatalog.h1InteractionDefinitionRefs.stableId +
    $spatialCatalog.h1ExpressionDefinitionRefs.stableId +
    $spatialCatalog.h2DefinitionRefs.stableId
)
$knownGraphNodes = @($graph.nodes.nodeStableId)

foreach ($instance in $instances) {
    Require ([string] $instance.requirementCode -eq "RequiredForThisProfile") "Requirement:$($instance.placementInstanceId)"
    Require (@($instance.relativePlacementIntent).Count -gt 0) "PlacementIntent:$($instance.placementInstanceId)"
    Require ($knownSpatialRefs -contains [string] $instance.hDefinitionRef) "HRef:$($instance.placementInstanceId)"
    if ([string] $instance.graphBindingStateCode -eq "ExistingNode") {
        Require ($knownGraphNodes -contains [string] $instance.graphNodeRef) "GraphNode:$($instance.placementInstanceId)"
        Require ([string] $instance.graphNodeRef -eq [string] $instance.intendedGraphNodeRef) "GraphNodeIntent:$($instance.placementInstanceId)"
    }
    elseif ([string] $instance.graphBindingStateCode -eq "PendingStableNode") {
        Require ($null -eq $instance.graphNodeRef) "PendingGraphNodeMustBeNull:$($instance.placementInstanceId)"
        Require ($knownGraphNodes -notcontains [string] $instance.intendedGraphNodeRef) "PendingGraphNodeUnexpectedlyExists:$($instance.placementInstanceId)"
    }
    else { throw "ForestEdgeFarmAssetSurveyInvalid:GraphBindingState:$($instance.placementInstanceId)" }

    $survey = $instance.assetSurvey
    Require (@($survey.visualRequirementTags).Count -gt 0) "VisualRequirementTags:$($instance.placementInstanceId)"
    Require (($survey.sourcePreferenceCodes -join ",") -eq "SyntyOwnedFirst") "SourcePreference:$($instance.placementInstanceId)"
    Require ([string] $survey.surveyStateCode -eq "InventoryMatched") "SurveyState:$($instance.placementInstanceId)"
    Require (@($survey.candidateRefs).Count -eq 0) "ExactCandidateMustRemainEmpty:$($instance.placementInstanceId)"
    Require (@($survey.openGapCodes).Count -gt 0) "OpenGaps:$($instance.placementInstanceId)"
    foreach ($pack in @($survey.syntyPackCodes)) {
        $normalized = ([string] $pack).ToLowerInvariant().Replace("alpinemountain", "alpine-mountain")
        Require ($knownPacks -contains $normalized) "UnknownPack:$($instance.placementInstanceId):$pack"
    }
    foreach ($moduleCode in @($survey.functionalModuleCodes)) {
        Require ($knownModules -contains [string] $moduleCode) "UnknownModule:$($instance.placementInstanceId):$moduleCode"
    }
    foreach ($familyId in @($survey.assetFamilyIds)) {
        Require ($knownFamilies -contains [string] $familyId) "UnknownFamily:$($instance.placementInstanceId):$familyId"
    }
    foreach ($evidenceRef in @($survey.inventoryEvidenceRefs)) {
        $knownEvidence = ($knownSpatialRefs -contains [string] $evidenceRef) -or ($knownLoopModules -contains [string] $evidenceRef)
        Require $knownEvidence "UnknownEvidence:$($instance.placementInstanceId):$evidenceRef"
    }
}

$homeInstance = @($instances | Where-Object placementInstanceId -eq "placement-instance:forest-edge-farm:hans-residential-home")[0]
Require ([string] $homeInstance.assetSurvey.modificationNeedCode -eq "BlenderPlanRequiredCandidate") "HomeModificationState"
Require ([string] $homeInstance.assetSurvey.blenderGapSurvey.stateCode -eq "PlanningOnly") "BlenderPlanningState"
Require (($homeInstance.assetSurvey.blenderGapSurvey.evaluationOrderCodes -join ",") -eq "ExistingPrefab,CompositionOnly,ProjectOwnedVariant,BlenderPlanRequired,BlenderValidatedCopy") "BlenderEvaluationOrder"
Require ([string] $homeInstance.assetSurvey.blenderGapSurvey.singleDamageCueCode -eq "SmallRoofHoleVisibleFromForestTrail") "SingleRoofDamageCue"
Require (-not [bool] $homeInstance.assetSurvey.blenderGapSurvey.executionApproved) "BlenderExecutionApproval"

Require (@($profile.blockingReasons) -contains "CanonicalSceneRuntimeValidationPending") "RuntimeValidationBlocker"
Require (@($profile.blockingReasons) -contains "PlayerTraversalAndColliderValidationPending") "TraversalBlocker"
Require (@($profile.blockingReasons) -contains "VisualQualityApprovalPending") "VisualApprovalBlocker"

Write-Output "ForestEdgeFarmAssetSurveyTestsPassed:Instances=5;InventoryMatched=5;Candidates=0;GraphIntegrated=5;Candidate=Unapproved;Visual=Pending;Unity=0;Blender=1"
