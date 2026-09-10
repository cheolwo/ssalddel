[CmdletBinding()]
param(
    [ValidateSet('Survey','Write','Check','Query')]
    [string] $Mode = 'Check',
    [string] $PolicyPath = 'eng/execution-ledgers/h1-synty-representation-assignment-policy.json',
    [string] $LedgerPath = 'eng/execution-ledgers/h1-synty-representation-assignments.json',
    [string] $HCatalogPath = 'eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json',
    [string] $PlanningIndexPath = 'docs/AI/PLANNING.md',
    [string] $PresentationPoolPath = 'eng/execution-ledgers/playable-loop-presentation-e4-candidate-pool.json',
    [string] $FunctionalModulePath = 'eng/execution-ledgers/synty-asset-functional-modules.json',
    [string] $OutputPath = 'docs/AI/generated/h1-synty-representation-assignments.md',
    [string] $MachineOutputPath = 'docs/AI/generated/h1-synty-representation-assignments.json',
    [string] $UnityProjectRoot = '',
    [ValidateSet('H1','H2','Plan','State','Status')]
    [string] $QueryKind = 'H1',
    [string] $QueryValue = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../common/deterministic-text-output.ps1')

function Require([bool] $Condition, [string] $Code) {
    if (-not $Condition) { throw "H1SyntyRepresentationAssignmentInvalid:$Code" }
}
function Require-Text([object] $Value, [string] $Code) {
    Require (-not [string]::IsNullOrWhiteSpace([string] $Value)) $Code
}
function Get-Sha256([string] $Path) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($Path)))
}
function Resolve-RepoPath([string] $Root, [string] $RelativePath) {
    $resolved = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    Require ($resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) "PathTraversal:$RelativePath"
    $resolved
}
function Get-RelativePath([string] $Root, [string] $Path) {
    [IO.Path]::GetRelativePath($Root, $Path).Replace('\','/')
}
function Get-MetaGuid([string] $MetaPath) {
    $line = Select-String -LiteralPath $MetaPath -Pattern '^guid:\s*([0-9a-f]{32})\s*$' | Select-Object -First 1
    if ($null -eq $line) { return '' }
    [string] $line.Matches[0].Groups[1].Value
}
function Escape-Cell([object] $Value) {
    if ($null -eq $Value) { return '' }
    ([string] $Value).Replace('|','\|').Replace("`r",' ').Replace("`n",' ')
}
function Get-PropertyValues([object] $Object, [string] $Name) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    @($property.Value)
}
function Read-HDefinitions([object] $Catalog, [string] $CatalogFile) {
    $inventoryRoot = Split-Path -Parent $CatalogFile
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($ref in @($Catalog.h1DefinitionRefs)) {
        $definitionPath = Resolve-RepoPath $inventoryRoot ([string] $ref.definitionPath)
        Require (Test-Path -LiteralPath $definitionPath -PathType Leaf) "H1DefinitionMissing:$($ref.stableId)"
        $definitionSha256 = Get-Sha256 $definitionPath
        $referenceHashMatches = $definitionSha256 -ceq ([string] $ref.definitionSha256).ToUpperInvariant()
        $definition = Get-Content -LiteralPath $definitionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Require ([string] $definition.stableId -ceq [string] $ref.stableId) "H1StableIdMismatch:$($ref.stableId)"
        $packs = @(@(Get-PropertyValues $definition 'sourcePackCode') + @(Get-PropertyValues $definition 'sourcePackCodes') | Where-Object { -not [string]::IsNullOrWhiteSpace([string] $_) } | Select-Object -Unique)
        $rows.Add([pscustomobject]@{
            stableId = [string] $ref.stableId
            cardKindCode = [string] $ref.cardKindCode
            title = [string] $definition.title
            sourcePackCodes = $packs
            wiIds = @(Get-PropertyValues $definition 'wiIds' | ForEach-Object { [string] $_ })
            spatialRoleCodes = @(Get-PropertyValues $definition 'spatialRoleCodes' | ForEach-Object { [string] $_ })
            capabilityCodes = @(Get-PropertyValues $definition 'capabilityCodes' | ForEach-Object { [string] $_ })
            definitionPath = Get-RelativePath $repositoryRoot $definitionPath
            definitionSha256 = $definitionSha256
            catalogReferenceHashMatches = $referenceHashMatches
        })
    }
    @($rows)
}
function Read-H2Definitions([object] $Catalog, [string] $CatalogFile) {
    $inventoryRoot = Split-Path -Parent $CatalogFile
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($ref in @($Catalog.h2DefinitionRefs)) {
        $definitionPath = Resolve-RepoPath $inventoryRoot ([string] $ref.definitionPath)
        Require (Test-Path -LiteralPath $definitionPath -PathType Leaf) "H2DefinitionMissing:$($ref.stableId)"
        $definitionSha256 = Get-Sha256 $definitionPath
        $referenceHashMatches = $definitionSha256 -ceq ([string] $ref.definitionSha256).ToUpperInvariant()
        $definition = Get-Content -LiteralPath $definitionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $rows.Add([pscustomobject]@{
            stableId = [string] $ref.stableId
            title = [string] $definition.title
            requiredH1Refs = @(Get-PropertyValues $definition 'requiredH1Refs' | ForEach-Object { [string] $_ })
            optionalH1Refs = @(Get-PropertyValues $definition 'optionalH1Refs' | ForEach-Object { [string] $_ })
            connectorRoleCodes = @(Get-PropertyValues $definition 'connectorRoleCodes' | ForEach-Object { [string] $_ })
            definitionPath = Get-RelativePath $repositoryRoot $definitionPath
            definitionSha256 = $definitionSha256
            catalogReferenceHashMatches = $referenceHashMatches
        })
    }
    @($rows)
}
function Read-PlanningRows([string] $IndexPath, [object[]] $H1Rows) {
    $planRows = [Collections.Generic.List[object]]::new()
    $indexDirectory = Split-Path -Parent $IndexPath
    foreach ($line in Get-Content -LiteralPath $IndexPath -Encoding UTF8) {
        if ($line -notmatch '^\| `(?<id>PLAN-[^`]+)` \| (?<documents>.+?) \| `(?<status>[^`]+)` \|') { continue }
        $planId = [string] $Matches.id
        $status = [string] $Matches.status
        $documentCell = [string] $Matches.documents
        $documentRefs = [Collections.Generic.List[string]]::new()
        $documentHashes = [Collections.Generic.List[object]]::new()
        foreach ($match in [regex]::Matches($documentCell, '\[[^\]]+\]\((?<path>[^)#]+)(?:#[^)]+)?\)')) {
            $relative = [string] $match.Groups['path'].Value
            if ($relative -match '^[a-z]+://') { continue }
            $full = [IO.Path]::GetFullPath((Join-Path $indexDirectory $relative))
            if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
            $repoRelative = Get-RelativePath $repositoryRoot $full
            if (-not $documentRefs.Contains($repoRelative)) {
                $documentRefs.Add($repoRelative)
                $documentHashes.Add([pscustomobject]@{ path=$repoRelative; sha256=Get-Sha256 $full })
            }
        }
        $combined = @($documentHashes | ForEach-Object { Get-Content -LiteralPath (Resolve-RepoPath $repositoryRoot $_.path) -Raw -Encoding UTF8 }) -join "`n"
        $direct = @($H1Rows | Where-Object { $combined.Contains([string] $_.stableId, [StringComparison]::Ordinal) } | ForEach-Object { $_.stableId } | Sort-Object -Unique)
        $wiRefs = @([regex]::Matches($combined, 'WI-(?:[A-Z]+(?:-[A-Z0-9]+)*|\d+)') | ForEach-Object { $_.Value } | Sort-Object -Unique)
        $indirect = @()
        if ($direct.Count -eq 0 -and $wiRefs.Count -gt 0) {
            $indirect = @($H1Rows | Where-Object { @($_.wiIds | Where-Object { $wiRefs -contains $_ }).Count -gt 0 } | ForEach-Object { $_.stableId } | Sort-Object -Unique)
        }
        $classification = if ($direct.Count -gt 0) { 'DirectHReference' } elseif ($indirect.Count -gt 0) { 'IndirectViaWI' } else { 'NoDirectHImpact' }
        $planRows.Add([pscustomobject]@{
            planId=$planId; status=$status; classificationCode=$classification
            directH1Refs=$direct; indirectH1Refs=$indirect; wiRefs=$wiRefs
            documentRefs=@($documentHashes)
        })
    }
    @($planRows)
}
function Find-Rule([object] $Policy, [object] $H1) {
    $search = @($H1.stableId, $H1.title, @($H1.spatialRoleCodes) -join ' ', @($H1.capabilityCodes) -join ' ') -join ' '
    $matches = @($Policy.assignmentRules | Where-Object { $search -match [string] $_.matchPattern } | Select-Object -First 1)
    if ($matches.Count -eq 0) { return $null }
    $matches[0]
}
function Get-AllowedRoots([object] $Policy, [object] $H1) {
    $roots = [Collections.Generic.List[string]]::new()
    foreach ($pack in @($H1.sourcePackCodes)) {
        $property = $Policy.packRoots.PSObject.Properties[[string] $pack]
        if ($null -eq $property) { continue }
        foreach ($root in @($property.Value)) { if (-not $roots.Contains([string] $root)) { $roots.Add([string] $root) } }
    }
    @($roots)
}
function Find-PrefabCandidates([object] $Policy, [object] $H1, [object] $Rule, [object[]] $PrefabInventory) {
    if ($null -eq $Rule) { return @() }
    $allowedRoots = [Collections.Generic.List[string]]::new()
    foreach ($root in @(Get-AllowedRoots $Policy $H1) + @(Get-PropertyValues $Rule 'additionalRoots')) {
        if (-not $allowedRoots.Contains([string] $root)) { $allowedRoots.Add([string] $root) }
    }
    $scored = [Collections.Generic.List[object]]::new()
    for ($patternIndex=0; $patternIndex -lt @($Rule.candidateNamePatterns).Count; $patternIndex++) {
        $pattern = [string] $Rule.candidateNamePatterns[$patternIndex]
        foreach ($asset in @($PrefabInventory | Where-Object {
            $candidate = $_
            $candidate.name -match $pattern -and ($allowedRoots.Count -eq 0 -or @($allowedRoots | Where-Object { $candidate.assetPath.StartsWith([string] $_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0)
        })) {
            $packRank = 99
            for ($i=0; $i -lt $allowedRoots.Count; $i++) { if ($asset.assetPath.StartsWith($allowedRoots[$i], [StringComparison]::OrdinalIgnoreCase)) { $packRank=$i; break } }
            $scored.Add([pscustomobject]@{asset=$asset; score=($patternIndex*100)+($packRank*10)+$asset.assetPath.Length})
        }
    }
    $seen=@{}; $selected=[Collections.Generic.List[object]]::new()
    foreach ($entry in @($scored | Sort-Object score,@{Expression={$_.asset.assetPath}})) {
        if ($seen.ContainsKey($entry.asset.assetPath)) { continue }
        $seen[$entry.asset.assetPath]=$true; $selected.Add($entry.asset)
        if ($selected.Count -ge [int] $Policy.candidateLimit) { break }
    }
    @($selected)
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$policyFile = Resolve-RepoPath $repositoryRoot $PolicyPath
$hCatalogFile = Resolve-RepoPath $repositoryRoot $HCatalogPath
$planningIndexFile = Resolve-RepoPath $repositoryRoot $PlanningIndexPath
$presentationPoolFile = Resolve-RepoPath $repositoryRoot $PresentationPoolPath
$functionalModuleFile = Resolve-RepoPath $repositoryRoot $FunctionalModulePath
$ledgerFile = if ([IO.Path]::IsPathRooted($LedgerPath)) { [IO.Path]::GetFullPath($LedgerPath) } else { Join-Path $repositoryRoot $LedgerPath }
$policy = Get-Content -LiteralPath $policyFile -Raw -Encoding UTF8 | ConvertFrom-Json
$hCatalog = Get-Content -LiteralPath $hCatalogFile -Raw -Encoding UTF8 | ConvertFrom-Json
$presentationPool = Get-Content -LiteralPath $presentationPoolFile -Raw -Encoding UTF8 | ConvertFrom-Json
$functionalModules = Get-Content -LiteralPath $functionalModuleFile -Raw -Encoding UTF8 | ConvertFrom-Json

Require ([string] $policy.schemaVersion -ceq 'h1-synty-representation-assignment-policy.v1') 'PolicySchemaInvalid'
Require ([bool] $policy.authorityBoundary.hDefinitionsRemainAssetPathFree) 'HDefinitionBoundaryMissing'
Require ([bool] $policy.authorityBoundary.candidateIsNotSelectionApproval) 'CandidateApprovalBoundaryInvalid'
Require ([bool] $policy.authorityBoundary.candidateIsNotWorldPlacement) 'WorldPlacementBoundaryInvalid'
Require ([bool] $policy.authorityBoundary.candidateIsNotEvidencePromotion) 'EvidenceBoundaryInvalid'

$h1Rows = @(Read-HDefinitions $hCatalog $hCatalogFile)
$h2Rows = @(Read-H2Definitions $hCatalog $hCatalogFile)
Require ($h1Rows.Count -eq @($hCatalog.h1DefinitionRefs).Count) 'H1CoverageMismatch'
Require (@($h1Rows | Group-Object stableId | Where-Object Count -gt 1).Count -eq 0) 'H1Duplicate'
Require (@($h2Rows | Group-Object stableId | Where-Object Count -gt 1).Count -eq 0) 'H2Duplicate'
$plans = @(Read-PlanningRows $planningIndexFile $h1Rows)
Require ($plans.Count -gt 0) 'PlanningIndexEmpty'
Require (@($plans | Group-Object planId | Where-Object Count -gt 1).Count -eq 0) 'PlanDuplicate'

if ($Mode -eq 'Survey') {
    Require-Text $UnityProjectRoot 'UnityProjectRootRequired'
    $unityRoot = [IO.Path]::GetFullPath($UnityProjectRoot)
    $syntyRoot = Join-Path $unityRoot 'Assets/Synty'
    Require (Test-Path -LiteralPath $syntyRoot -PathType Container) 'UnitySyntyRootMissing'
    $prefabs = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $syntyRoot -Recurse -File -Filter '*.prefab') {
        $meta = $file.FullName + '.meta'
        if (-not (Test-Path -LiteralPath $meta -PathType Leaf)) { continue }
        $prefabs.Add([pscustomobject]@{
            name=$file.BaseName
            assetPath=Get-RelativePath $unityRoot $file.FullName
            guid=Get-MetaGuid $meta
            fileSha256=Get-Sha256 $file.FullName
            metaSha256=Get-Sha256 $meta
        })
    }
    Require ($prefabs.Count -gt 0) 'UnityPrefabInventoryEmpty'
    $existingTransitions=@($presentationPool.stateTransitionVisualPreparations)
    $assignments=[Collections.Generic.List[object]]::new()
    foreach ($h1 in @($h1Rows | Sort-Object stableId)) {
        $rule=Find-Rule $policy $h1
        $candidates=@(Find-PrefabCandidates $policy $h1 $rule @($prefabs))
        $existingTransition=@($existingTransitions | Where-Object { [string] $_.subjectStableId -ceq [string] $h1.stableId })
        $stateCategory = if ($null -eq $rule) { 'Static' } else { [string] $rule.stateCategoryCode }
        $stateDefinition = $policy.stateCategories.PSObject.Properties[$stateCategory].Value
        $stateStatus = if ($existingTransition.Count -gt 0) { 'ExistingE4Preparation' } elseif ($stateCategory -eq 'Static') { 'StateTransitionNotRequired' } else { 'PendingStateContract' }
        $assignmentStatus = if ($candidates.Count -eq 0) { 'NoCandidate' } elseif ($existingTransition.Count -gt 0 -and @($existingTransition.representationCandidates | Where-Object strategyCode -eq 'BlenderOwnedVariant').Count -gt 0) { 'BlenderRequired' } else { 'PendingVisualReview' }
        $candidateRows=[Collections.Generic.List[object]]::new()
        for ($i=0; $i -lt $candidates.Count; $i++) {
            $asset=$candidates[$i]
            $candidateRows.Add([pscustomobject]@{
                slotCode=if($i -eq 0){'PrimaryCandidate'}else{'AlternativeCandidate'}
                roleCode=if($null -eq $rule){'Unresolved'}else{[string]$rule.roleCode}
                assetPath=$asset.assetPath; guid=$asset.guid
                fileSha256=$asset.fileSha256; metaSha256=$asset.metaSha256
                evidenceStateCode='StaticFileAndMetaVerified_VisualReviewPending'
            })
        }
        $assignments.Add([pscustomobject]@{
            h1StableId=$h1.stableId; title=$h1.title; cardKindCode=$h1.cardKindCode
            definitionPath=$h1.definitionPath; definitionSha256=$h1.definitionSha256
            catalogReferenceHashMatches=[bool]$h1.catalogReferenceHashMatches
            sourcePackCodes=@($h1.sourcePackCodes); wiIds=@($h1.wiIds)
            matchedRuleId=if($null -eq $rule){$null}else{[string]$rule.ruleId}
            assignmentStatusCode=$assignmentStatus
            representationSet=@($candidateRows)
            stateTransition=[pscustomobject]@{
                statusCode=$stateStatus; categoryCode=$stateCategory
                before=@($stateDefinition.before); active=@($stateDefinition.active); after=@($stateDefinition.after); recovery=@($stateDefinition.recovery)
                reason=[string]$stateDefinition.reason
                existingPreparationRefs=@($existingTransition | ForEach-Object { [pscustomobject]@{transitionStableId=$_.transitionStableId; worldInteractionId=$_.worldInteractionId; readinessCode=$_.readinessCode} })
            }
            reviewBoundary='Candidate allocation only; image suitability, Unity placement, E5 and approval remain separate.'
        })
    }
    $assignmentByH1=@{}; foreach($item in $assignments){$assignmentByH1[[string]$item.h1StableId]=$item}
    $h2Compositions=[Collections.Generic.List[object]]::new()
    foreach($h2 in @($h2Rows|Sort-Object stableId)){
        $missing=@($h2.requiredH1Refs|Where-Object{-not $assignmentByH1.ContainsKey([string]$_) -or [string]$assignmentByH1[[string]$_].assignmentStatusCode -eq 'NoCandidate'})
        $h2Compositions.Add([pscustomobject]@{
            h2StableId=$h2.stableId; title=$h2.title; requiredH1Refs=@($h2.requiredH1Refs); optionalH1Refs=@($h2.optionalH1Refs)
            definitionPath=$h2.definitionPath; definitionSha256=$h2.definitionSha256
            catalogReferenceHashMatches=[bool]$h2.catalogReferenceHashMatches
            connectorRoleCodes=@($h2.connectorRoleCodes); directPrefabAssignmentAllowed=$false
            readinessCode=if($missing.Count -eq 0){'CandidateCompositionReady'}else{'BlockedByH1CandidateGap'}
            missingRequiredH1CandidateRefs=$missing
        })
    }
    $ledger=[ordered]@{
        schemaVersion='h1-synty-representation-assignments.v1'
        revision='h1-synty-representation-assignments.r1'
        surveyedAtUtc=$null
        surveyEvidenceCode='DeterministicLocalFileAndMetaSurvey'
        sourceBaselines=[ordered]@{
            policy=[ordered]@{path=$PolicyPath;sha256=Get-Sha256 $policyFile;revision=$policy.revision}
            hCatalog=[ordered]@{path=$HCatalogPath;sha256=Get-Sha256 $hCatalogFile;revision=$hCatalog.revision}
            planningIndex=[ordered]@{path=$PlanningIndexPath;sha256=Get-Sha256 $planningIndexFile;planCount=$plans.Count}
            presentationPool=[ordered]@{path=$PresentationPoolPath;sha256=Get-Sha256 $presentationPoolFile;revision=$presentationPool.revision}
            functionalModules=[ordered]@{path=$FunctionalModulePath;sha256=Get-Sha256 $functionalModuleFile;revision=$functionalModules.revision;expectedPrefabCount=$functionalModules.expectedPrefabCount}
            unitySurvey=[ordered]@{relativeRoot='Assets/Synty';discoveredPrefabCount=$prefabs.Count;note='기능 대장 4,211과 파일 조사 모집단은 정의가 다를 수 있어 GUID 집합 대조 전 동일 수량으로 해석하지 않는다.'}
        }
        authorityBoundary=$policy.authorityBoundary
        counts=[ordered]@{plans=$plans.Count;h1=$h1Rows.Count;h1Interaction=@($h1Rows|Where-Object cardKindCode -eq 'InteractionSpace').Count;h1Expression=@($h1Rows|Where-Object cardKindCode -eq 'PackExpression').Count;h1CatalogHashDrift=@($h1Rows|Where-Object{-not $_.catalogReferenceHashMatches}).Count;h2=$h2Rows.Count;h2CatalogHashDrift=@($h2Rows|Where-Object{-not $_.catalogReferenceHashMatches}).Count}
        planImpacts=@($plans)
        h1Assignments=@($assignments)
        h2Compositions=@($h2Compositions)
        serverDraftImport=[ordered]@{statusCode='NotImported';reason='시각 검토를 통과한 ExactCandidate가 아직 없어 기존 구성 UseCase에 Draft를 만들지 않는다.'}
    }
    $ledgerText=ConvertTo-DeterministicText (($ledger|ConvertTo-Json -Depth 40)+"`n")
    Write-DeterministicTextIfChanged $ledgerFile $ledgerText | Out-Null
}

Require (Test-Path -LiteralPath $ledgerFile -PathType Leaf) 'LedgerMissing'
$ledger=Get-Content -LiteralPath $ledgerFile -Raw -Encoding UTF8|ConvertFrom-Json
Require ([string]$ledger.schemaVersion -ceq 'h1-synty-representation-assignments.v1') 'LedgerSchemaInvalid'
Require ([string]$ledger.sourceBaselines.policy.sha256 -ceq (Get-Sha256 $policyFile)) 'PolicyStale'
Require ([string]$ledger.sourceBaselines.hCatalog.sha256 -ceq (Get-Sha256 $hCatalogFile)) 'HCatalogStale'
Require ([string]$ledger.sourceBaselines.planningIndex.sha256 -ceq (Get-Sha256 $planningIndexFile)) 'PlanningIndexStale'
Require ([string]$ledger.sourceBaselines.presentationPool.sha256 -ceq (Get-Sha256 $presentationPoolFile)) 'PresentationPoolStale'
Require ([string]$ledger.sourceBaselines.functionalModules.sha256 -ceq (Get-Sha256 $functionalModuleFile)) 'FunctionalModulesStale'
Require ([int]$ledger.counts.plans -eq $plans.Count) 'PlanCountMismatch'
Require ([int]$ledger.counts.h1 -eq $h1Rows.Count) 'H1CountMismatch'
Require ([int]$ledger.counts.h2 -eq $h2Rows.Count) 'H2CountMismatch'
Require ([int]$ledger.counts.h1CatalogHashDrift -eq @($h1Rows|Where-Object{-not $_.catalogReferenceHashMatches}).Count) 'H1CatalogDriftCountMismatch'
Require ([int]$ledger.counts.h2CatalogHashDrift -eq @($h2Rows|Where-Object{-not $_.catalogReferenceHashMatches}).Count) 'H2CatalogDriftCountMismatch'
Require (@($ledger.planImpacts).Count -eq $plans.Count) 'PlanImpactCoverageMismatch'
Require (@($ledger.h1Assignments).Count -eq $h1Rows.Count) 'H1AssignmentCoverageMismatch'
Require (@($ledger.h2Compositions).Count -eq $h2Rows.Count) 'H2CompositionCoverageMismatch'
Require (@($ledger.h1Assignments|Group-Object h1StableId|Where-Object Count -gt 1).Count -eq 0) 'H1AssignmentDuplicate'
$knownH1=@{};foreach($h1 in $h1Rows){$knownH1[[string]$h1.stableId]=$h1}
foreach($item in @($ledger.h1Assignments)){
    $id=[string]$item.h1StableId;Require($knownH1.ContainsKey($id)) "H1AssignmentUnknown:$id"
    Require([string]$item.definitionSha256 -ceq [string]$knownH1[$id].definitionSha256) "H1DefinitionHashMismatch:$id"
    Require([bool]$item.catalogReferenceHashMatches -eq [bool]$knownH1[$id].catalogReferenceHashMatches) "H1CatalogDriftMismatch:$id"
    Require([string]$item.assignmentStatusCode -in @('PendingVisualReview','BlenderRequired','NoCandidate','ExactCandidate','NotApplicable')) "H1StatusInvalid:$id"
    if([string]$item.assignmentStatusCode -notin @('NoCandidate','NotApplicable')){Require(@($item.representationSet).Count -gt 0) "H1CandidateMissing:$id"}
    Require(@($item.representationSet|Where-Object slotCode -eq 'PrimaryCandidate').Count -le 1) "H1PrimaryDuplicate:$id"
    foreach($asset in @($item.representationSet)){
        Require-Text $asset.assetPath "AssetPathMissing:$id";Require([string]$asset.guid -match '^[0-9a-f]{32}$') "AssetGuidInvalid:$id"
        Require([string]$asset.fileSha256 -match '^[0-9A-F]{64}$') "AssetHashInvalid:$id";Require([string]$asset.metaSha256 -match '^[0-9A-F]{64}$') "AssetMetaHashInvalid:$id"
        if(-not [string]::IsNullOrWhiteSpace($UnityProjectRoot)){
            $assetFile=[IO.Path]::GetFullPath((Join-Path $UnityProjectRoot ([string]$asset.assetPath)))
            Require(Test-Path -LiteralPath $assetFile -PathType Leaf) "AssetMissing:${id}:$($asset.assetPath)"
            Require((Get-Sha256 $assetFile) -ceq [string]$asset.fileSha256) "AssetFileStale:${id}:$($asset.assetPath)"
            Require((Get-Sha256 ($assetFile+'.meta')) -ceq [string]$asset.metaSha256) "AssetMetaStale:${id}:$($asset.assetPath)"
            Require((Get-MetaGuid ($assetFile+'.meta')) -ceq [string]$asset.guid) "AssetGuidMismatch:${id}:$($asset.assetPath)"
        }
    }
    $state=[string]$item.stateTransition.statusCode
    Require($state -in @('ExistingE4Preparation','PendingStateContract','StateTransitionNotRequired')) "StateStatusInvalid:$id"
    if($state -eq 'PendingStateContract'){Require(@($item.stateTransition.before).Count -gt 0 -and @($item.stateTransition.active).Count -gt 0 -and @($item.stateTransition.after).Count -gt 0) "StateTriadMissing:$id"}
}
$knownH2=@{};foreach($h2 in $h2Rows){$knownH2[[string]$h2.stableId]=$h2}
foreach($h2 in @($ledger.h2Compositions)){
    $h2Id=[string]$h2.h2StableId;Require($knownH2.ContainsKey($h2Id)) "H2Unknown:$h2Id"
    Require([string]$h2.definitionSha256 -ceq [string]$knownH2[$h2Id].definitionSha256) "H2DefinitionHashMismatch:$h2Id"
    Require([bool]$h2.catalogReferenceHashMatches -eq [bool]$knownH2[$h2Id].catalogReferenceHashMatches) "H2CatalogDriftMismatch:$h2Id"
    Require(-not [bool]$h2.directPrefabAssignmentAllowed) "H2DirectPrefabForbidden:$($h2.h2StableId)"
    foreach($ref in @($h2.requiredH1Refs)+@($h2.optionalH1Refs)){Require($knownH1.ContainsKey([string]$ref)) "H2H1Unknown:$($h2.h2StableId):$ref"}
}

$statusGroups=@($ledger.h1Assignments|Group-Object assignmentStatusCode)
$stateGroups=@($ledger.h1Assignments|Group-Object {$_.stateTransition.statusCode})
$planGroups=@($ledger.planImpacts|Group-Object classificationCode)
$builder=[Text.StringBuilder]::new()
[void]$builder.AppendLine('# H1 Synty 표현 배당·상태 변화 전수 상태판')
[void]$builder.AppendLine()
[void]$builder.AppendLine("> 이 문서는 ``$LedgerPath``에서 생성된다. 후보 배당은 이미지 적합성 승인·Unity World 배치·E5가 아니다.")
[void]$builder.AppendLine()
[void]$builder.AppendLine(('- 기획: `{0}`개 — 직접 H 참조 `{1}`, WI 간접 참조 `{2}`, 직접 영향 없음 `{3}`' -f $ledger.counts.plans,@($ledger.planImpacts|Where-Object classificationCode -eq 'DirectHReference').Count,@($ledger.planImpacts|Where-Object classificationCode -eq 'IndirectViaWI').Count,@($ledger.planImpacts|Where-Object classificationCode -eq 'NoDirectHImpact').Count))
[void]$builder.AppendLine(('- H1: `{0}`개 — 상호작용 `{1}`, 표현 패턴 `{2}`' -f $ledger.counts.h1,$ledger.counts.h1Interaction,$ledger.counts.h1Expression))
[void]$builder.AppendLine(('- H2: `{0}`개 — H1 후보 조립만 검사하며 직접 Prefab 배당 금지' -f $ledger.counts.h2))
[void]$builder.AppendLine(('- H 카탈로그 참조 해시 불일치: H1 `{0}`, H2 `{1}` — 현재 정의로 조사하되 E5 전 재결속 필요' -f $ledger.counts.h1CatalogHashDrift,$ledger.counts.h2CatalogHashDrift))
foreach($code in @('ExactCandidate','PendingVisualReview','BlenderRequired','NoCandidate','NotApplicable')){[void]$builder.AppendLine(('- H1 {0}: `{1}`' -f $code,@($ledger.h1Assignments|Where-Object assignmentStatusCode -eq $code).Count))}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## H1 표현 배당')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| H1 | 종류·팩 | 역할별 후보 | 상태 | 전·중·후 |')
[void]$builder.AppendLine('| --- | --- | --- | --- | --- |')
foreach($item in @($ledger.h1Assignments|Sort-Object h1StableId)){
    $assets=if(@($item.representationSet).Count -eq 0){'미배당'}else{@($item.representationSet|ForEach-Object{"``$($_.slotCode)`` · ``$($_.assetPath)``"}) -join '<br>'}
    $transition=if([string]$item.stateTransition.statusCode -eq 'StateTransitionNotRequired'){'정적·상태 계약 불필요'}else{('{0}: {1} → {2} → {3} / {4}' -f $item.stateTransition.statusCode,(@($item.stateTransition.before)-join ','),(@($item.stateTransition.active)-join ','),(@($item.stateTransition.after)-join ','),(@($item.stateTransition.recovery)-join ','))}
    [void]$builder.AppendLine(('| `{0}` {1} | {2} / {3} | {4} | **{5}** | {6} |' -f $item.h1StableId,(Escape-Cell $item.title),$item.cardKindCode,(Escape-Cell (@($item.sourcePackCodes)-join ', ')),(Escape-Cell $assets),$item.assignmentStatusCode,(Escape-Cell $transition)))
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## H2 조립 결손')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| H2 | 필수 H1 | 상태 | 미배당 필수 H1 |')
[void]$builder.AppendLine('| --- | --- | --- | --- |')
foreach($item in @($ledger.h2Compositions|Sort-Object h2StableId)){[void]$builder.AppendLine(('| `{0}` {1} | {2} | **{3}** | {4} |' -f $item.h2StableId,(Escape-Cell $item.title),(Escape-Cell (@($item.requiredH1Refs)-join '<br>')),$item.readinessCode,(Escape-Cell (@($item.missingRequiredH1CandidateRefs)-join '<br>'))))}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## 경계')
[void]$builder.AppendLine()
[void]$builder.AppendLine('- H 의미 원본에는 Prefab 경로·GUID를 기록하지 않는다.')
[void]$builder.AppendLine('- `PendingVisualReview`는 실제 파일·meta 근거가 있는 검색 후보이며 외형 적합성 승인이나 선택 완료가 아니다.')
[void]$builder.AppendLine('- `PendingStateContract`의 전·중·후는 조사 질문이며 Simulation 상태나 WI 결과를 자동 창작하지 않는다.')
[void]$builder.AppendLine('- 서버 Draft 반입은 `ExactCandidate`가 생긴 뒤 기존 역할·슬롯 구성 UseCase를 통해 수행한다.')
[void]$builder.AppendLine('- Blender 파생형은 기존 Prefab·재질·Animation·Unity 조립으로 판독할 수 없는 결손만 대상으로 한다.')
$markdown=ConvertTo-DeterministicText $builder.ToString()
$machine=ConvertTo-DeterministicText (([ordered]@{schemaVersion='h1-synty-representation-assignments.generated.v1';revision=$ledger.revision;counts=$ledger.counts;planImpacts=$ledger.planImpacts;h1Assignments=$ledger.h1Assignments;h2Compositions=$ledger.h2Compositions;serverDraftImport=$ledger.serverDraftImport}|ConvertTo-Json -Depth 40)+"`n")

if($Mode -eq 'Query'){
    $items=switch($QueryKind){
        'H1' {@($ledger.h1Assignments|Where-Object h1StableId -eq $QueryValue)}
        'H2' {@($ledger.h2Compositions|Where-Object h2StableId -eq $QueryValue)}
        'Plan' {@($ledger.planImpacts|Where-Object planId -eq $QueryValue)}
        'State' {@($ledger.h1Assignments|Where-Object {$_.stateTransition.statusCode -eq $QueryValue})}
        'Status' {@($ledger.h1Assignments|Where-Object assignmentStatusCode -eq $QueryValue)}
    }
    [pscustomobject]@{revision=$ledger.revision;queryKind=$QueryKind;queryValue=$QueryValue;count=@($items).Count;items=@($items)}|ConvertTo-Json -Depth 40
    exit 0
}
$outputFile=Join-Path $repositoryRoot $OutputPath;$machineFile=Join-Path $repositoryRoot $MachineOutputPath
if($Mode -in @('Survey','Write')){
    Write-DeterministicTextIfChanged $outputFile $markdown|Out-Null
    Write-DeterministicTextIfChanged $machineFile $machine|Out-Null
}else{
    Require(Test-Path -LiteralPath $outputFile -PathType Leaf) 'GeneratedMarkdownMissing'
    Require((ConvertTo-DeterministicText (Get-Content -LiteralPath $outputFile -Raw -Encoding UTF8)) -ceq $markdown) 'GeneratedMarkdownOutOfDate'
    Require(Test-Path -LiteralPath $machineFile -PathType Leaf) 'GeneratedJsonMissing'
    Require((ConvertTo-DeterministicText (Get-Content -LiteralPath $machineFile -Raw -Encoding UTF8)) -ceq $machine) 'GeneratedJsonOutOfDate'
}
Write-Output ("H1SyntyRepresentationAssignmentsValid:Plans={0};H1={1};H2={2};PendingVisualReview={3};BlenderRequired={4};NoCandidate={5};ExistingTransitions={6};PendingTransitions={7}" -f $ledger.counts.plans,$ledger.counts.h1,$ledger.counts.h2,@($ledger.h1Assignments|Where-Object assignmentStatusCode -eq 'PendingVisualReview').Count,@($ledger.h1Assignments|Where-Object assignmentStatusCode -eq 'BlenderRequired').Count,@($ledger.h1Assignments|Where-Object assignmentStatusCode -eq 'NoCandidate').Count,@($ledger.h1Assignments|Where-Object {$_.stateTransition.statusCode -eq 'ExistingE4Preparation'}).Count,@($ledger.h1Assignments|Where-Object {$_.stateTransition.statusCode -eq 'PendingStateContract'}).Count)
