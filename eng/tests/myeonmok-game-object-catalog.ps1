param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$catalogPath = Join-Path $repositoryRoot 'eng/world-seedbeds/object-catalogs/jungnang-myeonmok-game-objects.v1.json'
$classificationPath = Join-Path $repositoryRoot 'docs/AI/generated/world-interaction-gwae-classifications.json'
$projectPath = Join-Path $repositoryRoot 'eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj'
$artifactPath = Join-Path $repositoryRoot 'artifacts/local/spatial-catalog/20260910-r6'
$legacyArtifactPath = Join-Path $repositoryRoot 'artifacts/local/spatial-catalog/20260909-r4'
$registryPath = Join-Path $repositoryRoot 'eng/world-seedbeds/neighborhood-packages/registry.v2.json'
$semanticLayerPath = Join-Path $repositoryRoot 'eng/world-seedbeds/neighborhood-packages/semantic-layers.v1.json'

$catalogText = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8
$catalog = $catalogText | ConvertFrom-Json
$classification = Get-Content -LiteralPath $classificationPath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($catalog.schemaVersion -ne 'myeonmok-game-object-catalog.v1' -or
    $catalog.catalogStableId -ne 'game-object-catalog:jungnang-myeonmok.v1' -or
    $catalog.revision -ne 'jungnang-myeonmok-game-objects.r2' -or
    $catalog.authorityBoundary.isExecutionAuthority -or $catalog.authorityBoundary.sceneReady -or
    $catalog.authorityBoundary.gameStateConnected -or $catalog.authorityBoundary.privateDataIncluded) {
    throw 'MyeonmokObjectCatalogBoundaryMismatch'
}
if ($catalogText -match '"(roadAddress|lotAddress|houseNumber|buildingManagementNumber|phone|residentName)"\s*:') {
    throw 'MyeonmokObjectCatalogPrivateFieldLeak'
}

