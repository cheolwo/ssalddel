$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. "$root/eng/world-seedbeds/NeighborhoodGeographicProjection.ps1"
$profilePath="$root/eng/world-seedbeds/placement-map-profiles/neighborhood-market-extension.v1.json"
function Profile { Get-Content $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json }
function Assert($condition,$name) { if(!$condition){throw "Failed:$name"}; "PASS $name" }
$p=Profile; $points=@(Get-NeighborhoodMarketPoints $p)
$originProfile=Profile
$originProfile.features=@([pscustomobject]@{id='station';name='사가정';latitude=$p.originLatitude;longitude=$p.originLongitude})
$origin=@(Get-NeighborhoodMarketPoints $originProfile)
Assert ($points.Count -eq 3 -and $origin[0].x -eq 550 -and $origin[0].z -eq 8) 'origin'
Assert ($points[1].x -lt 550 -and $points[1].z -lt 8 -and $points[2].x -gt 550 -and $points[2].z -gt 8) 'southwest-northeast'
# 사가정역 원점과 두 지점은 약 500m, 96m 거리다.
foreach($i in 1,2) {
    $d=[Math]::Sqrt([Math]::Pow($points[$i].x-550,2)+[Math]::Pow($points[$i].z-8,2))
    Assert ($d -gt @(495,90)[$i-1] -and $d -lt @(505,100)[$i-1]) "distance-$i"
}
foreach($case in @('duplicate','range','outside','scale','nan')) {
    $p=Profile
    switch($case) {
        duplicate {$p.features[1].id=$p.features[0].id}
        range {$p.features[1].latitude=91}
        outside {$p.features[1].latitude=38}
        scale {$p.metersPerUnit=0}
        nan {$p.offsetX=[double]::NaN}
    }
    $failed=$false
    try {$null=@(Get-NeighborhoodMarketPoints $p)} catch { $failed=$_.Exception.Message.StartsWith('NeighborhoodGeographyInvalid:') }
    Assert $failed "reject-$case"
}
$raw="$root/artifacts/local/public-data/eight-life-domains/market-20260908-r1/markets.json"
if(Test-Path $raw) {
    $p=Profile
    Assert ((Get-FileHash $raw -Algorithm SHA256).Hash -eq $p.rawSha256) 'frozen-source-hash'
    $rows=Get-Content $raw -Raw -Encoding UTF8|ConvertFrom-Json
    foreach($f in $p.features) {
        $row=@($rows|Where-Object MRKT_NM -eq $f.name)
        Assert ($row.Count -eq 1 -and [decimal]$row[0].LATITUDE -eq [decimal]$f.latitude -and [decimal]$row[0].LONGITUDE -eq [decimal]$f.longitude -and $row[0].REFERENCE_DATE -eq $p.referenceDate) "source-row-$($f.id)"
    }
} else { 'SKIP local source bytes unavailable; source provenance not reverified' }
& "$root/eng/tests/neighborhood-maps.ps1"
