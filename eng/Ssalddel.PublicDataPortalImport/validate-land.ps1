param([Parameter(Mandatory=$true)][string]$RepositoryRoot)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$folder = Join-Path $root 'artifacts/local/public-data/myeonmok-land-20260908-r1'
$output = Join-Path $folder ('validation-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
function Save-Result([string]$name,[object]$value) {
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($value | ConvertTo-Json -Depth 20))
    $stream=[IO.File]::Open((Join-Path $output $name),[IO.FileMode]::CreateNew)
    try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
}
function Run-Checked([string]$name,[string[]]$arguments) {
    $lines=@(& dotnet @arguments 2>&1); $code=$LASTEXITCODE
    Save-Result ($name+'.json') @{arguments=$arguments;exitCode=$code;output=$lines}
    if($code -ne 0){throw ('ValidationCommandFailed:'+ $name)}
    return ($lines -join "`n")
}
Push-Location $root
try {
    $null=Run-Checked 'build' @('build','eng/Ssalddel.PublicDataPortalImport','--no-restore','--verbosity','minimal')
    $tests=@()
    foreach($mode in @('land-self-test','land-replay','land-verify','land-inventory','business-self-test','myeonmok-self-test','address-link-test')) {
        $tests+=((Run-Checked $mode @('run','--no-build','--project','eng/Ssalddel.PublicDataPortalImport','--',$mode,$root)) | ConvertFrom-Json)
    }
    $review=Get-Content (Join-Path $folder 'review.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $selection=Get-Content (Join-Path $folder 'selection.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $gis=Get-Content (Join-Path $folder 'gis-sample.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $acquire=Get-Content (Join-Path $folder 'acquisition.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($review.rows.Count -ne 30 -or @($selection.Samples.Id | Sort-Object -Unique).Count -ne 30){throw 'SelectionCountOrDuplicates'}
    if(@($selection.Samples | Group-Object Group | Where-Object Count -ne 5).Count -ne 0){throw 'SampleGroupSize'}
    $counts=@{};foreach($g in ($review.rows | Group-Object {$_.GisBuilding.Status})){$counts[$g.Name]=$g.Count}
    if($counts['주소단일후보'] -ne 14 -or $counts['복수후보'] -ne 8 -or $counts['연결불가'] -ne 8){throw 'CandidateCountsChanged'}
    if($gis.RowsRead -ne 695761 -or $gis.MyeonmokRows -ne 13389 -or $gis.Rows.Count -ne 40){throw 'GisCountsChanged'}
    if(@($gis.Rows | Where-Object {$_.Geometry.status -ne 'FiniteClosedRings;TopologyNotValidated'}).Count -ne 0){throw 'GisStructureChanged'}
    if(@($review.rows | Where-Object {$_.OfficialBuilding.Status -ne '연결불가' -or $_.ParcelRoad.Status -ne '연결불가'}).Count -ne 0){throw 'UnapprovedLinkPromotion'}
    if($acquire.buildingQueries.Count -ne 23 -or @($acquire.buildingQueries | Where-Object status -eq 'Blocked').Count -ne 1 -or @($acquire.buildingQueries | Where-Object status -eq 'NotRequested').Count -ne 22){throw 'AcquisitionBoundaryChanged'}
    if($review.privateReviewOnly -ne $true -or $review.gameStateConnected -ne $false){throw 'ReviewBoundaryChanged'}
    $manifest=Get-Content (Join-Path $folder 'review-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach($f in $manifest){$p=Join-Path $folder $f.Path;if((Get-FileHash -LiteralPath $p).Hash -ne $f.Sha256 -or (Get-Item -LiteralPath $p).Length -ne $f.Bytes){throw 'ReviewManifestDrift'}}
    $business=Get-Content (Join-Path $root 'artifacts/local/public-data/myeonmok-business-20260908-r1/business-readback.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $numbers=@($business.rows | Where-Object {$_.observation.BuildingManagementNumber})
    if($numbers.Count -ne 5404 -or @($numbers | Where-Object {$_.observation.BuildingManagementNumber -notmatch '^\d{25}$'}).Count -ne 0){throw 'BuildingNumberShapeChanged'}
    $restaurants=@($review.rows | Where-Object {$_.Sample.Group -eq '음식'} | ForEach-Object {$_.RestaurantComparisons})
    $summary=@{status='PassedForExecutedScope';tests=$tests;sampleCount=30;candidateCounts=$counts;manifestFiles=$manifest.Count;gisGeometryStructuralCount=40;geometryTopologyVerified=$false;officialBuildingLinks=0;completeBuildingParcelRoadChains=0;buildingNumbers25Digits=5404;restaurantComparisons=$restaurants.Count;sameRestaurantName=@($restaurants | Where-Object sameName -eq $true).Count;restaurantStatusMissing=@($restaurants | Where-Object {$_.status -eq ''}).Count;databaseWriteAttempted=$false;unityApplied=$false}
    Save-Result 'verification.json' $summary
    Save-Result 'input-manifest.json' @(Get-ChildItem -LiteralPath $folder -File | ForEach-Object {@{file=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    @{directory=$output;summary=$summary} | ConvertTo-Json -Depth 15
} catch { Save-Result 'failure.json' @{status='Failed';error=$_.Exception.Message;databaseWriteAttempted=$false}; throw }
finally {Pop-Location}
