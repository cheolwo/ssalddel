$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repositoryRoot 'eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj'
$output = Join-Path $repositoryRoot 'artifacts/local/public-data/myeonmok-diorama-view-r1'

$null = dotnet build $project --no-restore
if ($LASTEXITCODE -ne 0) { throw 'DioramaBuildFailed' }

$selfTest = dotnet run --project $project --no-build -- diorama-self-test $repositoryRoot | ConvertFrom-Json
if ($selfTest.selfTestsPassed -ne 12 -or $selfTest.databaseWriteAttempted -or $selfTest.unityExecuted) { throw 'DioramaSelfTestFailed' }

$verified = dotnet run --project $project --no-build -- diorama-verify $repositoryRoot | ConvertFrom-Json
if ($verified.buildingOverlays -ne 204 -or $verified.activityObservations -ne 801 -or $verified.landSamples -ne 30 -or $verified.distributionApproved) { throw 'DioramaVerificationFailed' }

$private = Get-Content -LiteralPath (Join-Path $output 'private-spatial-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$projectionText = Get-Content -LiteralPath (Join-Path $output 'presentation-projection.json') -Raw -Encoding UTF8
$projection = $projectionText | ConvertFrom-Json
if (@($private.addresses).Count -ne 204 -or @($private.activityObservations).Count -ne 801 -or @($private.landSampleLinks).Count -ne 30) { throw 'DioramaPrivateCounts' }
if (@($projection.layers).Count -ne 7 -or @($projection.buildingOverlays).Count -ne 204) { throw 'DioramaProjectionCounts' }
if ($projectionText -match '"(name|address|roadAddress|addressKey|normalizedRoadAddress|buildingManagementNumber|observationId)"\s*:') { throw 'DioramaPrivateFieldLeak' }
foreach ($value in @($private.activityObservations | ForEach-Object { $_.name; $_.roadAddress } | Where-Object { $_.Length -ge 4 } | Sort-Object -Unique)) {
    if ($projectionText.Contains($value,[StringComparison]::Ordinal)) { throw 'DioramaPrivateValueLeak' }
}

$duplicateAttempt = dotnet run --project $project --no-build -- diorama-build $repositoryRoot | ConvertFrom-Json
if ($LASTEXITCODE -eq 0 -or $duplicateAttempt.errorCode -ne 'DioramaOutputExists') { throw 'DioramaDuplicateBuildNotRejected' }

Write-Output "PASS diorama generator: 12 self-tests; 204 buildings/801 observations/30 samples; privacy projection verified; duplicate build rejected; DB/Unity writes 0"
