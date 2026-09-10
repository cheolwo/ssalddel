[CmdletBinding()]
param([ValidateSet('Prepare','Build','Up','Status','Start','Result','Stop','NewSample')][string]$Action = 'Status')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot 'artifacts/local/verification/food-observer'
$envFile = Join-Path $artifactRoot '.env'
$connectionFile = Join-Path $artifactRoot 'connection.json'
$composeFile = Join-Path $PSScriptRoot 'docker-compose.food-observer.yml'
if ($Action -eq 'Prepare') {
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    if (-not (Test-Path -LiteralPath $envFile)) {
        function New-LocalSecret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
        $values = [ordered]@{
            FOOD_OBSERVER_DB_PASSWORD = (New-LocalSecret)
            FOOD_OBSERVER_ROOT_PASSWORD = (New-LocalSecret)
            FOOD_OBSERVER_ACCESS_KEY = (New-LocalSecret)
            FOOD_OBSERVER_ACCOUNT_PASSWORD = ('Aa1' + (New-LocalSecret))
            FOOD_OBSERVER_JWT_KEY = (New-LocalSecret)
            FOOD_OBSERVER_AES_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            FOOD_OBSERVER_HASH_SALT = (New-LocalSecret)
        }
        [IO.File]::WriteAllLines($envFile, @($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }), [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($connectionFile, (@{baseUrl='http://127.0.0.1:5215';accessKey=$values.FOOD_OBSERVER_ACCESS_KEY} | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    }
    Write-Output 'Prepared isolated food observer credentials (values omitted).'
    return
}
if (-not (Test-Path -LiteralPath $envFile) -or -not (Test-Path -LiteralPath $connectionFile)) { throw 'Run -Action Prepare first.' }
$composeArgs = @('compose','--project-name','ssalddel-food-observer','--env-file',$envFile,'-f',$composeFile)
switch ($Action) {
    'Build' { & docker @composeArgs build app; if ($LASTEXITCODE) { throw 'Observer image build failed.' }; return }
    'Up' {
        if (-not (Test-Path -LiteralPath (Join-Path $artifactRoot 'menus.json') -PathType Leaf)) { throw 'Run export-food-observer-menus.ps1 first. No fixed-menu fallback.' }
        & docker @composeArgs up -d; if ($LASTEXITCODE) { throw 'Observer startup failed.' }; return
    }
    'Stop' { & docker @composeArgs stop; if ($LASTEXITCODE) { throw 'Observer stop failed.' }; return }
}
$connection = Get-Content -LiteralPath $connectionFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($connection.baseUrl -ne 'http://127.0.0.1:5215') { throw 'Only the isolated loopback observer endpoint is allowed.' }
$headers = @{'X-Verification-Key'=$connection.accessKey}
$url = $connection.baseUrl + '/verification/food-delivery'
if ($Action -eq 'Start') {
    Invoke-RestMethod -Method Post -Uri ($url + '/start') -Headers $headers -TimeoutSec 15 | Out-Null
    Write-Output 'Started 300-second wall-clock verification; scenario duration is unchanged.'
    return
}
$snapshot = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 15
if ($Action -eq 'NewSample') {
    if ($snapshot.status -notin @('Failed','Completed','TimedOut','Waiting')) { throw 'Only a terminal verification can be replaced by a new isolated sample.' }
    $sampleId = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText((Join-Path $artifactRoot "previous-$sampleId.json"), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    & docker @composeArgs stop
    if ($LASTEXITCODE) { throw 'Previous sample stop failed; volume configuration unchanged.' }
    $lines = @(Get-Content -LiteralPath $envFile | Where-Object { $_ -notmatch '^FOOD_OBSERVER_(APP|MYSQL|MONGO)_VOLUME=' })
    foreach ($kind in @('APP','MYSQL','MONGO')) { $lines += "FOOD_OBSERVER_${kind}_VOLUME=ssalddel-food-observer_${sampleId}_$($kind.ToLowerInvariant())" }
    [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
    Write-Output "Previous DB/result volumes preserved. New sample $sampleId selected; run -Action Up, then explicitly Start."
    return
}
if ($Action -eq 'Result') {
    [IO.File]::WriteAllText((Join-Path $artifactRoot 'result.json'), ($snapshot | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
}
$snapshot | Select-Object status,elapsedSeconds,durationSeconds,orderNo,orderStatus,dispatchStatus,message | ConvertTo-Json
