[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$source="$root/artifacts/local/public-data/myeonmok-business-20260908-r1/address-review.json"
$expected='A48B62059B2FE2B6B5D5165949C1C40ECFAD25292154B59BD64FD20EC6A3F41D'
if((Get-FileHash $source).Hash -ne $expected){throw 'WideSourceChanged'}
$data=Get-Content $source -Raw -Encoding utf8|ConvertFrom-Json
$profile=Get-Content "$root/eng/world-seedbeds/placement-map-profiles/neighborhood-market-extension.v1.json" -Raw -Encoding utf8|ConvertFrom-Json
$profile.halfExtentMeters=3000;$profile.rawSha256=$expected
. "$root/eng/world-seedbeds/NeighborhoodGeographicProjection.ps1"
$markers=[Collections.Generic.List[object]]::new()
foreach($f in $data.features) {
    $o=$f.observation
    if($o.Kind -ne 'shop'){continue}
    if($f.QualityCode -ne 'PendingHumanReview' -or $o.CoordinateSystem -ne 'LongitudeLatitude;CRSNotExplicitlyConfirmed'){throw 'WideSourceContractChanged'}
    $profile.features=@(@{id=$f.StableId;name=$o.Name;latitude=$o.CoordinateY;longitude=$o.CoordinateX})
    $p=Get-NeighborhoodMarketPoints $profile -MaximumExtentMeters 3000
    $markers.Add([ordered]@{id=$p.id;name=$o.Name;x=$p.x;z=$p.z;address=$o.RoadAddress;category=$o.Attributes.category;floor=$o.Attributes.floor;source=$f.SourceId;date=$f.EvidenceAsOfUtc})
}
if($markers.Count -ne 5411 -or @($markers.id|Sort-Object -Unique).Count -ne 5411){throw 'WideCountMismatch'}
$output=[ordered]@{revision='myeonmok-wide-reference.r1';sourceHash=$expected;coordinateNote='Source longitude/latitude projected as WGS84 for private review; datum not explicitly confirmed; not entrance or footprint';markers=@($markers)}
$path="$root/artifacts/local/public-data/myeonmok-business-20260908-r1/wide-reference.json"
$json=$output|ConvertTo-Json -Depth 8 -Compress
if(Test-Path $path){if((Get-Content $path -Raw -Encoding utf8) -cne $json){throw 'WideOutputExistsWithDifferentContent'}}
else{[IO.File]::WriteAllText($path,$json,[Text.UTF8Encoding]::new($false))}
[pscustomobject]@{count=$markers.Count;hash=(Get-FileHash $path).Hash;path=$path}
