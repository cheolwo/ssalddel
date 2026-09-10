param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# 브라우저의 실제 JSON 다운로드 요청과 공식 메타데이터 링크에서 확인한 공개 주소만 사용한다.
$urls = [ordered]@{
    'markets.json' = 'https://www.data.go.kr/download/standard.json?publicDataPk=15012894&colNmList=MRKT_NM&colNmList=MRKT_TYPE&colNmList=RDNMADR&colNmList=LNMADR&colNmList=MRKT_ESTBL_CYCLE&colNmList=LATITUDE&colNmList=LONGITUDE&colNmList=STOR_NUMBER&colNmList=TRTMNT_PRDLST&colNmList=USE_GCCT&colNmList=HOMEPAGE_URL&colNmList=PBLIC_TOILET_YN&colNmList=PRKPLCE_YN&colNmList=ESTBL_YEAR&colNmList=PHONE_NUMBER&colNmList=REFERENCE_DATE&totalCount=1393&svcTableNm=tn_pubr_public_trdit_mrkt_svc&perPage=10000&page=1'
    'metadata.json' = 'https://www.data.go.kr/catalog/15012894/standard.json'
    'metadata.rdf' = 'https://www.data.go.kr/biz/dcat/metadata/15012894.do'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'AcquisitionDirectoryAlreadyExists' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $directory | Out-Null
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
$client.MaxResponseContentBufferSize = 5MB
$files = @()
try {
    foreach ($entry in $urls.GetEnumerator()) {
        $response = $client.GetAsync($entry.Value).GetAwaiter().GetResult()
        try {
            $response.EnsureSuccessStatusCode() | Out-Null
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            if ($bytes.Length -eq 0 -or $bytes.Length -gt 5MB) { throw 'PayloadSizeInvalid' }
            $path = Join-Path $directory $entry.Key
            $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
            $files += [ordered]@{ file=$entry.Key; url=$entry.Value; collectedAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); contentType=$response.Content.Headers.ContentType.ToString(); bytes=$bytes.Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        } finally { $response.Dispose() }
    }
    $rows = @(Get-Content -LiteralPath (Join-Path $directory 'markets.json') -Raw | ConvertFrom-Json)
    $selected = @($rows | Where-Object { $_.RDNMADR.StartsWith('서울특별시 중랑구 ',[StringComparison]::Ordinal) })
    if ($rows.Count -ne 1393 -or $selected.Count -ne 12) { throw 'ObservedRowCountChanged' }
    if (!(Get-Content -LiteralPath (Join-Path $directory 'metadata.rdf') -Raw).Contains('이용허락범위 제한 없음')) { throw 'LicenseEvidenceChanged' }
    $receipt = [ordered]@{ schema='public-data-portal-acquisition.r1'; officialPage='https://www.data.go.kr/data/15012894/standard.do'; sourceId='semas-traditional-market-status'; datasetId='data-go-kr-15012894-standard'; sourceVersion='reference-date:2025-11-10'; dataRevision='jungnang-markets-20260908.r1'; acquiredRows=$rows.Count; selectedRows=$selected.Count; selector='RDNMADR starts with 서울특별시 중랑구 '; licenseObserved='이용허락범위 제한 없음 (DCAT)'; reviewStatus='PendingHumanReview'; publicationAllowed=$false; runtimeAuthorized=$false; files=$files }
    $text = $receipt | ConvertTo-Json -Depth 8
    $out = [IO.File]::Open((Join-Path $directory 'acquisition.json'),[IO.FileMode]::CreateNew)
    try { $encoded=[Text.UTF8Encoding]::new($false).GetBytes($text); $out.Write($encoded,0,$encoded.Length) } finally { $out.Dispose() }
    $receipt | ConvertTo-Json -Depth 8
} finally { $client.Dispose() }
