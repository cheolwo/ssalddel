using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using Ssalddel.Services.WorldProjection.SpatialCatalog;

// 기존 면목동 자료를 움직이지 않고 공통 객체 원형과 법정동별 자료 패키지를 분리한다.
// 이 빌더의 결과는 비공개 검토용 Mongo 조회 자료이며 Unity·Simulation·업무 상태의 권위가 아니다.
internal static class 동별공간PackageCatalog
{
    internal const string RegistryRelative = "eng/world-seedbeds/neighborhood-packages/registry.v1.json";
    internal const string MyeonmokArea = "region:kr:bjd:1126010100";
    internal const string JunghwaArea = "region:kr:bjd:1126010300";
    internal const string CommonCatalogSourcePath = "generated:five-element-work-object-catalog.r1.json";
    internal const string MyeonmokProjectionSourcePath = "generated:myeonmok-game-object-candidates.r3.json";
    internal const string JunghwaInventorySourcePath = "generated:junghwa-observation-inventory.r1.json";
    internal const string MyeonmokPackageSourcePath = "generated:neighborhood-package-1126010100.r1.json";
    internal const string JunghwaPackageSourcePath = "generated:neighborhood-package-1126010300.r1.json";

    private const string CommonCatalogId = "game-object-catalog:ssalddel-five-element-work.v1";
    private const string CommonCatalogRevision = "ssalddel-five-element-work-objects.r1";
    private const string MyeonmokPackageId = "neighborhood-spatial-package:region:kr:bjd:1126010100.v1";
    private const string JunghwaPackageId = "neighborhood-spatial-package:region:kr:bjd:1126010300.v1";
    private const string JunghwaInventoryId = "neighborhood-observation-inventory:region:kr:bjd:1126010300.v1";
    private const string SeoulShopEntry = "소상공인시장진흥공단_상가(상권)정보_서울_202606.csv";

    internal sealed record 원본입력Fingerprint(string SourcePath, string AreaStableId, long Bytes,
        string Sha256, long MtimeUtcTicks);

    internal sealed record 결과(IReadOnlyList<공간자료Source> Sources,
        IReadOnlyList<원본입력Fingerprint> RawInputFingerprints, int JunghwaShopRows,
        int JunghwaFacilityRows, int JunghwaCoordinateRows);

    internal static 결과 Build(string root, IReadOnlyList<공간자료Source> physicalSources,
        공간자료Source legacyProjectionSource)
    {
        var registrySource = physicalSources.Single(x => x.Kind == "NeighborhoodRegistry");
        var registry = 공간자료Json.Parse(registrySource.Bytes);
        ValidateRegistry(registry);
        var rawInputFingerprints = VerifyRawInputs(root, registry);

        var legacyCatalogSource = physicalSources.Single(x => x.Kind == "ObjectCatalog");
        var commonCatalog = BuildCommonCatalog(legacyCatalogSource);
        var commonSource = new 공간자료Source(CommonCatalogSourcePath, "shared-five-element-work",
            "ObjectCatalog", Bytes(commonCatalog));

        var myeonmokProjection = BuildMyeonmokProjection(legacyProjectionSource, commonSource);
        var myeonmokProjectionSource = new 공간자료Source(MyeonmokProjectionSourcePath,
            "neighborhood:1126010100", "ObjectCandidateProjection", Bytes(myeonmokProjection), MyeonmokArea);

        var junghwaPackageRegistration = Package(registry, JunghwaPackageId);
        var inventory = BuildJunghwaInventory(root, junghwaPackageRegistration);
        var inventorySource = new 공간자료Source(JunghwaInventorySourcePath,
            "neighborhood:1126010300", "ObservationInventory", Bytes(inventory.Document), JunghwaArea);

        var myeonmokPackage = BuildMyeonmokPackage(registry, commonSource, myeonmokProjectionSource,
            physicalSources);
        var junghwaPackage = BuildJunghwaPackage(registry, commonSource, inventorySource, inventory);

        return new 결과(
        [
            commonSource,
            myeonmokProjectionSource,
            inventorySource,
            new 공간자료Source(MyeonmokPackageSourcePath, "neighborhood:1126010100",
                "NeighborhoodPackage", Bytes(myeonmokPackage), MyeonmokArea),
            new 공간자료Source(JunghwaPackageSourcePath, "neighborhood:1126010300",
                "NeighborhoodPackage", Bytes(junghwaPackage), JunghwaArea)
        ], rawInputFingerprints, inventory.ShopRows, inventory.FacilityRows, inventory.CoordinateRows);
    }