$sourceChecks = @(
    @{ Path=$catalog.graphMapPath; Ref=$catalog.graphMapRef; Revision=$catalog.graphMapRevision; IdField='graphStableId'; Hash=$catalog.graphMapSha256 },
    @{ Path=$catalog.placementMapPath; Ref=$catalog.placementMapRef; Revision=$catalog.placementMapRevision; IdField='profileStableId'; Hash=$catalog.placementMapSha256 },
    @{ Path=$catalog.sourceManifestPath; Ref=$catalog.sourceManifestRef; Revision=$catalog.sourceManifestRevision; IdField='manifestStableId'; Hash=$catalog.sourceManifestSha256 }
)
foreach ($sourceCheck in $sourceChecks) {
    $sourcePath = Join-Path $repositoryRoot $sourceCheck.Path
    $source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($source.($sourceCheck.IdField) -ne $sourceCheck.Ref -or $source.revision -ne $sourceCheck.Revision -or
        (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $sourceCheck.Hash) {
        throw "MyeonmokObjectCatalogSourceIdentityMismatch:$($sourceCheck.Path)"
    }
}

$expectedModules = [ordered]@{ Order='JIN'; Restaurant='RI'; Dispatch='GAN'; Delivery='GAM'; Warehouse='TAE' }
$expectedElements = @{ JIN='WOOD'; RI='FIRE'; GAN='EARTH'; GAM='WATER'; TAE='METAL' }
if (@($catalog.workflowBindings).Count -ne $expectedModules.Count) { throw 'MyeonmokObjectCatalogModuleCountMismatch' }
foreach ($module in $expectedModules.GetEnumerator()) {
    $binding = @($catalog.workflowBindings | Where-Object moduleCode -eq $module.Key)
    if ($binding.Count -ne 1 -or $binding[0].primaryRoleGwaeCode -ne $module.Value -or
        $binding[0].fiveElementCode -ne $expectedElements[$module.Value]) {
        throw "MyeonmokObjectCatalogModuleMismatch:$($module.Key)"
    }
}

$archetypes = @($catalog.archetypes)
if ($archetypes.Count -ne 26 -or @($archetypes.objectArchetypeStableId | Sort-Object -Unique).Count -ne 26) {
    throw 'MyeonmokObjectCatalogArchetypeIdentityMismatch'
}
foreach ($archetype in $archetypes) {
    if ($archetype.isExecutionAuthority) { throw "MyeonmokObjectCatalogAuthorityLeak:$($archetype.objectArchetypeStableId)" }
    if ($archetype.primaryRoleGwaeCode -eq 'NONE') {
        if ($archetype.fiveElementCode -ne 'NEUTRAL' -or $archetype.placementEligibility -ne 'BackdropOnly') {
            throw "MyeonmokObjectCatalogNeutralMismatch:$($archetype.objectArchetypeStableId)"
        }
    }
    elseif ($archetype.fiveElementCode -ne $expectedElements[$archetype.primaryRoleGwaeCode]) {
        throw "MyeonmokObjectCatalogElementMismatch:$($archetype.objectArchetypeStableId)"
    }
}

if ($catalog.classificationRevision -ne $classification.sourceRevision) { throw 'MyeonmokObjectCatalogClassificationRevisionMismatch' }
$profileIds = @($catalog.interactionProfiles.profileStableId)
if (@($profileIds | Sort-Object -Unique).Count -ne $profileIds.Count) { throw 'MyeonmokObjectCatalogProfileDuplicate' }
foreach ($profile in @($catalog.interactionProfiles)) {
    $classified = @($classification.items | Where-Object wiId -eq $profile.wiId)
    if ($classified.Count -ne 1) { throw "MyeonmokObjectCatalogWiMissing:$($profile.wiId)" }
    foreach ($pair in @(
        @('actionGwaeCode','actionGwae'), @('operationGwaeCode','operationGwae'),
        @('targetGwaeCode','targetGwae'), @('supportGwaeCode','supportGwae'))) {
        $catalogValue = [string]$profile.($pair[0])
        $classificationValue = [string]$classified[0].($pair[1])
        if ($catalogValue -ne $classificationValue) { throw "MyeonmokObjectCatalogGwaeMismatch:$($profile.wiId):$($pair[0])" }
    }
}

$bindings = @($catalog.roleActionBindings)
if (@($bindings.bindingStableId | Sort-Object -Unique).Count -ne $bindings.Count) { throw 'MyeonmokObjectCatalogRoleActionDuplicate' }
foreach ($binding in $bindings) {
    if ($binding.profileStableId -notin $profileIds -or $binding.objectArchetypeRef -notin $archetypes.objectArchetypeStableId) {
        throw "MyeonmokObjectCatalogRoleActionRefMissing:$($binding.bindingStableId)"
    }
}
foreach ($profileId in $profileIds) {
    if (@($bindings | Where-Object { $_.profileStableId -eq $profileId -and $_.participationKindCode -eq 'Action' -and $_.required }).Count -ne 1) {
        throw "MyeonmokObjectCatalogRequiredSubjectMismatch:$profileId"
    }
}

if (-not $SkipBuild) {
    $null = dotnet build $projectPath --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'MyeonmokObjectCatalogBuildFailed' }
    $preview = dotnet run --project $projectPath --no-build -- catalog-preview $repositoryRoot | ConvertFrom-Json
}
else {
    $previewEvidence = Get-ChildItem -LiteralPath $artifactPath -Filter 'preview-*.json' |
        Where-Object BaseName -Match '^preview-\d{8}T' |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($null -eq $previewEvidence) { throw 'MyeonmokObjectCatalogPreviewEvidenceMissing' }
    $preview = Get-Content -LiteralPath $previewEvidence.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
}
if ((-not $SkipBuild -and $LASTEXITCODE -ne 0) -or $preview.documents -ne 34 -or $preview.elements -ne 7511 -or
    $preview.objectCandidates -ne 603 -or $preview.workflowObjectArchetypes -ne 26 -or
    $preview.neighborhoodPackageRegistrations -ne 2 -or $preview.neighborhoodPackageDocuments -ne 2 -or
    $preview.junghwaShopObservations -ne 1672 -or $preview.junghwaFacilityObservations -ne 14 -or
    $preview.junghwaCoordinateObservations -ne 1683 -or @($preview.areaStableIds).Count -ne 2 -or
    $preview.byKind.SemanticLayerCatalog -ne 1 -or $preview.byKind.PresentationProjection -ne 2 -or
    $preview.byElementKind.SemanticLayerDefinition -ne 8 -or $preview.byElementKind.SemanticLayerBinding -ne 12 -or
    $preview.bySemanticLayer.'spatial-layer:building-footprint.v1' -ne 1207 -or
    $preview.bySemanticLayer.'spatial-layer:road-centerline.v1' -ne 2401 -or
    $preview.bySemanticLayer.'spatial-layer:facility-observation.v1' -ne 2694 -or
    $preview.bySemanticLayer.'spatial-layer:address-link-private.v1' -ne 206 -or
    $preview.databaseWriteAttempted -or $preview.gameStateConnected) {
    throw 'MyeonmokObjectCatalogPreviewMismatch'
}

