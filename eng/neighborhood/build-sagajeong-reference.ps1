[CmdletBinding()]
param(
    [string]$RawPath='artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm',
    [string]$OutputPath='C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$expected='3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3'
$raw=Join-Path $root $RawPath
if((Get-FileHash $raw -Algorithm SHA256).Hash -ne $expected){throw 'SagajeongSourceHashMismatch'}
[xml]$osm=Get-Content $raw -Raw -Encoding UTF8
$profile=Get-Content "$root/eng/world-seedbeds/placement-map-profiles/neighborhood-market-extension.v1.json" -Raw -Encoding UTF8|ConvertFrom-Json
$station=$osm.osm.node|Where-Object id -eq '6046633864'
if([double]$station.lat -ne $profile.originLatitude -or [double]$station.lon -ne $profile.originLongitude){throw 'StationOriginMismatch'}
. "$root/eng/world-seedbeds/NeighborhoodGeographicProjection.ps1"
$lookup=@{}; $batch=@()
foreach($node in $osm.osm.node) {
    $batch+= [pscustomobject]@{id=[string]$node.id;name='';latitude=[double]$node.lat;longitude=[double]$node.lon}
    if($batch.Count -eq 100) {
        $profile.features=$batch
        foreach($p in @(Get-NeighborhoodMarketPoints $profile -SkipOutsideExtent)){$lookup[$p.id]=$p}
        $batch=@()
    }
}
if($batch.Count){$profile.features=$batch; foreach($p in @(Get-NeighborhoodMarketPoints $profile -SkipOutsideExtent)){$lookup[$p.id]=$p}}
$roads=[Collections.Generic.List[object]]::new();$buildings=[Collections.Generic.List[object]]::new()
$complexWays=[Collections.Generic.HashSet[string]]::new()
foreach($relation in $osm.osm.relation) {
    if(@($relation.tag|Where-Object {$_.k -eq 'type' -and $_.v -eq 'multipolygon'}).Count) {
        foreach($member in $relation.member){if($member.type -eq 'way'){$null=$complexWays.Add([string]$member.ref)}}
    }
}
$omittedBuildings=0;$omittedSegments=0
foreach($way in $osm.osm.way) {
    $tags=@{};foreach($tag in $way.tag){$tags[[string]$tag.k]=[string]$tag.v}
    $refs=@($way.nd|ForEach-Object {[string]$_.ref})
    if($tags.ContainsKey('building') -and $tags.building -ne 'no') {
        if($complexWays.Contains([string]$way.id)){$omittedBuildings++;continue}
        if($refs.Count -lt 4 -or $refs[0] -ne $refs[-1] -or @($refs|Where-Object {!$lookup.ContainsKey($_)}).Count){$omittedBuildings++;continue}
        # 외곽 벽과 윤곽만 표현. 복합 도형·내곽은 이 표본에서 다루지 않는다.
        $polygon=@($refs|ForEach-Object {@{x=$lookup[$_].x;z=$lookup[$_].z}})
        $height=4.0;$heightKind='SymbolicHeight'
        $parsed=0.0
        if($tags.ContainsKey('height') -and [double]::TryParse($tags.height,[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$parsed) -and $parsed -gt 0 -and $parsed -le 200){$height=$parsed;$heightKind='OsmHeightTag'}
        # 주소/층수는 원문 속성이다. 층수로 높이를 추정하거나 가상 동·호수를 만들지 않는다.
        $buildings.Add(@{id='osm:way:'+$way.id;version=[int]$way.version;height=$height;heightKind=$heightKind;points=$polygon;
            name=[string]$tags.name;street=[string]$tags['addr:street'];houseNumber=[string]$tags['addr:housenumber'];
            unit=[string]$tags['addr:unit'];levelsText=[string]$tags['building:levels'];buildingKind=[string]$tags.building})
    }
    if($tags.ContainsKey('highway') -and $tags.highway -notin @('proposed','construction')) {
        if($tags.tunnel -eq 'yes' -or $tags.bridge -eq 'yes'){continue}
        for($i=1;$i -lt $refs.Count;$i++) {
            if(!$lookup.ContainsKey($refs[$i-1]) -or !$lookup.ContainsKey($refs[$i])){$omittedSegments++;continue}
            $a=$lookup[$refs[$i-1]];$b=$lookup[$refs[$i]]
            $roads.Add(@{id='osm:way:'+$way.id+':segment:'+($i-1);kind=$tags.highway;x1=$a.x;z1=$a.z;x2=$b.x;z2=$b.z})
        }
    }
}
$output=[ordered]@{revision='sagajeong-reference.r2';rawSha256=$expected;sourceUrl='https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854';attribution='© OpenStreetMap contributors · ODbL 1.0';licenseUrl='https://www.openstreetmap.org/copyright';originNode='osm:node:6046633864';originLatitude=$profile.originLatitude;originLongitude=$profile.originLongitude;coordinateMethod=$profile.coordinateMethod;offsetX=$profile.offsetX;offsetZ=$profile.offsetZ;halfExtent=500;roads=@($roads);buildings=@($buildings);omittedBoundaryBuildings=$omittedBuildings;omittedBoundarySegments=$omittedSegments;boundary='ReferenceOnly;NoNavigation;WidthSymbolic;UnknownHeight4m;NoTerrain;WholeFeaturesInsideSquareOnly'}
$output.revision='sagajeong-reference.r3'
$null=New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath)
[IO.File]::WriteAllText($OutputPath,($output|ConvertTo-Json -Depth 10 -Compress),[Text.UTF8Encoding]::new($false))
"roads=$($roads.Count) buildings=$($buildings.Count) omittedBuildings=$omittedBuildings omittedSegments=$omittedSegments output=$OutputPath"