    internal static void VerifyRawInputs(string root)
    {
        var path = Resolve(root, RegistryRelative);
        ValidateRegistry(공간자료Json.Parse(File.ReadAllBytes(path)));
        _ = VerifyRawInputs(root, 공간자료Json.Parse(File.ReadAllBytes(path)));
    }

    private static BsonDocument BuildCommonCatalog(공간자료Source legacySource)
    {
        var legacy = 공간자료Json.Parse(legacySource.Bytes);
        Require(Text(legacy, "schemaVersion") == "myeonmok-game-object-catalog.v1", "CommonCatalogLegacySchemaChanged");
        var common = (BsonDocument)legacy.DeepClone();
        foreach (var field in new[]
        {
            "graphMapRef", "graphMapRevision", "graphMapPath", "graphMapSha256",
            "placementMapRef", "placementMapRevision", "placementMapPath", "placementMapSha256",
            "sourceManifestRef", "sourceManifestRevision", "sourceManifestPath", "sourceManifestSha256"
        }) common.Remove(field);

        common["schemaVersion"] = "five-element-work-object-catalog.v1";
        common["catalogStableId"] = CommonCatalogId;
        common["revision"] = CommonCatalogRevision;
        common["stateCode"] = "ApprovedFoundation";
        common["planningRef"] = "docs/AI/Planning/시스템/PLAN-SYSTEM-NEIGHBORHOOD-SPATIAL-PACKAGES/README.md";
        common["sourceCatalogRef"] = Text(legacy, "catalogStableId");
        common["sourceCatalogRevision"] = Text(legacy, "revision");
        common["sourceCatalogSha256"] = 공간자료Json.Hash(legacySource.Bytes);
        common["scope"] = new BsonDocument
        {
            ["areaScoped"] = false,
            ["role"] = "동마다 복제하지 않는 공통 업무 객체 원형·WI 행위·역할 결속과 선택 분류 메타데이터",
            ["geographicUse"] = "각 법정동 패키지가 참조하며 현실 객체 인스턴스를 자동 생성하지 않는다."
        };
        common["authorityBoundary"] = AuthorityBoundary();

        var profiles = Array(common, "candidateGenerationProfiles");
        Replace(profiles, "profileStableId", "candidate-profile:myeonmok-neutral-building.r1",
            "candidate-profile:neighborhood-neutral-building.r1");
        Replace(profiles, "profileStableId", "candidate-profile:myeonmok-neutral-road-network.r1",
            "candidate-profile:neighborhood-neutral-road-network.r1");
        ReplaceAll(profiles, "objectArchetypeRef", "game-object-archetype:myeonmok-neutral-building.v1",
            "game-object-archetype:neighborhood-neutral-building.v1");
        ReplaceAll(profiles, "objectArchetypeRef", "game-object-archetype:myeonmok-neutral-road-network.v1",
            "game-object-archetype:neighborhood-neutral-road-network.v1");
        foreach (var profile in profiles)
            profile["rule"] = Text(profile, "sourceElementKind") == "Building"
                ? "지역 패키지의 검증된 건물 원본을 참조하는 중립 배경 후보를 만들며 개수는 원천 manifest가 소유한다."
                : "지역 패키지의 검증된 도로 원본 ID를 보존하며 통행 가능성과 업무 장소를 만들지 않는다.";

        var archetypes = Array(common, "archetypes");
        Replace(archetypes, "objectArchetypeStableId", "game-object-archetype:myeonmok-neutral-building.v1",
            "game-object-archetype:neighborhood-neutral-building.v1", "displayName", "지역 중립 건물");
        Replace(archetypes, "objectArchetypeStableId", "game-object-archetype:myeonmok-neutral-road-network.v1",
            "game-object-archetype:neighborhood-neutral-road-network.v1", "displayName", "지역 중립 도로망");
        common["compatibilityAliases"] = new BsonArray
        {
            new BsonDocument
            {
                ["aliasStableId"] = "compatibility-alias:myeonmok-neutral-building.r1",
                ["legacyRef"] = "game-object-archetype:myeonmok-neutral-building.v1",
                ["canonicalRef"] = "game-object-archetype:neighborhood-neutral-building.v1",
                ["reason"] = "기존 면목동 r2 묶음의 안정 ID를 삭제하거나 다시 쓰지 않는다."
            },
            new BsonDocument
            {
                ["aliasStableId"] = "compatibility-alias:myeonmok-neutral-road-network.r1",
                ["legacyRef"] = "game-object-archetype:myeonmok-neutral-road-network.v1",
                ["canonicalRef"] = "game-object-archetype:neighborhood-neutral-road-network.v1",
                ["reason"] = "기존 면목동 r2 묶음의 안정 ID를 삭제하거나 다시 쓰지 않는다."
            }
        };
        common["knownGaps"] = new BsonArray
        {
            Gap("worksite-selection", "PendingHumanReview", "현실 건물과 음식점·주택·창고 역할은 지역별 사람 검토 전 결속하지 않는다."),
            Gap("entrance-traversal", "BlockedByEvidence", "출입구·보행 가능 구역·도로 통행 가능성은 근거 없이 생성하지 않는다."),
            Gap("move-return-state", "PendingStateMapping", "이동·복귀 WI의 상태 대응은 기존 업무 상태 계약이 정하기 전 보류한다."),
            Gap("unity-binding", "UnityExecutionDeferred", "Prefab·Scene·Game View 결속은 별도 Unity 검증이 필요하다.")
        };
        ValidateCommonCatalog(common);
        return common;
    }