$registry = Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($registry.schemaVersion -ne 'neighborhood-package-registry.v2' -or
    $registry.semanticLayerCatalogRef -ne 'spatial-semantic-layer-catalog:ssalddel-neighborhood.v1' -or
    @($registry.packages).Count -ne 2 -or
    @($registry.packages | Where-Object areaStableId -eq 'region:kr:bjd:1126010100').Count -ne 1 -or
    @($registry.packages | Where-Object areaStableId -eq 'region:kr:bjd:1126010300').Count -ne 1) {
    throw 'NeighborhoodPackageRegistryMismatch'
}
$semanticLayerText = Get-Content -LiteralPath $semanticLayerPath -Raw -Encoding UTF8
$semanticLayerCatalog = $semanticLayerText | ConvertFrom-Json
if ($semanticLayerCatalog.schemaVersion -ne 'spatial-semantic-layer-catalog.v1' -or
    @($semanticLayerCatalog.semanticLayers).Count -ne 8 -or
    @($semanticLayerCatalog.semanticLayers.semanticLayerStableId | Sort-Object -Unique).Count -ne 8 -or
    (Get-FileHash -LiteralPath $semanticLayerPath -Algorithm SHA256).Hash -ne $registry.semanticLayerCatalogSha256) {
    throw 'NeighborhoodSemanticLayerCatalogMismatch'
}
if (@($registry.compatibilityAliases | Where-Object {
    $_.legacyRef -eq 'area:reference:myeonmok' -and $_.canonicalRef -eq 'region:kr:bjd:1126010100'
}).Count -ne 1) { throw 'NeighborhoodAreaCompatibilityAliasMissing' }
foreach ($package in @($registry.packages)) {
    foreach ($input in @($package.sourceInputs)) {
        $inputPath = Join-Path $repositoryRoot $input.repoRelativePath
        if ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $input.expectedSha256) {
            throw "NeighborhoodPackageSourceHashMismatch:$($input.sourceStableId)"
        }
    }
}

# r4 Unity 인계는 호환 증거로 남긴다. r5 동별 패키지는 Unity/Scene을 새로 쓰지 않는다.
$handoffPath = Join-Path $legacyArtifactPath 'myeonmok-game-object-unity-handoff.r2.json'
$handoffText = Get-Content -LiteralPath $handoffPath -Raw -Encoding UTF8
$handoff = $handoffText | ConvertFrom-Json
$buildingCandidates = @($handoff.candidates | Where-Object { $_.objectArchetypeRef -eq 'game-object-archetype:myeonmok-neutral-building.v1' })
$roadCandidates = @($handoff.candidates | Where-Object { $_.objectArchetypeRef -eq 'game-object-archetype:myeonmok-neutral-road-network.v1' })
if ($buildingCandidates.Count -ne 602 -or $roadCandidates.Count -ne 1 -or $roadCandidates[0].sourceElementCount -ne 2397) {
    throw 'MyeonmokObjectCatalogCandidateCountMismatch'
}
if ($handoff.sceneReady -or $handoff.gameStateConnected -or $handoff.isExecutionAuthority -or
    $handoff.unityExecutionState -ne 'Deferred' -or $handoffText -match '"sourceElementRefs"\s*:') {
    throw 'MyeonmokObjectCatalogHandoffBoundaryMismatch'
}

Write-Output "PASS Neighborhood packages: shared 26 archetypes, Myeonmok 602 building + 1 road-network candidates, Junghwa 1672 shops + 14 facilities (1683 coordinate candidates); SkipBuild=$SkipBuild; Mongo preview only; Unity/operational writes 0"
