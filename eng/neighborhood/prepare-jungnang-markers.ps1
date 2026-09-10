[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$ExpectedSha256,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$inputFile=[IO.Path]::GetFullPath($InputPath)
$outputFile=[IO.Path]::GetFullPath($OutputPath)
$allowed=Join-Path $root 'artifacts/local/public-data/jungnang-spatial-20260908-r2/'
if(-not $inputFile.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or -not $outputFile.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'PrivateArtifactPathRequired' }
if(Test-Path -LiteralPath $outputFile) { throw 'OutputAlreadyExists' }
$hash=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
if($hash -cne $ExpectedSha256.ToUpperInvariant()) { throw 'ReadbackExportHashMismatch' }
$data=Get-Content -LiteralPath $inputFile -Raw -Encoding utf8 | ConvertFrom-Json
if($data.schema -ne 'jungnang-location-review.r1' -or $data.coordinateSystem -ne 'EPSG:4326' -or $data.region -ne 'region:kr:sig:11260' -or $data.publicationApproved -or $data.gameStateConnected) { throw 'InputContractMismatch' }
$profilePath=Join-Path $root 'eng/world-seedbeds/placement-map-profiles/neighborhood-market-extension.v1.json'
$profile=Get-Content -LiteralPath $profilePath -Raw -Encoding utf8 | ConvertFrom-Json
$profileHash=(Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash
$projectionPath=Join-Path $root 'eng/world-seedbeds/NeighborhoodGeographicProjection.ps1'
$projectionHash=(Get-FileHash -LiteralPath $projectionPath -Algorithm SHA256).Hash
. $projectionPath
$ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$markers=[Collections.Generic.List[object]]::new()
$unplaced=[Collections.Generic.List[object]]::new()
$profile.rawSha256=$hash
foreach($feature in $data.features) {
    if(-not $ids.Add($feature.StableId) -or $feature.QualityCode -ne 'PendingHumanReview') { throw 'FeatureIdentityOrQualityMismatch' }
    $o=$feature.observation
    if($null -eq $o.Latitude -or $null -eq $o.Longitude) {
        $unplaced.Add([ordered]@{id=$feature.StableId;name=$o.Name;reason='SourceCoordinateUnavailable'})
        continue
    }
    $profile.features=@(@{id=$feature.StableId;name=$o.Name;latitude=$o.Latitude;longitude=$o.Longitude})
    $points=@(Get-NeighborhoodMarketPoints $profile -SkipOutsideExtent)
    if($points.Count -eq 0) { $unplaced.Add([ordered]@{id=$feature.StableId;name=$o.Name;reason='OutsideExistingSagajeongSquare'}); continue }
    if($points.Count -ne 1) { throw 'PointCountMismatch' }
    $p=$points[0]
    $markers.Add([ordered]@{id=$p.id;name=$p.name;kind=$o.Kind;x=$p.x;z=$p.z;heightMetres=$null;referenceDate=$o.ReferenceDate;
        sourceId=$feature.SourceId;datasetId=$feature.DatasetId;rawSnapshotId=$feature.RawSnapshotId;rawSha256=$feature.sourceHash;
        recordSha256=$o.RawRecordSha256;quality=$feature.QualityCode;geometry='ReferencePointNotEntranceOrFootprint'})
}
if((Get-FileHash $inputFile).Hash -ne $hash -or (Get-FileHash $profilePath).Hash -ne $profileHash -or (Get-FileHash $projectionPath).Hash -ne $projectionHash) { throw 'InputChanged' }
$result=[ordered]@{revision='jungnang-sagajeong-markers.r1';inputHash=$hash;inputCount=$ids.Count;markerCount=$markers.Count;unplacedCount=$unplaced.Count;
    coordinateMethod=$profile.coordinateMethod;originLatitude=$profile.originLatitude;originLongitude=$profile.originLongitude;
    offsetX=$profile.offsetX;offsetZ=$profile.offsetZ;halfExtentMeters=$profile.halfExtentMeters;metersPerUnit=$profile.metersPerUnit;
    profileHash=$profileHash;projectionHash=$projectionHash;unityExecuted=$false;sceneModified=$false;markers=@($markers);unplaced=@($unplaced);
    boundary='Private reference only. Current 1km square unchanged. No height, footprint, entrance, Collider, Prefab, navigation or operating-status inference.'}
$bytes=[Text.UTF8Encoding]::new($false).GetBytes(($result|ConvertTo-Json -Depth 12))
$stream=[IO.File]::Open($outputFile,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
[pscustomobject]@{input=$ids.Count;markers=$markers.Count;unplaced=$unplaced.Count;output=$outputFile;sha256=(Get-FileHash $outputFile).Hash}