    private static BsonDocument BuildMyeonmokProjection(공간자료Source legacyProjectionSource,
        공간자료Source commonCatalogSource)
    {
        var projection = 공간자료Json.Parse(legacyProjectionSource.Bytes);
        Require(Text(projection, "schemaVersion") == "myeonmok-game-object-candidate-projection.v1",
            "MyeonmokLegacyProjectionSchemaChanged");
        projection["schemaVersion"] = "neighborhood-game-object-candidate-projection.v1";
        projection["projectionStableId"] = "game-object-candidate-projection:region:kr:bjd:1126010100.v1";
        projection["revision"] = "jungnang-myeonmok-game-object-candidates.r3";
        projection["areaStableId"] = MyeonmokArea;
        projection["packageRef"] = MyeonmokPackageId;
        projection["objectCatalogRef"] = CommonCatalogId;
        projection["objectCatalogRevision"] = CommonCatalogRevision;
        projection["objectCatalogSha256"] = 공간자료Json.Hash(commonCatalogSource.Bytes);
        projection["legacyProjectionSha256"] = 공간자료Json.Hash(legacyProjectionSource.Bytes);
        projection["compatibilityStableIdsPreserved"] = true;
        foreach (var candidate in Array(projection, "candidates"))
        {
            var reference = Text(candidate, "objectArchetypeRef");
            if (reference == "game-object-archetype:myeonmok-neutral-building.v1")
                candidate["objectArchetypeRef"] = "game-object-archetype:neighborhood-neutral-building.v1";
            else if (reference == "game-object-archetype:myeonmok-neutral-road-network.v1")
                candidate["objectArchetypeRef"] = "game-object-archetype:neighborhood-neutral-road-network.v1";
        }
        return projection;
    }

