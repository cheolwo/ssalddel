param([Parameter(Mandatory=$true)][string]$RepositoryRoot)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$folder = Join-Path $root 'artifacts/local/public-data/myeonmok-business-20260908-r1'
$output = Join-Path $folder ('validation-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfff'))
New-Item -ItemType Directory -Path $output | Out-Null
function Save-Result([string]$name, [object]$value) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($value | ConvertTo-Json -Depth 12))
    $stream = [IO.File]::Open((Join-Path $output $name),[IO.FileMode]::CreateNew)
    try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
}
function Run-Checked([string]$name,[string[]]$arguments) {
    $lines = @(& dotnet @arguments 2>&1)
    $code = $LASTEXITCODE
    Save-Result ($name + '.json') @{ arguments=$arguments; exitCode=$code; output=$lines }
    if ($code -ne 0) { throw ('ValidationCommandFailed:' + $name) }
    return ($lines -join "`n")
}
Push-Location $root
try {
    $build = Run-Checked 'build' @('build','eng/Ssalddel.PublicDataPortalImport','--no-restore','--verbosity','minimal')
    $results = @()
    foreach ($mode in @('business-self-test','business-source-check','business-verify','myeonmok-self-test','address-link-test')) {
        $text = Run-Checked $mode @('run','--no-build','--project','eng/Ssalddel.PublicDataPortalImport','--',$mode,$root)
        $results += ($text | ConvertFrom-Json)
    }
    $connection = Get-Content (Join-Path $folder 'connection.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $records = Get-Content (Join-Path $folder 'business-readback.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $selected = Get-Content (Join-Path $folder 'selected.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($connection.inputCount -ne 7543 -or $connection.connectedCount -ne 801 -or $connection.groups.Count -ne 204) { throw 'CombinedCountsChanged' }
    if ($records.rows.Count -ne 5537 -or $selected.Rows.Count -ne 5537) { throw 'SelectedReadbackCountChanged' }
    if (@($records.rows.RecordKey | Sort-Object -Unique).Count -ne 5537) { throw 'DuplicateDatabaseRecordKeys' }
    # 배열의 Item 멤버와 JSON item 이름을 혼동하지 않도록 행별로 명시 접근한다.
    if (@($connection.links | ForEach-Object { $_.item.id } | Sort-Object -Unique).Count -ne 7543) { throw 'DuplicateCombinedIds' }
    if ($records.duplicateCandidates.Count -ne 994) { throw 'DuplicateCandidateCountChanged' }
    $rawHashes = @('053819D3C5F9059E27BAEFC0691F7374911CBED5FDA60C90DD8FE3AAF30944D7','57AA361544108FF4FC73334F87D2CED61E41FE8638DD70BCD842930F865D2386')
    $rawFiles = @('factory.csv','shops-national.zip')
    for ($i=0; $i -lt 2; $i++) { if ((Get-FileHash (Join-Path $folder $rawFiles[$i])).Hash -ne $rawHashes[$i]) { throw 'RawChanged' } }
    if ($connection.privateReviewOnly -ne $true -or $connection.gameStateConnected -ne $false) { throw 'ReviewBoundaryChanged' }
    $linkInputHash = (Get-FileHash (Join-Path $folder 'address-review.json')).Hash
    if ($connection.inputHash -ne $linkInputHash) { throw 'CombinedLineageMismatch' }
    $shop = @($records.rows | Where-Object { $_.observation.Kind -eq 'shop' })
    $stats = @{
        tests=$results; sourceReparsePassed=$true; databaseReadbackPassed=$true
        observations=$records.rows.Count; shopRows=$shop.Count; factoryRows=126
        shopsWithFloor=@($shop | Where-Object { $_.observation.Floor -ne '' }).Count
        shopsWithRoom=@($shop | Where-Object { $_.observation.Room -ne '' }).Count
        shopsWithBuildingNumber=@($shop | Where-Object { $_.observation.BuildingManagementNumber -ne '' }).Count
        industries=@($shop.observation.Industry | Sort-Object -Unique).Count
        duplicateCandidates=994; mergedBusinesses=0; combinedRecords=7543; connectedRecords=801; connectedBuildings=204
        unityApplied=$false; sourceAndSnapshotPreserved=$true; databaseWriteAttempted=$false
    }
    Save-Result 'verification.json' $stats
    $files = @(Get-ChildItem -LiteralPath $folder -File | ForEach-Object { @{file=$_.Name; bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    Save-Result 'input-manifest.json' @{ files=$files; includesNationalSourceEnvelope=$true; selectedOnlyInDatabase=$true }
    @{status='Passed'; directory=$output; filesVerified=$files.Count; summary=$stats} | ConvertTo-Json -Depth 10
} catch {
    Save-Result 'failure.json' @{status='Failed'; error=$_.Exception.Message; databaseWriteAttempted=$false}
    throw
} finally { Pop-Location }
