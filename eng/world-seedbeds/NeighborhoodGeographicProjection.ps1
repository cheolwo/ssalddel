# WGS84 타원체의 지역 접평면. EPSG:5186 도로 검사 입력과 구별한다.
function Get-NeighborhoodMarketPoints($extension, [switch]$SkipOutsideExtent, [int]$MaximumExtentMeters = 500) {
    function Require-Geo($condition, $code) { if (-not $condition) { throw "NeighborhoodGeographyInvalid:$code" } }
    function Finite($value) { return $null -ne $value -and -not [double]::IsNaN([double]$value) -and -not [double]::IsInfinity([double]$value) }
    function Ecef([double]$lat, [double]$lon) {
        $p=$lat*[Math]::PI/180; $l=$lon*[Math]::PI/180
        $n=6378137/[Math]::Sqrt(1-0.0066943799901413165*[Math]::Sin($p)*[Math]::Sin($p))
        return @(($n*[Math]::Cos($p)*[Math]::Cos($l)), ($n*[Math]::Cos($p)*[Math]::Sin($l)), ($n*(1-0.0066943799901413165)*[Math]::Sin($p)))
    }
    Require-Geo ($extension.coordinateMethod -eq 'WGS84-ECEF-ENU-at-zero-altitude') 'CoordinateMethod'
    foreach($v in @($extension.originLatitude,$extension.originLongitude,$extension.offsetX,$extension.offsetZ,$extension.halfExtentMeters,$extension.metersPerUnit)) { Require-Geo (Finite $v) 'NonFinite' }
    Require-Geo ([Math]::Abs($extension.originLatitude) -le 85 -and [Math]::Abs($extension.originLongitude) -le 180) 'OriginRange'
    Require-Geo ($MaximumExtentMeters -ge 500 -and $MaximumExtentMeters -le 3000 -and $extension.halfExtentMeters -gt 0 -and $extension.halfExtentMeters -le $MaximumExtentMeters -and $extension.metersPerUnit -eq 1) 'ExtentOrScale'
    Require-Geo ($extension.rawSha256 -cmatch '^[A-Fa-f0-9]{64}$') 'SourceHash'
    Require-Geo (@($extension.features).Count -gt 0 -and @($extension.features).Count -le 100) 'FeatureCount'
    $origin=Ecef $extension.originLatitude $extension.originLongitude
    $p=$extension.originLatitude*[Math]::PI/180; $l=$extension.originLongitude*[Math]::PI/180
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($f in $extension.features) {
        Require-Geo ($f.id -cmatch '^[a-zA-Z0-9:._-]+$' -and $ids.Add($f.id)) 'FeatureIdentity'
        Require-Geo ((Finite $f.latitude) -and (Finite $f.longitude) -and [Math]::Abs($f.latitude) -le 85 -and [Math]::Abs($f.longitude) -le 180) 'CoordinateRange'
        $q=Ecef $f.latitude $f.longitude; $dx=$q[0]-$origin[0];$dy=$q[1]-$origin[1];$dz=$q[2]-$origin[2]
        $east=-[Math]::Sin($l)*$dx+[Math]::Cos($l)*$dy
        $north=-[Math]::Sin($p)*[Math]::Cos($l)*$dx-[Math]::Sin($p)*[Math]::Sin($l)*$dy+[Math]::Cos($p)*$dz
        $inside = [Math]::Abs($east) -le $extension.halfExtentMeters -and [Math]::Abs($north) -le $extension.halfExtentMeters
        if (-not $inside -and $SkipOutsideExtent) { continue }
        Require-Geo $inside 'OutsideExtent'
        [pscustomobject]@{id=$f.id; name=$f.name; x=[Math]::Round($east+$extension.offsetX,3); z=[Math]::Round($north+$extension.offsetZ,3)}
    }
}