    private static BsonDocument BuildMyeonmokPackage(BsonDocument registry, 공간자료Source commonSource,
        공간자료Source projectionSource, IReadOnlyList<공간자료Source> physicalSources)
    {
        var registration = Package(registry, MyeonmokPackageId);
        var legacy = 공간자료Json.Parse(physicalSources.Single(x => x.Kind == "ObjectCatalog").Bytes);
        var projection = 공간자료Json.Parse(projectionSource.Bytes);
        return new BsonDocument
        {
            ["schemaVersion"] = "neighborhood-spatial-package.v1",
            ["packageStableId"] = MyeonmokPackageId,
            ["revision"] = Text(registration, "packageRevision"),
            ["areaStableId"] = MyeonmokArea,
            ["legalDongCode"] = Text(registration, "legalDongCode"),
            ["legalDongName"] = Text(registration, "legalDongName"),
            ["dataset"] = Text(registration, "dataset"),
            ["coordinateFrame"] = CoordinateFrame(registry, registration),
            ["commonObjectCatalogRef"] = CommonCatalogId,
            ["commonObjectCatalogRevision"] = CommonCatalogRevision,
            ["commonObjectCatalogSha256"] = 공간자료Json.Hash(commonSource.Bytes),
            ["graphMapRef"] = legacy["graphMapRef"],
            ["graphMapRevision"] = legacy["graphMapRevision"],
            ["graphMapSha256"] = legacy["graphMapSha256"],
            ["placementMapRef"] = legacy["placementMapRef"],
            ["placementMapRevision"] = legacy["placementMapRevision"],
            ["placementMapSha256"] = legacy["placementMapSha256"],
            ["sourceManifestRef"] = legacy["sourceManifestRef"],
            ["sourceManifestRevision"] = legacy["sourceManifestRevision"],
            ["sourceManifestSha256"] = legacy["sourceManifestSha256"],
            ["candidateProjectionRef"] = projection["projectionStableId"],
            ["candidateProjectionRevision"] = projection["revision"],
            ["candidateProjectionSha256"] = 공간자료Json.Hash(projectionSource.Bytes),
            ["readinessCode"] = Text(registration, "readinessCode"),
            ["readinessIsApproval"] = false,
            ["boundaryState"] = Text(registration, "boundaryState"),
            ["placementEligibility"] = "ReviewOnly",
            ["sourceCoverage"] = new BsonDocument
            {
                ["buildingCandidates"] = projection["candidateCounts"]["buildingBackdrop"],
                ["roadSegments"] = projection["sourceElementCounts"]["roads"]
            },
            ["administrativeDongRefs"] = new BsonArray(),
            ["privacyState"] = "PrivateReviewOnly;NoDetailedAddressInPackage",
            ["sceneReady"] = false,
            ["gameStateConnected"] = false,
            ["isExecutionAuthority"] = false
        };
    }

    private sealed record Inventory(BsonDocument Document, int ShopRows, int FacilityRows, int CoordinateRows);

