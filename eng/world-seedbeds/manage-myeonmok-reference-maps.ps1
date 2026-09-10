[CmdletBinding()]
param(
    [ValidateSet('Check','Write')][string] $Mode = 'Check',
    [string] $ManifestPath = 'eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json',
    [string] $GraphPath = 'eng/world-seedbeds/graph-maps/jungnang-myeonmok-reference.v1.json',
    [string] $PlacementPath = 'eng/world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v1.json',
    [string] $SagajeongSourcePath = '',
    [switch] $SkipSourceFreshness
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'GraphMapTooling.ps1') -RepositoryRoot $root -ErrorPrefix 'MyeonmokReferenceMapInvalid'

function Property([object] $value, [string] $name) {
    $property = $value.PSObject.Properties[$name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Require-Integer([object] $value, [string] $code) {
    $number = 0L
    Require ([long]::TryParse([string] $value, [ref] $number) -and $number -ge 0) $code
    return $number
}

function Source([object] $manifest, [string] $stableId) {
    $items = @($manifest.sourceSnapshots | Where-Object sourceStableId -eq $stableId)
    Require ($items.Count -eq 1) "SourceMissing:$stableId"
    return $items[0]
}

function Optional-Source([object] $manifest, [string] $stableId) {
    $items = @($manifest.sourceSnapshots | Where-Object sourceStableId -eq $stableId)
    Require ($items.Count -le 1) "SourceDuplicate:$stableId"
    if ($items.Count -eq 0) { return $null }
    return $items[0]
}

function Resolve-SourcePath([object] $source) {
    $relative = [string] (Property $source 'repoRelativePath')
    if (-not [string]::IsNullOrWhiteSpace($relative)) { return Resolve-RepoPath $relative }
    if (-not [string]::IsNullOrWhiteSpace($SagajeongSourcePath)) { return [IO.Path]::GetFullPath($SagajeongSourcePath) }
    $unityRoot = [Environment]::GetEnvironmentVariable('SSALDDEL_UNITY_ROOT')
    if (-not [string]::IsNullOrWhiteSpace($unityRoot)) {
        return [IO.Path]::GetFullPath((Join-Path $unityRoot ([string] $source.logicalResourcePath)))
    }
    $profileRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    return [IO.Path]::GetFullPath((Join-Path (Join-Path $profileRoot 'ssalddel') ([string] $source.logicalResourcePath)))
}

function Read-FrozenSource([object] $source) {
    $path = Resolve-SourcePath $source
    Require (Test-Path -LiteralPath $path -PathType Leaf) "SourceBytesMissing:$($source.sourceStableId)"
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    Require ($hash -ceq ([string] $source.expectedSha256).ToUpperInvariant()) "SourceHashMismatch:$($source.sourceStableId)"
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Tile-Key([int] $x, [int] $z) { return "tile:myeonmok:500m:x$x`:z$z" }

function Tile-Index([double] $worldCoordinate, [double] $offset, [int] $size) {
    return [int] [Math]::Floor(($worldCoordinate - $offset) / $size)
}

function Add-Count([hashtable] $tiles, [int] $x, [int] $z, [string] $name, [int] $amount = 1) {
    $key = Tile-Key $x $z
    Require ($tiles.ContainsKey($key)) "FeatureOutsideFrozenTileRange:$key"
    $tiles[$key][$name] = [int] $tiles[$key][$name] + $amount
}

function Build-Maps([object] $manifest) {
    $frame = $manifest.coordinateFrame
    $size = [int] $frame.tileSizeMeters
    $offsetX = [double] $frame.worldOffsetX
    $offsetZ = [double] $frame.worldOffsetZ
    $shops = Read-FrozenSource (Source $manifest 'source:myeonmok-shop-points:r1')
    $links = Read-FrozenSource (Source $manifest 'source:myeonmok-address-building-links:r1')
    $jungnang = Read-FrozenSource (Source $manifest 'source:jungnang-location-review:r1')
    $publicMarkers = Read-FrozenSource (Source $manifest 'source:jungnang-sagajeong-markers:r1')
    $geography = Read-FrozenSource (Source $manifest 'source:sagajeong-osm-reference:r3')
    $privateIndexSource = Optional-Source $manifest 'source:myeonmok-address-spatial-index:r1'
    $dioramaSource = Optional-Source $manifest 'source:myeonmok-diorama-presentation:r1'
    $privateIndex = $null
    $diorama = $null
    if ($null -ne $privateIndexSource -or $null -ne $dioramaSource) {
        Require ($null -ne $privateIndexSource -and $null -ne $dioramaSource) 'DioramaSourcePairRequired'
        $privateIndex = Read-FrozenSource $privateIndexSource
        $diorama = Read-FrozenSource $dioramaSource
    }

    Require ($shops.revision -eq 'myeonmok-wide-reference.r1' -and @($shops.markers).Count -eq 5411) 'ShopSourceContract'
    Require ($links.schema -eq 'myeonmok-building-reference.r1' -and $links.inputCount -eq 7543 -and $links.connectedCount -eq 801 -and $links.unlinkedCount -eq 6742 -and @($links.groups).Count -eq 204) 'AddressLinkSourceContract'
    Require ($jungnang.schema -eq 'jungnang-location-review.r1' -and $jungnang.region -eq 'region:kr:sig:11260' -and @($jungnang.features).Count -eq 146) 'JungnangSourceContract'
    Require ($publicMarkers.revision -eq 'jungnang-sagajeong-markers.r1' -and $publicMarkers.inputCount -eq 146 -and $publicMarkers.markerCount -eq 10 -and $publicMarkers.unplacedCount -eq 136) 'JungnangMarkerContract'
    Require ($geography.revision -eq 'sagajeong-reference.r3' -and @($geography.buildings).Count -eq 602 -and @($geography.roads).Count -eq 2397) 'GeographySourceContract'
    Require ([double] $geography.originLatitude -eq [double] $frame.originLatitude -and [double] $geography.originLongitude -eq [double] $frame.originLongitude -and [double] $geography.offsetX -eq $offsetX -and [double] $geography.offsetZ -eq $offsetZ) 'CoordinateFrameMismatch'
    if ($null -ne $diorama) {
        Require ($privateIndex.schema -eq 'myeonmok-address-spatial-index.r1' -and $privateIndex.privateReviewOnly -and -not $privateIndex.gameStateConnected -and -not $privateIndex.distributionApproved -and @($privateIndex.addresses).Count -eq 204 -and @($privateIndex.activityObservations).Count -eq 801 -and @($privateIndex.landSampleLinks).Count -eq 30) 'DioramaPrivateIndexContract'
        Require ($diorama.schema -eq 'myeonmok-diorama-presentation.r1' -and $diorama.privateReviewOnly -and -not $diorama.gameStateConnected -and -not $diorama.distributionApproved -and $diorama.mapSha256 -eq ([string] (Source $manifest 'source:sagajeong-osm-reference:r3').expectedSha256) -and @($diorama.layers).Count -eq 7 -and @($diorama.buildingOverlays).Count -eq 204 -and $diorama.counts.activityObservations -eq 801 -and $diorama.counts.landSamples -eq 30) 'DioramaProjectionContract'
        foreach ($propertyName in @('name','address','roadAddress','addressKey','normalizedRoadAddress','buildingManagementNumber','observationId')) {
            Require ((ConvertTo-Json $diorama -Depth 100 -Compress) -notmatch ('"' + [regex]::Escape($propertyName) + '"\s*:')) "DioramaPrivateFieldLeak:$propertyName"
        }
    }

    $tiles = @{}
    for ($x = [int] $frame.tileIndexMinX; $x -le [int] $frame.tileIndexMaxX; $x++) {
        for ($z = [int] $frame.tileIndexMinZ; $z -le [int] $frame.tileIndexMaxZ; $z++) {
            $tiles[(Tile-Key $x $z)] = [ordered]@{
                shopPointCandidates = 0
                buildingFootprints = 0
                roadSegments = 0
                addressLinkedBuildings = 0
                addressLinkedRecords = 0
                jungnangFacilityCandidates = 0
            }
        }
    }

    foreach ($marker in $shops.markers) {
        Add-Count $tiles (Tile-Index ([double] $marker.x) $offsetX $size) (Tile-Index ([double] $marker.z) $offsetZ $size) 'shopPointCandidates'
    }

    $buildingTiles = @{}
    foreach ($building in $geography.buildings) {
        $points = @($building.points)
        Require ($points.Count -ge 4) "BuildingFootprintInvalid:$($building.id)"
        $xs = @($points | ForEach-Object { [double] $_.x })
        $zs = @($points | ForEach-Object { [double] $_.z })
        $centerX = (($xs | Measure-Object -Minimum).Minimum + ($xs | Measure-Object -Maximum).Maximum) / 2
        $centerZ = (($zs | Measure-Object -Minimum).Minimum + ($zs | Measure-Object -Maximum).Maximum) / 2
        $tileX = Tile-Index $centerX $offsetX $size
        $tileZ = Tile-Index $centerZ $offsetZ $size
        Add-Count $tiles $tileX $tileZ 'buildingFootprints'
        $buildingTiles[[string] $building.id] = @( $tileX, $tileZ )
    }

    foreach ($road in $geography.roads) {
        $centerX = (([double] $road.x1) + ([double] $road.x2)) / 2
        $centerZ = (([double] $road.z1) + ([double] $road.z2)) / 2
        Add-Count $tiles (Tile-Index $centerX $offsetX $size) (Tile-Index $centerZ $offsetZ $size) 'roadSegments'
    }

    foreach ($group in $links.groups) {
        $buildingId = [string] $group.buildingId
        Require ($buildingTiles.ContainsKey($buildingId)) "AddressBuildingMissing:$buildingId"
        $indices = $buildingTiles[$buildingId]
        Add-Count $tiles $indices[0] $indices[1] 'addressLinkedBuildings'
        Add-Count $tiles $indices[0] $indices[1] 'addressLinkedRecords' @($group.records).Count
    }

    foreach ($marker in $publicMarkers.markers) {
        Add-Count $tiles (Tile-Index ([double] $marker.x) $offsetX $size) (Tile-Index ([double] $marker.z) $offsetZ $size) 'jungnangFacilityCandidates'
    }

    $manifestHash = File-Hash $ManifestPath 'SourceManifest'
    $nodes = [Collections.Generic.List[object]]::new()
    $edges = [Collections.Generic.List[object]]::new()
    $nodes.Add([ordered]@{nodeStableId='region:kr:sig:11260';nodeKindCode='Region';title='서울특별시 중랑구';hStatus='ReferenceIndexOnly'})
    $nodes.Add([ordered]@{nodeStableId='area:reference:myeonmok';nodeKindCode='Area';title='면목동 자료 상세 구역';hStatus='PrivateReviewCandidate'})
    $nodes.Add([ordered]@{nodeStableId='reference:sagajeong-station';nodeKindCode='ObservationAnchor';title='사가정역 기준점';hStatus='SourceCoordinateReference'})
    $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:shop-points';nodeKindCode='ReferenceLayer';title='면목동 상가 위치 후보';hStatus='ReviewRequiredCoordinateDatumUnconfirmed'})
    $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:osm-buildings';nodeKindCode='ReferenceLayer';title='사가정 OSM 건물 외곽선';hStatus='SourceGeometryNotSurveyed'})
    $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:osm-roads';nodeKindCode='ReferenceLayer';title='사가정 OSM 도로 선분';hStatus='SourceGeometryNotNavigation'})
    $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:address-links';nodeKindCode='ReferenceLayer';title='주소 기반 건물 후보 연결';hStatus='OccupancyUnverified'})
    $nodes.Add([ordered]@{nodeStableId='layer:jungnang:public-facilities';nodeKindCode='ReferenceLayer';title='중랑구 공공시설 위치 색인';hStatus='PendingHumanReview'})
    $nodes.Add([ordered]@{nodeStableId='scenario:synthetic-neighborhood';nodeKindCode='ScenarioOverlay';title='기존 도형 동네';hStatus='ScenarioOnly';externalGraphRef='eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json'})
    if ($null -ne $diorama) {
        $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:address-spatial-index';nodeKindCode='ReferenceLayer';title='면목동 주소 중심 공간 색인';hStatus='PrivateReviewOnly'})
        $nodes.Add([ordered]@{nodeStableId='layer:myeonmok:diorama-presentation';nodeKindCode='ReferenceLayer';title='면목동 디오라마 표현 투영';hStatus='LocalEditorReviewOnly'})
    }
    $edges.Add([ordered]@{edgeStableId='relation:jungnang-contains-myeonmok';fromNodeRef='region:kr:sig:11260';toNodeRef='area:reference:myeonmok';relationCode='ContainsReferenceArea';wiRefs=@()})
    $edges.Add([ordered]@{edgeStableId='relation:myeonmok-observed-from-sagajeong';fromNodeRef='area:reference:myeonmok';toNodeRef='reference:sagajeong-station';relationCode='UsesCoordinateOrigin';wiRefs=@()})
    foreach ($layer in @('layer:myeonmok:shop-points','layer:myeonmok:osm-buildings','layer:myeonmok:osm-roads','layer:myeonmok:address-links')) {
        $suffix = $layer.Substring($layer.LastIndexOf(':') + 1)
        $edges.Add([ordered]@{edgeStableId="relation:myeonmok-has-$suffix";fromNodeRef='area:reference:myeonmok';toNodeRef=$layer;relationCode='HasReferenceLayer';wiRefs=@()})
    }
    $edges.Add([ordered]@{edgeStableId='relation:jungnang-has-public-facilities';fromNodeRef='region:kr:sig:11260';toNodeRef='layer:jungnang:public-facilities';relationCode='HasReferenceLayer';wiRefs=@()})
    $edges.Add([ordered]@{edgeStableId='relation:synthetic-overlay-of-myeonmok';fromNodeRef='scenario:synthetic-neighborhood';toNodeRef='area:reference:myeonmok';relationCode='ScenarioOverlayOf';wiRefs=@()})
    if ($null -ne $diorama) {
        $edges.Add([ordered]@{edgeStableId='relation:myeonmok-has-address-spatial-index';fromNodeRef='area:reference:myeonmok';toNodeRef='layer:myeonmok:address-spatial-index';relationCode='HasReferenceLayer';wiRefs=@()})
        $edges.Add([ordered]@{edgeStableId='relation:diorama-projection-derived-from-address-index';fromNodeRef='layer:myeonmok:diorama-presentation';toNodeRef='layer:myeonmok:address-spatial-index';relationCode='PrivacyProjectionOf';wiRefs=@()})
        $edges.Add([ordered]@{edgeStableId='relation:myeonmok-has-diorama-presentation';fromNodeRef='area:reference:myeonmok';toNodeRef='layer:myeonmok:diorama-presentation';relationCode='HasReferenceLayer';wiRefs=@()})
    }

    $instances = [Collections.Generic.List[object]]::new()
    $summaries = [Collections.Generic.List[object]]::new()
    foreach ($x in ([int] $frame.tileIndexMinX)..([int] $frame.tileIndexMaxX)) {
        foreach ($z in ([int] $frame.tileIndexMinZ)..([int] $frame.tileIndexMaxZ)) {
            $id = Tile-Key $x $z
            $nodes.Add([ordered]@{nodeStableId=$id;nodeKindCode='PlacementTile';title="면목동 500m 타일 ($x,$z)";hStatus='PrivateReviewCandidate'})
            $edges.Add([ordered]@{edgeStableId="relation:myeonmok-contains-x$x-z$z";fromNodeRef='area:reference:myeonmok';toNodeRef=$id;relationCode='ContainsPlacementTile';wiRefs=@()})
            $instances.Add([ordered]@{id=$id;nodeRef=$id;title="면목동 500m 타일 ($x,$z)";kind='PlacementTile';geometryKind='Tile';placementStatus='Candidate';tileIndexX=$x;tileIndexZ=$z;x=$offsetX+(($x+0.5)*$size);z=$offsetZ+(($z+0.5)*$size);width=$size;depth=$size})
            $count = $tiles[$id]
            $summaries.Add([ordered]@{tileRef=$id;shopPointCandidates=$count.shopPointCandidates;buildingFootprints=$count.buildingFootprints;roadSegments=$count.roadSegments;addressLinkedBuildings=$count.addressLinkedBuildings;addressLinkedRecords=$count.addressLinkedRecords;jungnangFacilityCandidates=$count.jungnangFacilityCandidates})
        }
    }

    $categoryCounts = @($shops.markers | Group-Object category | Sort-Object Name | ForEach-Object { [ordered]@{category=[string] $_.Name;count=$_.Count} })
    $reasonCounts = @($links.links | Group-Object result | Sort-Object Name | ForEach-Object { [ordered]@{reason=[string] $_.Name;count=$_.Count} })
    $kindCounts = @($jungnang.features | Group-Object { $_.observation.Kind } | Sort-Object Name | ForEach-Object { [ordered]@{kind=[string] $_.Name;count=$_.Count} })

    $mapVersion = [string] (Property $manifest 'mapVersion')
    if ([string]::IsNullOrWhiteSpace($mapVersion)) { $mapVersion = 'v1' }
    $mapRevision = [string] (Property $manifest 'mapRevision')
    if ([string]::IsNullOrWhiteSpace($mapRevision)) { $mapRevision = 'jungnang-myeonmok-reference.r1' }
    $graph = [ordered]@{
        schemaVersion='simulation-world-graph-map.v1'
        graphStableId="graph-map:jungnang-myeonmok-reference.$mapVersion"
        revision=$mapRevision
        planningRef=[string] $manifest.planningRef
        placementMapRef=$PlacementPath
        sourceManifestRef=$ManifestPath
        sourceManifestSha256=$manifestHash
        authorityBoundary='중랑구 색인과 면목동 공간 후보 관계만 표현한다. 실제 입주·영업·통행·업무 상태·Scene·E 승격을 만들지 않는다.'
        nodes=@($nodes)
        edges=@($edges)
    }

    $sourceLayers = @(
        [ordered]@{nodeRef='layer:myeonmok:shop-points';geometryKind='Point';placementStatus='ReviewRequired';count=5411;categoryCounts=$categoryCounts;sourceRef='source:myeonmok-shop-points:r1'},
        [ordered]@{nodeRef='layer:myeonmok:osm-buildings';geometryKind='Footprint';placementStatus='Candidate';count=602;sourceRef='source:sagajeong-osm-reference:r3'},
        [ordered]@{nodeRef='layer:myeonmok:osm-roads';geometryKind='LineSegment';placementStatus='Candidate';count=2397;sourceRef='source:sagajeong-osm-reference:r3'},
        [ordered]@{nodeRef='layer:myeonmok:address-links';geometryKind='BuildingReference';placementStatus='Candidate';count=801;buildingCount=204;unplacedCount=6742;reasonCounts=$reasonCounts;sourceRef='source:myeonmok-address-building-links:r1'},
        [ordered]@{nodeRef='layer:jungnang:public-facilities';geometryKind='PointOrUnplaced';placementStatus='ReviewRequired';count=146;placedInCurrentSquare=10;coordinateCount=110;missingCoordinateCount=36;kindCounts=$kindCounts;sourceRef='source:jungnang-location-review:r1'}
    )
    if ($null -ne $diorama) {
        $sourceLayers += [ordered]@{nodeRef='layer:myeonmok:diorama-presentation';geometryKind='BuildingOverlayReference';placementStatus='LocalEditorReviewOnly';count=204;activityObservationCount=801;landSampleCount=30;officialBuildingLinkCount=0;addressSingleCandidateCount=14;multipleCandidateCount=8;unlinkedSampleCount=8;sourceRef='source:myeonmok-diorama-presentation:r1'}
    }
    $checks = @('SourceRevisionAndHash','UniqueReferences','GraphPlacementCrossReference','TileGeometryAndCoverage','AggregateCountConservation','UnplacedReasonConservation','SyntheticOverlayBoundary')
    if ($null -ne $diorama) { $checks += 'DioramaPrivacyProjection' }
    $placement = [ordered]@{
        schemaVersion='simulation-world-placement-map-preparation-profile.v1'
        profileStableId="placement-map:jungnang-myeonmok-reference.$mapVersion"
        revision=$mapRevision
        title=if ($null -ne $diorama) {'중랑구 색인·면목동 주소 레이어·500m 타일 배치 참고'} else {'중랑구 색인·면목동 500m 타일 배치 참고'}
        stateCode='PreparedPrivateReview'
        planningRef=[string] $manifest.planningRef
        graphMapRef=$GraphPath
        sourceManifestRef=$ManifestPath
        sourceManifestSha256=$manifestHash
        coordinateSource=[ordered]@{method=[string] $frame.method;originStableId=[string] $frame.originStableId;originLatitude=[double] $frame.originLatitude;originLongitude=[double] $frame.originLongitude;worldOffsetX=$offsetX;worldOffsetZ=$offsetZ;metersPerUnit=1;tileSizeMeters=$size;tileKeyRule='floor((worldCoordinate-worldOffset)/500m)'}
        instances=@($instances)
        anchors=@(
            [ordered]@{id='anchor:sagajeong-station';ownerRef='area:reference:myeonmok';x=$offsetX;z=$offsetZ;role='ObservationOrigin';placementStatus='SourceCoordinateReference'},
            [ordered]@{id='anchor:synthetic-neighborhood-overlay';ownerRef='scenario:synthetic-neighborhood';x=0;z=0;role='ScenarioOverlayOrigin';placementStatus='ScenarioOnly'}
        )
        paths=@()
        actors=@()
        tileSummaries=@($summaries)
        sourceLayers=$sourceLayers
        unplacedCatalog=@(
            [ordered]@{sourceRef='source:myeonmok-address-building-links:r1';reason='NoExactBuildingInCurrentMap';count=6615},
            [ordered]@{sourceRef='source:myeonmok-address-building-links:r1';reason='MultipleBuildingsForAddress';count=82},
            [ordered]@{sourceRef='source:myeonmok-address-building-links:r1';reason='AddressMissing';count=45},
            [ordered]@{sourceRef='source:jungnang-sagajeong-markers:r1';reason='OutsideExistingSagajeongSquare';count=100},
            [ordered]@{sourceRef='source:jungnang-sagajeong-markers:r1';reason='SourceCoordinateUnavailable';count=36}
        )
        scenarioOverlays=@([ordered]@{graphRef='eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json';anchorRef='anchor:synthetic-neighborhood-overlay';relationCode='ScenarioOverlayOf';promotesToRealPlace=$false})
        knownGaps=@(
            [ordered]@{id='myeonmok-administrative-boundary';reason='면목동 전체 행정경계 도형이 없어 상가 관측 범위의 36개 타일을 상세 후보 범위로 사용한다.';blocksSceneReadiness=$true},
            [ordered]@{id='shop-coordinate-datum';reason='상가 경위도 좌표의 측지 기준이 명시적으로 확인되지 않아 WGS84 변환 결과를 검토 후보로만 사용한다.';blocksSceneReadiness=$true},
            [ordered]@{id='building-occupancy-and-entrance';reason='같은 주소의 단일 OSM 건물도 실제 입주·출입구·현재 영업을 증명하지 않는다.';blocksSceneReadiness=$true},
            [ordered]@{id='height-floor-unit';reason='건물 높이·층·동·호수는 확인된 자료가 없는 경우 추정하지 않는다.';blocksSceneReadiness=$true}
        )
        placementRules=@('500m 타일은 사가정역 ENU 기준점과 기존 World offset을 함께 보존','경계 객체는 중심점이 속한 대표 타일 한 곳에서만 집계','좌표 없음·주소 모호성·좌표계 미확정은 강제 보정하지 않음','개별 주소·사업체 행은 Git 산출물에 복제하지 않음','기존 도형 동네는 ScenarioOverlay이며 현실 공간으로 승격하지 않음')
        requiredDevelopmentChecks=$checks
        presentationMode='PlacementReviewOnly'
        presentationOnly=$true
        isOperationalState=$false
    }
    return [pscustomobject]@{ Graph=$graph; Placement=$placement }
}

function Sum([object[]] $items, [string] $name) {
    return [long] (($items | Measure-Object -Property $name -Sum).Sum)
}

function Validate-Maps([object] $manifest, [object] $graph, [object] $placement) {
    Require ($manifest.schemaVersion -eq 'simulation-world-regional-reference-sources.v1') 'ManifestSchema'
    Require ($graph.schemaVersion -eq 'simulation-world-graph-map.v1' -and $placement.schemaVersion -eq 'simulation-world-placement-map-preparation-profile.v1') 'MapSchema'
    Require ($graph.placementMapRef -eq $PlacementPath -and $placement.graphMapRef -eq $GraphPath) 'CrossReference'
    Require ($graph.planningRef -eq $manifest.planningRef -and $placement.planningRef -eq $manifest.planningRef -and (Test-Path -LiteralPath (Resolve-RepoPath $manifest.planningRef))) 'PlanningReference'
    $manifestHash = File-Hash $ManifestPath 'SourceManifest'
    Require ($graph.sourceManifestRef -eq $ManifestPath -and $placement.sourceManifestRef -eq $ManifestPath -and $graph.sourceManifestSha256 -eq $manifestHash -and $placement.sourceManifestSha256 -eq $manifestHash) 'SourceManifestMismatch'
    Require (-not $placement.isOperationalState -and $placement.presentationOnly -and $placement.presentationMode -eq 'PlacementReviewOnly') 'AuthorityBoundary'
    Require-Unique @($graph.nodes) { param($x) $x.nodeStableId } 'DuplicateNode'
    Require-Unique @($graph.edges) { param($x) $x.edgeStableId } 'DuplicateRelation'
    Require-Unique @($placement.instances) { param($x) $x.id } 'DuplicateInstance'
    Require-Unique @($placement.anchors) { param($x) $x.id } 'DuplicateAnchor'
    $nodes = @{}; foreach ($node in $graph.nodes) { $nodes[$node.nodeStableId] = $node }
    foreach ($edge in $graph.edges) { Require ($nodes.ContainsKey($edge.fromNodeRef) -and $nodes.ContainsKey($edge.toNodeRef)) "RelationNodeMissing:$($edge.edgeStableId)" }
    Require (@($graph.edges | Where-Object relationCode -eq 'ScenarioOverlayOf').Count -eq 1) 'SyntheticOverlayRelationMissing'
    $frame = $manifest.coordinateFrame
    $expectedTiles = @()
    foreach ($x in ([int] $frame.tileIndexMinX)..([int] $frame.tileIndexMaxX)) { foreach ($z in ([int] $frame.tileIndexMinZ)..([int] $frame.tileIndexMaxZ)) { $expectedTiles += Tile-Key $x $z } }
    $actualTiles = @($placement.instances | ForEach-Object id | Sort-Object)
    Require (@(Compare-Object ($expectedTiles | Sort-Object) $actualTiles).Count -eq 0) 'TileSetMismatch'
    Require (@($placement.tileSummaries).Count -eq $expectedTiles.Count) 'TileSummaryCount'
    $summaryRefs = @($placement.tileSummaries | ForEach-Object tileRef | Sort-Object)
    Require (@(Compare-Object ($expectedTiles | Sort-Object) $summaryRefs).Count -eq 0) 'TileSummarySetMismatch'
    foreach ($tile in $placement.instances) {
        Require ($nodes.ContainsKey($tile.nodeRef) -and $tile.nodeRef -eq $tile.id) "TileNodeMissing:$($tile.id)"
        Require ($tile.kind -eq 'PlacementTile' -and $tile.geometryKind -eq 'Tile' -and $tile.placementStatus -eq 'Candidate' -and [double] $tile.width -eq 500 -and [double] $tile.depth -eq 500) "TileGeometryInvalid:$($tile.id)"
        $expectedX = [double] $frame.worldOffsetX + (([int] $tile.tileIndexX + 0.5) * 500)
        $expectedZ = [double] $frame.worldOffsetZ + (([int] $tile.tileIndexZ + 0.5) * 500)
        Require ([double] $tile.x -eq $expectedX -and [double] $tile.z -eq $expectedZ -and $tile.id -eq (Tile-Key $tile.tileIndexX $tile.tileIndexZ)) "TileCoordinateInvalid:$($tile.id)"
    }
    foreach ($summary in $placement.tileSummaries) {
        foreach ($name in @('shopPointCandidates','buildingFootprints','roadSegments','addressLinkedBuildings','addressLinkedRecords','jungnangFacilityCandidates')) { $null = Require-Integer (Property $summary $name) "NegativeOrNonIntegerCount:$($summary.tileRef):$name" }
    }
    Require ((Sum $placement.tileSummaries 'shopPointCandidates') -eq 5411 -and (Sum $placement.tileSummaries 'buildingFootprints') -eq 602 -and (Sum $placement.tileSummaries 'roadSegments') -eq 2397 -and (Sum $placement.tileSummaries 'addressLinkedBuildings') -eq 204 -and (Sum $placement.tileSummaries 'addressLinkedRecords') -eq 801 -and (Sum $placement.tileSummaries 'jungnangFacilityCandidates') -eq 10) 'SummaryTotals'
    $unplaced = @($placement.unplacedCatalog)
    Require ((Sum @($unplaced | Where-Object sourceRef -eq 'source:myeonmok-address-building-links:r1') 'count') -eq 6742) 'AddressUnplacedTotals'
    Require ((Sum @($unplaced | Where-Object sourceRef -eq 'source:jungnang-sagajeong-markers:r1') 'count') -eq 136) 'JungnangUnplacedTotals'
    $dioramaSources = @($manifest.sourceSnapshots | Where-Object sourceStableId -eq 'source:myeonmok-diorama-presentation:r1')
    if ($dioramaSources.Count -eq 1) {
        $layer = @($placement.sourceLayers | Where-Object nodeRef -eq 'layer:myeonmok:diorama-presentation')
        Require ($layer.Count -eq 1 -and $layer[0].count -eq 204 -and $layer[0].activityObservationCount -eq 801 -and $layer[0].landSampleCount -eq 30 -and $layer[0].officialBuildingLinkCount -eq 0) 'DioramaLayerSummary'
        Require (@($graph.edges | Where-Object relationCode -eq 'PrivacyProjectionOf').Count -eq 1 -and @($placement.requiredDevelopmentChecks | Where-Object { $_ -eq 'DioramaPrivacyProjection' }).Count -eq 1) 'DioramaGraphContract'
    }
}

$manifest = Read-Json $ManifestPath 'SourceManifest'
if ($Mode -eq 'Write') {
    $built = Build-Maps $manifest
    [IO.File]::WriteAllText((Resolve-RepoPath $GraphPath), (Stable-Json $built.Graph), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Resolve-RepoPath $PlacementPath), (Stable-Json $built.Placement), [Text.UTF8Encoding]::new($false))
}

$graph = Read-Json $GraphPath 'GraphMap'
$placement = Read-Json $PlacementPath 'PlacementMap'
Validate-Maps $manifest $graph $placement
$freshness = 'Skipped'
if (-not $SkipSourceFreshness) {
    $built = Build-Maps $manifest
    Require ((Normalize-Text (Get-Content -LiteralPath (Resolve-RepoPath $GraphPath) -Raw -Encoding UTF8)) -ceq (Stable-Json $built.Graph)) 'GeneratedGraphStale'
    Require ((Normalize-Text (Get-Content -LiteralPath (Resolve-RepoPath $PlacementPath) -Raw -Encoding UTF8)) -ceq (Stable-Json $built.Placement)) 'GeneratedPlacementStale'
    $freshness = 'FrozenSourcesVerified'
}

[pscustomobject]@{
    Status = 'ReferenceMapsChecked'
    Revision = $graph.revision
    Nodes = @($graph.nodes).Count
    Relations = @($graph.edges).Count
    Tiles = @($placement.instances).Count
    ShopPointCandidates = Sum $placement.tileSummaries 'shopPointCandidates'
    BuildingFootprints = Sum $placement.tileSummaries 'buildingFootprints'
    RoadSegments = Sum $placement.tileSummaries 'roadSegments'
    AddressLinkedRecords = Sum $placement.tileSummaries 'addressLinkedRecords'
    AddressUnplacedRecords = Sum @($placement.unplacedCatalog | Where-Object sourceRef -eq 'source:myeonmok-address-building-links:r1') 'count'
    JungnangFacilityCandidates = Sum $placement.tileSummaries 'jungnangFacilityCandidates'
    DioramaOverlayBuildings = if (@($placement.sourceLayers | Where-Object nodeRef -eq 'layer:myeonmok:diorama-presentation').Count -eq 1) { 204 } else { 0 }
    SourceFreshness = $freshness
    PresentationMode = $placement.presentationMode
    SceneReady = $false
    UnityExecuted = $false
} | ConvertTo-Json -Depth 4
