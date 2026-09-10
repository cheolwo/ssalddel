using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

// 비공개 주소 검토 사본과 Unity 표현 투영을 같은 입력에서 결정적으로 만든다.
// 원본·DB·Graph/Placement 권위를 수정하지 않는 로컬 검토 전용 파생 자료다.
internal static class 면목동디오라마자료
{
    internal const string Revision = "myeonmok-diorama-view.r1";
    internal const string Relative = "artifacts/local/public-data/myeonmok-diorama-view-r1";
    internal const string ManifestRelative = "eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json";
    internal const string BusinessRelative = "artifacts/local/public-data/myeonmok-business-20260908-r1/connection.json";
    internal const string LandRelative = "artifacts/local/public-data/myeonmok-land-20260908-r1/review.json";
    internal const string UnityMapRelative = "Assets/Ssalddel/Resources/SagajeongReference.json";
    internal const string ManifestHash = "121D98150A4E6DF4546A5B824635FE700FA9F5F42766939B77CA0EF19555F5FE";
    internal const string BusinessHash = "D109A0AF26608E9627551600B37FE8B22F473893D5F6FEA542E83E5D1FD176E2";
    internal const string LandHash = "72A70F9614F932AEDD72E2B178E2217FAFBE7F21D6CDE3C4D2311E288DCBF543";
    internal const string MapHash = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "build" or "verify", "DioramaModeInvalid");
        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest();
            result["databaseWriteAttempted"] = false;
            result["unityExecuted"] = false;
            return;
        }

        var unityRoot = Environment.GetEnvironmentVariable("SSALDDEL_UNITY_ROOT");
        if (string.IsNullOrWhiteSpace(unityRoot))
            unityRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ssalddel");
        unityRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(unityRoot));

        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceManifest"] = Path.Combine(root, ManifestRelative),
            ["businessAddressCandidates"] = Path.Combine(root, BusinessRelative),
            ["landSampleReview"] = Path.Combine(root, LandRelative),
            ["sagajeongMap"] = Path.Combine(unityRoot, UnityMapRelative)
        };
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceManifest"] = ManifestHash,
            ["businessAddressCandidates"] = BusinessHash,
            ["landSampleReview"] = LandHash,
            ["sagajeongMap"] = MapHash
        };
        foreach (var item in paths)
        {
            SafeRead(item.Value, root, unityRoot);
            Require(HashFile(item.Value) == expected[item.Key], "DioramaInputHashChanged:" + item.Key);
        }

        var built = Build(paths, expected);
        Validate(built.PrivateIndex, built.Projection);
        var output = Path.Combine(root, Relative);
        var privateBytes = Serialize(built.PrivateIndex);
        var projectionBytes = Serialize(built.Projection);
        Require(privateBytes.Length <= 16 * 1024 * 1024, "DioramaPrivateIndexBudget");
        Require(projectionBytes.Length <= 4 * 1024 * 1024, "DioramaProjectionBudget");

        var manifest = new JsonObject
        {
            ["schema"] = "myeonmok-diorama-output-manifest.r1",
            ["revision"] = Revision,
            ["privateReviewOnly"] = true,
            ["distributionApproved"] = false,
            ["files"] = new JsonArray(
                FileRow("private-spatial-index.json", privateBytes),
                FileRow("presentation-projection.json", projectionBytes))
        };
        var manifestBytes = Serialize(manifest);

        if (mode == "build")
        {
            Require(!Directory.Exists(output), "DioramaOutputExists");
            Directory.CreateDirectory(output);
            try
            {
                await CreateNew(Path.Combine(output, "private-spatial-index.json"), privateBytes);
                await CreateNew(Path.Combine(output, "presentation-projection.json"), projectionBytes);
                await CreateNew(Path.Combine(output, "manifest.json"), manifestBytes);
            }
            catch
            {
                // 부분 파일은 증거로 보존한다. 삭제하거나 재시도하지 않는다.
                throw;
            }
        }
        else
        {
            Require(Directory.Exists(output), "DioramaOutputMissing");
            VerifyFile(Path.Combine(output, "private-spatial-index.json"), privateBytes);
            VerifyFile(Path.Combine(output, "presentation-projection.json"), projectionBytes);
            VerifyFile(Path.Combine(output, "manifest.json"), manifestBytes);
        }

        result["mode"] = mode;
        result["revision"] = Revision;
        result["privateIndexSha256"] = Hash(privateBytes);
        result["presentationProjectionSha256"] = Hash(projectionBytes);
        result["manifestSha256"] = Hash(manifestBytes);
        result["buildingOverlays"] = 204;
        result["activityObservations"] = 801;
        result["landSamples"] = 30;
        result["databaseWriteAttempted"] = false;
        result["unityExecuted"] = false;
        result["distributionApproved"] = false;
    }

    private static (JsonObject PrivateIndex, JsonObject Projection) Build(
        IReadOnlyDictionary<string, string> paths,
        IReadOnlyDictionary<string, string> expected)
    {
        var sourceManifest = ReadObject(paths["sourceManifest"]);
        var business = ReadObject(paths["businessAddressCandidates"]);
        var land = ReadObject(paths["landSampleReview"]);
        var map = ReadObject(paths["sagajeongMap"]);
        Require(S(business, "schema") == "myeonmok-building-reference.r1", "DioramaBusinessSchema");
        Require(I(business, "inputCount") == 7543 && I(business, "connectedCount") == 801 && I(business, "unlinkedCount") == 6742, "DioramaBusinessCounts");
        Require(S(land, "schema") == "myeonmok-land-review.r1" && A(land, "rows").Count == 30, "DioramaLandContract");
        Require(S(map, "revision") == "sagajeong-reference.r3" && A(map, "buildings").Count == 602 && A(map, "roads").Count == 2397, "DioramaMapContract");
        Require(S(sourceManifest, "revision") == "jungnang-myeonmok-sources.r1", "DioramaManifestContract");

        var buildings = A(map, "buildings").Select(NodeObject).ToDictionary(x => S(x, "id"), StringComparer.Ordinal);
        var groups = A(business, "groups").Select(NodeObject).OrderBy(x => S(x, "buildingId"), StringComparer.Ordinal).ToArray();
        Require(groups.Length == 204, "DioramaGroupCount");

        var privateAddresses = new JsonArray();
        var privateObservations = new JsonArray();
        var overlays = new JsonArray();
        foreach (var group in groups)
        {
            var buildingRef = S(group, "buildingId");
            Require(buildings.TryGetValue(buildingRef, out var building), "DioramaBuildingMissing:" + buildingRef);
            var address = S(group, "addressKey");
            var addressId = "address-private:" + ShortHash(address);
            var records = A(group, "records").Select(NodeObject).OrderBy(x => S(x, "id"), StringComparer.Ordinal).ToArray();
            Require(records.Length > 0, "DioramaEmptyGroup:" + buildingRef);

            privateAddresses.Add(new JsonObject
            {
                ["addressStableId"] = addressId,
                ["normalizedRoadAddress"] = address,
                ["buildingRef"] = buildingRef,
                ["connectionStatus"] = "주소단일후보",
                ["method"] = S(group, "relation"),
                ["observationIds"] = new JsonArray(records.Select(x => JsonValue.Create(S(x, "id"))).ToArray())
            });
            foreach (var record in records)
            {
                privateObservations.Add(new JsonObject
                {
                    ["observationId"] = S(record, "id"),
                    ["addressStableId"] = addressId,
                    ["buildingRef"] = buildingRef,
                    ["kind"] = S(record, "kind"),
                    ["name"] = S(record, "name"),
                    ["roadAddress"] = S(record, "roadAddress"),
                    ["sourceId"] = S(record, "sourceId"),
                    ["datasetId"] = S(record, "datasetId"),
                    ["sourceHash"] = S(record, "sourceHash"),
                    ["sourceRevision"] = S(record, "revision"),
                    ["evidenceAsOf"] = S(record, "evidenceAsOf"),
                    ["sourceStatus"] = S(record, "status"),
                    ["qualityCode"] = S(record, "quality")
                });
            }

            var categoryCounts = records.GroupBy(x => Category(S(x, "kind")), StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new JsonObject { ["categoryCode"] = x.Key, ["count"] = x.Count() })
                .ToArray();
            var operationCounts = records.GroupBy(OperationEvidence, StringComparer.Ordinal)
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new JsonObject { ["evidenceCode"] = x.Key, ["count"] = x.Count() })
                .ToArray();
            var heightKind = S(building!, "heightKind");
            overlays.Add(new JsonObject
            {
                ["spatialId"] = "spatial-building:" + ShortHash(buildingRef),
                ["buildingRef"] = buildingRef,
                ["buildingUseCode"] = BuildingUse(S(building!, "buildingKind")),
                ["buildingUseEvidenceCode"] = "OsmBuildingTagCandidate",
                ["displayHeight"] = D(building!, "height"),
                ["heightSemanticsCode"] = heightKind == "OsmHeightTag" ? "OsmHeightOrLevelsDerived" : "SymbolicDisplayOnly",
                ["activityCategories"] = new JsonArray(categoryCounts.Select(x => JsonValue.Create(S(x, "categoryCode"))).ToArray()),
                ["activityCounts"] = new JsonArray(categoryCounts),
                ["operationEvidenceCounts"] = new JsonArray(operationCounts),
                ["connectionStatus"] = "주소단일후보",
                ["connectionMethod"] = "UniqueAddressCandidateNotVerifiedOccupancy",
                ["observationCount"] = records.Length
            });
        }

        var sampleLinks = new JsonArray();
        foreach (var row in A(land, "rows").Select(NodeObject).OrderBy(x => S(NodeObject(x["Sample"]!), "Id"), StringComparer.Ordinal))
        {
            var sample = NodeObject(row["Sample"]!);
            sampleLinks.Add(new JsonObject
            {
                ["sampleId"] = S(sample, "Id"),
                ["group"] = S(sample, "Group"),
                ["name"] = S(sample, "Name"),
                ["roadAddress"] = S(sample, "RoadAddress"),
                ["lotAddress"] = S(sample, "LotAddress"),
                ["buildingManagementNumber"] = S(sample, "BuildingManagementNumber"),
                ["sourceId"] = S(sample, "SourceId"),
                ["sourceHash"] = S(sample, "SourceHash"),
                ["selectionReason"] = S(sample, "SelectionReason"),
                ["officialBuilding"] = Edge(NodeObject(row["OfficialBuilding"]!)),
                ["addressBuilding"] = Edge(NodeObject(row["AddressBuilding"]!)),
                ["gisBuilding"] = Edge(NodeObject(row["GisBuilding"]!)),
                ["buildingParcel"] = Edge(NodeObject(row["BuildingParcel"]!)),
                ["parcelRoad"] = Edge(NodeObject(row["ParcelRoad"]!)),
                ["spatialStatus"] = S(row, "SpatialStatus")
            });
        }

        var inputRows = new JsonArray(paths.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
        {
            ["inputCode"] = x.Key,
            ["path"] = Path.GetRelativePath(Path.GetDirectoryName(paths["sourceManifest"]) is { } manifestDir
                ? Path.GetFullPath(Path.Combine(manifestDir, "../../..")) : "", x.Value).Replace('\\', '/'),
            ["sha256"] = expected[x.Key],
            ["bytes"] = new FileInfo(x.Value).Length
        }).ToArray());

        var privateIndex = new JsonObject
        {
            ["schema"] = "myeonmok-address-spatial-index.r1",
            ["revision"] = Revision,
            ["privateReviewOnly"] = true,
            ["gameStateConnected"] = false,
            ["distributionApproved"] = false,
            ["authorityBoundary"] = "주소·건물·필지·도로·활동 관측의 후보 관계다. 입주·영업·출입·통행·게임 상태를 확정하지 않는다.",
            ["inputs"] = inputRows,
            ["coordinateFrame"] = sourceManifest["coordinateFrame"]!.DeepClone(),
            ["addresses"] = privateAddresses,
            ["activityObservations"] = privateObservations,
            ["landSampleLinks"] = sampleLinks
        };

        var sampleSummary = A(land, "rows").Select(NodeObject).GroupBy(x => S(NodeObject(x["GisBuilding"]!), "Status"), StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new JsonObject { ["status"] = x.Key, ["count"] = x.Count() }).ToArray();
        var projection = new JsonObject
        {
            ["schema"] = "myeonmok-diorama-presentation.r1",
            ["revision"] = Revision,
            ["privateReviewOnly"] = true,
            ["distributionApproved"] = false,
            ["gameStateConnected"] = false,
            ["mapRevision"] = S(map, "revision"),
            ["mapSha256"] = MapHash,
            ["sourceManifestSha256"] = ManifestHash,
            ["coordinateFrame"] = sourceManifest["coordinateFrame"]!.DeepClone(),
            ["layers"] = Layers(),
            ["buildingOverlays"] = overlays,
            ["unplacedSampleSummary"] = new JsonArray(sampleSummary),
            ["counts"] = new JsonObject
            {
                ["baseBuildings"] = 602,
                ["baseRoadSegments"] = 2397,
                ["overlayBuildings"] = 204,
                ["activityObservations"] = 801,
                ["landSamples"] = 30,
                ["officialBuildingLinks"] = 0,
                ["addressSingleCandidates"] = 14,
                ["multipleCandidates"] = 8,
                ["unlinkedSamples"] = 8
            },
            ["authorityBoundary"] = "상호명·상세주소·개인·세대 정보를 제외한 Editor 로컬 표현 투영이다. Reality Context, Scene, 실제 영업·입주 상태가 아니다."
        };
        return (privateIndex, projection);
    }

    private static JsonArray Layers() => new(
        Layer("RegionTile", "지역·타일 경계", false),
        Layer("Transport", "도로·역·정류장", true),
        Layer("PhysicalStructure", "필지·건물 외곽", true),
        Layer("ObservedBuildingUse", "관측 건물 종류", true),
        Layer("ObservedActivity", "음식·소매·의료·교육·수리·제조 관측", true),
        Layer("GameScenario", "H·WI·게임 시나리오", false),
        Layer("EvidenceReview", "연결 상태·근거 검토", true));

    private static JsonObject Layer(string code, string title, bool visible) => new()
    {
        ["layerCode"] = code,
        ["title"] = title,
        ["defaultVisible"] = visible
    };

    private static JsonObject Edge(JsonObject edge) => new()
    {
        ["status"] = S(edge, "Status"),
        ["method"] = S(edge, "Method"),
        ["candidateIds"] = edge["CandidateIds"]?.DeepClone() ?? new JsonArray(),
        ["reason"] = S(edge, "Reason")
    };

    private static string Category(string kind) => kind switch
    {
        "restaurant" => "음식",
        "shop" => "소매",
        "clinic" or "hospital" => "의료",
        "housing" => "주거",
        "factory" => "제조",
        "traditional-market-source-observation" => "전통시장",
        _ => "기타관측"
    };

    private static string BuildingUse(string kind) => kind switch
    {
        "house" or "residential" or "apartments" or "detached" => "주거후보",
        "commercial" or "retail" => "상업후보",
        "industrial" or "warehouse" => "산업후보",
        "civic" or "public" => "공공후보",
        _ => "미확인"
    };

    private static string OperationEvidence(JsonObject record)
    {
        var status = S(record, "status");
        if (status == "영업/정상") return "LicenseOperatingObservationOnly";
        if (status is "폐업" or "제외/삭제/전출") return "LicenseInactiveObservationOnly";
        return "CurrentOperationUnknown";
    }

    private static void Validate(JsonObject privateIndex, JsonObject projection)
    {
        Require(S(privateIndex, "schema") == "myeonmok-address-spatial-index.r1" && B(privateIndex, "privateReviewOnly") && !B(privateIndex, "gameStateConnected") && !B(privateIndex, "distributionApproved"), "DioramaPrivateBoundary");
        Require(A(privateIndex, "addresses").Count == 204 && A(privateIndex, "activityObservations").Count == 801 && A(privateIndex, "landSampleLinks").Count == 30, "DioramaPrivateCounts");
        Require(S(projection, "schema") == "myeonmok-diorama-presentation.r1" && B(projection, "privateReviewOnly") && !B(projection, "distributionApproved") && !B(projection, "gameStateConnected"), "DioramaProjectionBoundary");
        Require(A(projection, "layers").Count == 7 && A(projection, "buildingOverlays").Count == 204, "DioramaProjectionCounts");
        Require(A(projection, "buildingOverlays").Select(NodeObject).Select(x => S(x, "buildingRef")).Distinct(StringComparer.Ordinal).Count() == 204, "DioramaProjectionDuplicateBuilding");
        Require(A(projection, "buildingOverlays").Select(NodeObject).Sum(x => I(x, "observationCount")) == 801, "DioramaProjectionObservationCount");
        var forbiddenProperties = new HashSet<string>(new[] { "name", "address", "roadAddress", "addressKey", "normalizedRoadAddress", "buildingManagementNumber", "observationId" }, StringComparer.OrdinalIgnoreCase);
        Walk(projection, (name, _) => Require(!forbiddenProperties.Contains(name), "DioramaPrivateFieldLeak:" + name));
        var projectionText = projection.ToJsonString();
        foreach (var sensitive in A(privateIndex, "activityObservations").Select(NodeObject)
                     .SelectMany(x => new[] { S(x, "name"), S(x, "roadAddress") })
                     .Where(x => x.Length >= 4).Distinct(StringComparer.Ordinal))
            Require(!projectionText.Contains(sensitive, StringComparison.Ordinal), "DioramaPrivateValueLeak");
        Require(I(NodeObject(projection["counts"]!), "officialBuildingLinks") == 0 && I(NodeObject(projection["counts"]!), "addressSingleCandidates") == 14 && I(NodeObject(projection["counts"]!), "multipleCandidates") == 8 && I(NodeObject(projection["counts"]!), "unlinkedSamples") == 8, "DioramaSampleSummary");
    }

    private static void Walk(JsonNode? node, Action<string, JsonNode?> property)
    {
        if (node is JsonObject obj)
            foreach (var item in obj) { property(item.Key, item.Value); Walk(item.Value, property); }
        else if (node is JsonArray array)
            foreach (var item in array) Walk(item, property);
    }

    private static int SelfTest()
    {
        var count = 0;
        void Check(bool condition) { Require(condition, "DioramaSelfTest:" + (count + 1)); count++; }
        Check(Category("restaurant") == "음식");
        Check(Category("factory") == "제조");
        Check(Category("unknown") == "기타관측");
        Check(BuildingUse("apartments") == "주거후보");
        Check(BuildingUse("yes") == "미확인");
        Check(OperationEvidence(new JsonObject { ["status"] = "영업/정상" }) == "LicenseOperatingObservationOnly");
        Check(OperationEvidence(new JsonObject { ["status"] = "폐업" }) == "LicenseInactiveObservationOnly");
        Check(OperationEvidence(new JsonObject { ["status"] = "" }) == "CurrentOperationUnknown");
        Check(Layers().Count == 7);
        Check(ShortHash("same") == ShortHash("same"));
        Check(ShortHash("same") != ShortHash("different"));
        var sample = new JsonObject { ["safe"] = new JsonArray(new JsonObject { ["categoryCode"] = "음식" }) };
        var names = new List<string>(); Walk(sample, (name, _) => names.Add(name));
        Check(names.SequenceEqual(new[] { "safe", "categoryCode" }));
        return count;
    }

    private static JsonObject FileRow(string name, byte[] bytes) => new()
    {
        ["name"] = name,
        ["bytes"] = bytes.Length,
        ["sha256"] = Hash(bytes)
    };

    private static byte[] Serialize(JsonNode node)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(node, Json);
        return bytes[^1] == (byte)'\n' ? bytes : bytes.Concat(new[] { (byte)'\n' }).ToArray();
    }

    private static async Task CreateNew(string path, byte[] bytes)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes);
    }

    private static void VerifyFile(string path, byte[] expected)
    {
        SafeRead(path, Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetDirectoryName(Path.GetFullPath(path))!);
        var actual = File.ReadAllBytes(path);
        Require(actual.AsSpan().SequenceEqual(expected), "DioramaOutputMismatch:" + Path.GetFileName(path));
    }

    private static void SafeRead(string path, string repositoryRoot, string unityRoot)
    {
        var full = Path.GetFullPath(path);
        var repo = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var unity = Path.TrimEndingDirectorySeparator(Path.GetFullPath(unityRoot));
        Require(full.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || full.StartsWith(unity + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "DioramaPathOutsideRoots");
        for (var item = new FileInfo(full).Directory; item != null; item = item.Parent)
            if (item.Exists) Require((item.Attributes & FileAttributes.ReparsePoint) == 0, "DioramaReparseRejected");
        var file = new FileInfo(full);
        Require(file.Exists && file.Length > 0 && file.Length <= 32 * 1024 * 1024 && (file.Attributes & FileAttributes.ReparsePoint) == 0, "DioramaInputMissingOrInvalid");
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject ?? throw new InvalidDataException("DioramaJsonObjectRequired");
    private static JsonObject NodeObject(JsonNode? node) => node as JsonObject ?? throw new InvalidDataException("DioramaObjectRequired");
    private static JsonArray A(JsonObject value, string name) => value[name] as JsonArray ?? throw new InvalidDataException("DioramaArrayRequired:" + name);
    private static string S(JsonObject value, string name) => value[name]?.GetValue<string>() ?? "";
    private static int I(JsonObject value, string name) => value[name]?.GetValue<int>() ?? 0;
    private static double D(JsonObject value, string name) => value[name]?.GetValue<double>() ?? 0;
    private static bool B(JsonObject value, string name) => value[name]?.GetValue<bool>() ?? false;
    private static string ShortHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
}