    private static Inventory BuildJunghwaInventory(string root, BsonDocument registration)
    {
        var shopInput = SourceInput(registration, "source-input:semas-shops-national-202606");
        var facilityInput = SourceInput(registration, "source-input:jungnang-location-review.r1");
        var observations = new List<BsonDocument>();
        var shopRows = 0;
        var shopCoordinateRows = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using (var archive = new ZipArchive(File.OpenRead(Resolve(root, Text(shopInput, "repoRelativePath"))),
                   ZipArchiveMode.Read, false, Encoding.GetEncoding(949)))
        {
            var entry = archive.Entries.Single(x => x.FullName == SeoulShopEntry);
            Require(entry.Length == 301889640, "JunghwaSeoulShopEntryChanged");
            using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false, true));
            var seoulRows = 0;
            foreach (var row in 면목동사업체수집.Csv(reader))
            {
                Require(++seoulRows <= 700000, "JunghwaSeoulShopRowBudget");
                if (Required(row, "시군구코드") != "11260" || Required(row, "법정동코드") != "1126010300") continue;
                Require(Required(row, "시도코드") == "11" && Required(row, "시도명") == "서울특별시"
                    && Required(row, "시군구명") == "중랑구" && Required(row, "법정동명") == "중화동",
                    "JunghwaShopRegionConflict");
                var providerId = Required(row, "상가업소번호");
                Require(providerId.Length > 0, "JunghwaShopIdentityMissing");
                var longitude = Coordinate(Required(row, "경도"), 124, 132);
                var latitude = Coordinate(Required(row, "위도"), 33, 39);
                if (longitude.HasValue && latitude.HasValue) shopCoordinateRows++;
                else Require(!longitude.HasValue && !latitude.HasValue, "JunghwaShopPartialCoordinate");
                var identity = 공간자료Json.Hash(providerId);
                var rawHash = 공간자료Json.Hash(JsonSerializer.Serialize(row.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)));
                observations.Add(new BsonDocument
                {
                    ["observationStableId"] = "observation:junghwa:shop:" + identity,
                    ["sourceElementRef"] = "source:semas-shop:" + identity,
                    ["sourceDatasetId"] = "data-go-kr-15083033",
                    ["sourceRecordSha256"] = rawHash,
                    ["observationKindCode"] = "ShopListing",
                    ["activityCategoryCode"] = Required(row, "상권업종대분류코드"),
                    ["activitySubcategoryCode"] = Required(row, "상권업종소분류코드"),
                    ["longitude"] = longitude.HasValue ? (BsonValue)longitude.Value : BsonNull.Value,
                    ["latitude"] = latitude.HasValue ? (BsonValue)latitude.Value : BsonNull.Value,
                    ["coordinateStatus"] = longitude.HasValue ? "SourceCoordinateDatumUnconfirmed" : "SourceCoordinateUnavailable",
                    ["placementEligibility"] = longitude.HasValue ? "CoordinateCandidateOnly" : "AggregateOnly",
                    ["evidenceAsOfUtc"] = "2026-06-30T00:00:00+00:00",
                    ["reviewState"] = "PendingHumanReview",
                    ["privateDetailIncluded"] = false,
                    ["isExecutionAuthority"] = false
                });
                shopRows++;
            }
        }

        var facilityRows = 0;
        var facilityCoordinateRows = 0;
        using (var source = JsonDocument.Parse(File.ReadAllBytes(Resolve(root, Text(facilityInput, "repoRelativePath")))))
        {
            Require(source.RootElement.GetProperty("schema").GetString() == "jungnang-location-review.r1",
                "JunghwaFacilitySchemaChanged");
            foreach (var feature in source.RootElement.GetProperty("features").EnumerateArray())
            {
                var item = feature.GetProperty("observation");
                var lotAddress = item.GetProperty("LotAddress").GetString() ?? "";
                if (!lotAddress.StartsWith("서울특별시 중랑구 중화동 ", StringComparison.Ordinal)) continue;
                var sourceId = feature.GetProperty("StableId").GetString()!;
                var hasLongitude = item.TryGetProperty("Longitude", out var longitudeElement)
                    && longitudeElement.ValueKind == JsonValueKind.Number;
                var hasLatitude = item.TryGetProperty("Latitude", out var latitudeElement)
                    && latitudeElement.ValueKind == JsonValueKind.Number;
                Require(hasLongitude == hasLatitude, "JunghwaFacilityPartialCoordinate");
                if (hasLongitude) facilityCoordinateRows++;
                observations.Add(new BsonDocument
                {
                    ["observationStableId"] = "observation:junghwa:facility:" + 공간자료Json.Hash(sourceId),
                    ["sourceElementRef"] = sourceId,
                    ["sourceDatasetId"] = feature.GetProperty("DatasetId").GetString()!,
                    ["sourceRecordSha256"] = item.GetProperty("RawRecordSha256").GetString()!,
                    ["observationKindCode"] = "PublicFacility",
                    ["facilityKindCode"] = item.GetProperty("Kind").GetString()!,
                    ["longitude"] = hasLongitude ? (BsonValue)longitudeElement.GetDouble() : BsonNull.Value,
                    ["latitude"] = hasLatitude ? (BsonValue)latitudeElement.GetDouble() : BsonNull.Value,
                    ["coordinateStatus"] = hasLongitude ? "SourcePointUnverified" : "SourceCoordinateUnavailable",
                    ["placementEligibility"] = hasLongitude ? "CoordinateCandidateOnly" : "AggregateOnly",
                    ["evidenceAsOfUtc"] = feature.GetProperty("EvidenceAsOfUtc").GetString()!,
                    ["reviewState"] = "PendingHumanReview",
                    ["privateDetailIncluded"] = false,
                    ["isExecutionAuthority"] = false
                });
                facilityRows++;
            }
        }

        Require(shopRows == RequiredInt(registration, "expectedShopRows"), "JunghwaShopCountChanged");
        Require(facilityRows == RequiredInt(registration, "expectedFacilityRows"), "JunghwaFacilityCountChanged");
        Require(facilityCoordinateRows == RequiredInt(registration, "expectedFacilityCoordinateRows"),
            "JunghwaFacilityCoordinateCountChanged");
        Require(observations.Select(x => Text(x, "observationStableId")).Distinct(StringComparer.Ordinal).Count()
            == observations.Count, "JunghwaObservationIdentityCollision");
        observations.Sort((a, b) => StringComparer.Ordinal.Compare(Text(a, "observationStableId"), Text(b, "observationStableId")));

        var document = new BsonDocument
        {
            ["schemaVersion"] = "neighborhood-observation-inventory.v1",
            ["inventoryStableId"] = JunghwaInventoryId,
            ["revision"] = "jungnang-junghwa-observations.r1",
            ["areaStableId"] = JunghwaArea,
            ["legalDongCode"] = "1126010300",
            ["legalDongName"] = "중화동",
            ["sourceSnapshots"] = new BsonArray
            {
                SourceSnapshot(shopInput, shopRows, shopCoordinateRows, "ShopListingAt20260630;CurrentOperationUnverified"),
                SourceSnapshot(facilityInput, facilityRows, facilityCoordinateRows, "PublicFacilityPointOrNull;BoundaryUnverified")
            },
            ["counts"] = new BsonDocument
            {
                ["shopObservations"] = shopRows,
                ["shopCoordinateObservations"] = shopCoordinateRows,
                ["facilityObservations"] = facilityRows,
                ["facilityCoordinateObservations"] = facilityCoordinateRows,
                ["total"] = observations.Count
            },
            ["readinessCode"] = "InventoryReady",
            ["boundaryState"] = "NotAcquired",
            ["graphMapState"] = "NotCreated",
            ["placementMapState"] = "NotCreated",
            ["publicationApproved"] = false,
            ["privateDetailIncluded"] = false,
            ["sceneReady"] = false,
            ["gameStateConnected"] = false,
            ["isExecutionAuthority"] = false,
            ["observations"] = new BsonArray(observations)
        };
        return new(document, shopRows, facilityRows, shopCoordinateRows + facilityCoordinateRows);
    }

    private static BsonDocument BuildJunghwaPackage(BsonDocument registry, 공간자료Source commonSource,
        공간자료Source inventorySource, Inventory inventory)
    {
        var registration = Package(registry, JunghwaPackageId);
        return new BsonDocument
        {
            ["schemaVersion"] = "neighborhood-spatial-package.v1",
            ["packageStableId"] = JunghwaPackageId,
            ["revision"] = Text(registration, "packageRevision"),
            ["areaStableId"] = JunghwaArea,
            ["legalDongCode"] = Text(registration, "legalDongCode"),
            ["legalDongName"] = Text(registration, "legalDongName"),
            ["dataset"] = Text(registration, "dataset"),
            ["coordinateFrame"] = CoordinateFrame(registry, registration),
            ["commonObjectCatalogRef"] = CommonCatalogId,
            ["commonObjectCatalogRevision"] = CommonCatalogRevision,
            ["commonObjectCatalogSha256"] = 공간자료Json.Hash(commonSource.Bytes),
            ["sourceInventoryRef"] = JunghwaInventoryId,
            ["sourceInventoryRevision"] = "jungnang-junghwa-observations.r1",
            ["sourceInventorySha256"] = 공간자료Json.Hash(inventorySource.Bytes),
            ["readinessCode"] = "InventoryReady",
            ["readinessIsApproval"] = false,
            ["boundaryState"] = "NotAcquired",
            ["graphMapState"] = "NotCreated",
            ["placementMapState"] = "NotCreated",
            ["tileLoadingState"] = "DeferredUntilPlacementMap",
            ["sourceCoverage"] = new BsonDocument
            {
                ["shopObservations"] = inventory.ShopRows,
                ["facilityObservations"] = inventory.FacilityRows,
                ["coordinateObservations"] = inventory.CoordinateRows
            },
            ["administrativeDongRefs"] = new BsonArray(),
            ["privacyState"] = "NamesAndDetailedAddressesExcluded",
            ["sceneReady"] = false,
            ["gameStateConnected"] = false,
            ["isExecutionAuthority"] = false
        };
    }

    private static void ValidateRegistry(BsonDocument registry)
    {
        Require(Text(registry, "schemaVersion") == "neighborhood-package-registry.v1"
            && Text(registry, "registryStableId") == "neighborhood-package-registry:seoul-east.v1",
            "NeighborhoodRegistryIdentityMismatch");
        var packages = Array(registry, "packages");
        Require(packages.Count == 2 && packages.Select(x => Text(x, "packageStableId")).Distinct().Count() == 2,
            "NeighborhoodRegistryPackageSetChanged");
        var myeonmok = Package(registry, MyeonmokPackageId);
        var junghwa = Package(registry, JunghwaPackageId);
        Require(Text(myeonmok, "areaStableId") == MyeonmokArea && Text(myeonmok, "legalDongCode") == "1126010100",
            "MyeonmokPackageRegionMismatch");
        Require(Text(junghwa, "areaStableId") == JunghwaArea && Text(junghwa, "legalDongCode") == "1126010300"
            && Text(junghwa, "readinessCode") == "InventoryReady", "JunghwaPackageRegionMismatch");
        Require(Array(registry, "coordinateFrames").Count == 1, "NeighborhoodCoordinateFrameSetChanged");
    }

    private static IReadOnlyList<원본입력Fingerprint> VerifyRawInputs(string root, BsonDocument registry)
    {
        var fingerprints = new List<원본입력Fingerprint>();
        foreach (var package in Array(registry, "packages"))
        foreach (var input in Array(package, "sourceInputs"))
        {
            var path = Resolve(root, Text(input, "repoRelativePath"));
            using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(stream));
            Require(actual.Equals(Text(input, "expectedSha256"), StringComparison.OrdinalIgnoreCase),
                "NeighborhoodSourceHashMismatch:" + Text(input, "sourceStableId"));
            fingerprints.Add(new 원본입력Fingerprint(
                Text(input, "repoRelativePath"), Text(package, "areaStableId"), stream.Length,
                actual, File.GetLastWriteTimeUtc(path).Ticks));
        }
        return fingerprints.DistinctBy(x => x.SourcePath, StringComparer.Ordinal)
            .OrderBy(x => x.SourcePath, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateCommonCatalog(BsonDocument catalog)
    {
        Require(Text(catalog, "catalogStableId") == CommonCatalogId && Text(catalog, "revision") == CommonCatalogRevision,
            "CommonCatalogIdentityMismatch");
        Require(Array(catalog, "workflowBindings").Count == 5 && Array(catalog, "archetypes").Count == 26
            && Array(catalog, "interactionProfiles").Count == 11 && Array(catalog, "roleActionBindings").Count == 25,
            "CommonCatalogCountMismatch");
        Require(Array(catalog, "archetypes").All(x => !x.GetValue("isExecutionAuthority", true).ToBoolean()),
            "CommonCatalogAuthorityLeak");
        Require(Array(catalog, "archetypes").Any(x => Text(x, "objectArchetypeStableId")
            == "game-object-archetype:neighborhood-neutral-building.v1"), "CommonBuildingArchetypeMissing");
        Require(Array(catalog, "archetypes").Any(x => Text(x, "objectArchetypeStableId")
            == "game-object-archetype:neighborhood-neutral-road-network.v1"), "CommonRoadArchetypeMissing");
    }

    private static BsonDocument Package(BsonDocument registry, string id) => Array(registry, "packages")
        .Single(x => Text(x, "packageStableId") == id);

    private static BsonDocument SourceInput(BsonDocument package, string id) => Array(package, "sourceInputs")
        .Single(x => Text(x, "sourceStableId") == id);

    private static BsonDocument CoordinateFrame(BsonDocument registry, BsonDocument registration)
    {
        var reference = Text(registration, "coordinateFrameRef");
        var frame = (BsonDocument)Array(registry, "coordinateFrames")
            .Single(x => Text(x, "coordinateFrameStableId") == reference).DeepClone();
        frame["tileScopeCode"] = Text(registration, "tileScopeCode");
        return frame;
    }

    private static BsonDocument SourceSnapshot(BsonDocument input, int records, int coordinates, string quality) => new()
    {
        ["sourceStableId"] = Text(input, "sourceStableId"),
        ["repoRelativePath"] = Text(input, "repoRelativePath"),
        ["expectedSha256"] = Text(input, "expectedSha256"),
        ["recordCount"] = records,
        ["coordinateRecordCount"] = coordinates,
        ["qualityCode"] = quality,
        ["publicationApproved"] = false
    };

    private static BsonDocument AuthorityBoundary() => new()
    {
        ["isExecutionAuthority"] = false,
        ["isPlacementAuthority"] = false,
        ["isGameplayAuthority"] = false,
        ["sceneReady"] = false,
        ["gameStateConnected"] = false,
        ["privateDataIncluded"] = false,
        ["rule"] = "공통 객체 대장은 분류·조회·인계 자료이며 주문·배차·재고·이동·Scene·Prefab을 변경하지 않는다."
    };

    private static BsonDocument Gap(string code, string state, string description) => new()
    {
        ["id"] = "gap:five-element-work-object-catalog:" + code,
        ["stateCode"] = state,
        ["description"] = description
    };

    private static void Replace(IReadOnlyList<BsonDocument> items, string key, string before, string after,
        string? extraKey = null, string? extraValue = null)
    {
        var item = items.Single(x => Text(x, key) == before);
        item[key] = after;
        if (extraKey is not null) item[extraKey] = extraValue!;
    }

    private static void ReplaceAll(IEnumerable<BsonDocument> items, string key, string before, string after)
    {
        foreach (var item in items.Where(x => Text(x, key) == before)) item[key] = after;
    }

    private static double? Coordinate(string value, double min, double max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        Require(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && double.IsFinite(result) && result >= min && result <= max, "JunghwaCoordinateInvalid");
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> row, string key)
    {
        Require(row.TryGetValue(key, out var value), "JunghwaColumnMissing:" + key);
        return value!;
    }

    private static string Resolve(string root, string relative)
    {
        Require(relative.Length > 0 && !Path.IsPathRooted(relative) && !relative.Contains("..", StringComparison.Ordinal),
            "NeighborhoodSourcePathInvalid");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "NeighborhoodSourceOutsideRepository");
        면목동사업체수집.Safe(full);
        Require(File.Exists(full), "NeighborhoodSourceMissing:" + relative);
        return full;
    }

    private static List<BsonDocument> Array(BsonDocument value, string key)
    {
        Require(value.TryGetValue(key, out var array) && array.IsBsonArray, "NeighborhoodArrayMissing:" + key);
        return array.AsBsonArray.Select(x => x.AsBsonDocument).ToList();
    }

    private static int RequiredInt(BsonDocument value, string key)
    {
        Require(value.TryGetValue(key, out var number) && number.IsNumeric, "NeighborhoodNumberMissing:" + key);
        return number.ToInt32();
    }

    private static string Text(BsonDocument value, params string[] keys) => 공간자료Json.Text(value, keys);
    private static byte[] Bytes(BsonDocument value) => Encoding.UTF8.GetBytes(공간자료Json.Element(value, true).GetRawText());
    private static void Require(bool value, string code)
    {
        if (!value) throw new InvalidDataException(code);
    }
}
