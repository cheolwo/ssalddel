using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Presentation;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3, "정규화 지도와 경로 후보의 좌표·계보·실패·결정성을 검증한다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3결정성검증,
    Boundary = "합성 도형의 단위 시험이며 실제 면목동·SHP 변환·Unity·NPC 이동·E 승격 증거가 아니다.")]
public sealed class 동네공간Tests
{
    [Fact]
    public void 좌표축단위와출처를보존하고높이와폭을추정하지않는다()
    {
        var snapshot = 읽기(표본());
        Assert.True(snapshot.Source.IsSynthetic);
        Assert.False(snapshot.RuntimeAuthorized);
        Assert.Equal("LOCAL:SYNTHETIC", snapshot.Source.ProjectedCrs);
        Assert.Equal("SyntheticTestOnly", snapshot.Source.LicenseReviewStatus);
        Assert.Equal(new string('A', 64), snapshot.Source.RawContentHashSha256);
        var depot = Assert.Single(snapshot.Nodes, n => n.StableId == "depot");
        var restaurant = Assert.Single(snapshot.Nodes, n => n.StableId == "restaurant-stop");
        Assert.Equal((0.0, 0.0), (depot.Position.X, depot.Position.Z));
        Assert.Equal((100.0, 100.0), (restaurant.Position.X, restaurant.Position.Z));
        Assert.Equal((1000.0, 2000.0), (snapshot.ProjectedOrigin.X, snapshot.ProjectedOrigin.Z));
        Assert.Equal(500, snapshot.ProjectedBounds[2] - snapshot.ProjectedBounds[0]);
        Assert.Equal(3, snapshot.Buildings.Count);
        Assert.All(snapshot.Buildings, b => Assert.Null(b.HeightMeters));
        Assert.All(snapshot.Roads, r => Assert.Null(r.WidthMeters));
    }

    [Fact]
    public void 차량과도보후보를분리하고표현용굴절점을보존한다()
    {
        var snapshot = 읽기(표본());
        var drive = 경로(snapshot);
        Assert.True(drive.Found);
        Assert.False(drive.RuntimeAuthorized);
        Assert.Equal(200, drive.LengthMeters);
        Assert.Equal(new[] { "road-depot-junction", "road-junction-restaurant" }, drive.EdgeIds);
        Assert.Equal(new[] { (0.0, 0.0), (50.0, 0.0), (100.0, 0.0), (100.0, 100.0) },
            drive.Points.Select(p => (p.X, p.Z)));
        var walk = 경로(snapshot, "restaurant-stop", "restaurant-door", 동네이동수단.Pedestrian);
        Assert.Equal(5, walk.LengthMeters);
        Assert.Equal(drive.Points.Last(), walk.Points.First());
        Assert.False(경로(snapshot, "depot", "restaurant-stop", 동네이동수단.Pedestrian).Found);
        Assert.Equal("NeighborhoodVehicleEntranceForbidden", 경로(snapshot, "restaurant-stop", "restaurant-door").ReasonCode);
    }

    [Fact]
    public void 기존음식배달표현계약이같은판본의차량도보후보를소비할수있다()
    {
        var snapshot = 읽기(표본());
        var drive = 경로(snapshot);
        var walk = 경로(snapshot, "restaurant-stop", "restaurant-door", 동네이동수단.Pedestrian);
        var vehicle = new 음식배달관찰경로("synthetic-vehicle", drive.Revision,
            drive.Points.Select(p => new 배달평면위치(p.X, p.Z)).ToArray());
        var entrance = new 음식배달관찰경로("synthetic-entrance", walk.Revision,
            walk.Points.Select(p => new 배달평면위치(p.X, p.Z)).ToArray());
        var observation = new 음식배달관찰동선(vehicle, entrance);
        Assert.Equal(200, observation.VehicleRoute.Length);
        Assert.Equal((100.0, 100.0), (observation.정차위치.X, observation.정차위치.Z));
        var approach = observation.인계접근위치(1);
        Assert.Equal((105.0, 100.0), (approach.X, approach.Z));
        Assert.Equal("DeliveryRouteBlocked", Assert.Throws<InvalidOperationException>(() => vehicle.위치(0.5, blocked: true)).Message);
        Assert.False(drive.RuntimeAuthorized); // DTO 호환 시험이며 실제 Presenter/Scene 연결이 아니다.
    }

    [Fact]
    public void 굽은도로길이는양끝직선거리가아니다()
    {
        var json = 표본();
        도형(json, "road-depot-junction")["geometry"]!["coordinates"] = JsonNode.Parse("[[1000,2000],[1050,1950],[1100,2000]]");
        var result = 경로(읽기(json));
        Assert.Equal(Math.Sqrt(5000) * 2 + 100, result.LengthMeters, 8);
        Assert.Equal((50.0, -50.0), (result.Points[1].X, result.Points[1].Z));
    }

    [Theory]
    [InlineData("delivery-one-stop", "delivery-one-door")]
    [InlineData("delivery-two-stop", "delivery-two-door")]
    public void 두목적지각각에차량접근과도보왕복경로후보가존재한다(string stop, string door)
    {
        var snapshot = 읽기(표본());
        Assert.True(경로(snapshot, "restaurant-stop", stop).Found);
        Assert.True(경로(snapshot, stop, door, 동네이동수단.Pedestrian).Found);
        Assert.True(경로(snapshot, door, stop, 동네이동수단.Pedestrian).Found);
        Assert.True(경로(snapshot, stop, "depot").Found);
        // 경로 후보의 존재는 조리·배정·픽업·수령·NPC 복귀를 실행했다는 뜻이 아니다.
    }

    [Fact]
    public void 가까운점과도로위에있는점도노드연결없이접속시키지않는다()
    {
        var result = 경로(읽기(표본()), "depot", "near-but-disconnected");
        Assert.False(result.Found);
        Assert.Equal("NeighborhoodNoTraversableRoute", result.ReasonCode);
        Assert.Empty(result.Points);
        Assert.Empty(result.EdgeIds);
    }

    [Fact]
    public void 선이교차해도별도노드이면교차로를만들지않는다()
    {
        var json = 표본();
        var features = json["features"]!.AsArray();
        features.Add(JsonNode.Parse("""{"id":"cross-south","kind":"Node","geometry":{"type":"Point","coordinates":[1050,1950]},"properties":{"role":"Junction"}}"""));
        features.Add(JsonNode.Parse("""{"id":"cross-north","kind":"Node","geometry":{"type":"Point","coordinates":[1050,2050]},"properties":{"role":"Junction"}}"""));
        features.Add(JsonNode.Parse("""{"id":"cross-road","kind":"Road","geometry":{"type":"LineString","coordinates":[[1050,1950],[1050,2050]]},"properties":{"fromNodeId":"cross-south","toNodeId":"cross-north","direction":"Both","modes":["Vehicle"],"accessReview":"Reviewed","reviewEvidenceRef":"synthetic-crossing"}}"""));
        var snapshot = 읽기(json);
        Assert.True(경로(snapshot, "cross-south", "cross-north").Found);
        Assert.False(경로(snapshot, "depot", "cross-north").Found);
    }

    [Theory]
    [InlineData("accessReview", "Unknown")]
    [InlineData("accessReview", "Blocked")]
    [InlineData("direction", "Unknown")]
    public void 미검토나차단이나불명확방향을지름길로대체하지않는다(string property, string value)
    {
        var json = 표본();
        도형(json, "road-junction-restaurant")["properties"]![property] = value;
        Assert.False(경로(읽기(json)).Found);
    }

    [Fact]
    public void 일방통행역방향은거부하고양방향의역순궤적은보존한다()
    {
        var json = 표본();
        var reverse = 경로(읽기(json), "restaurant-stop", "depot");
        Assert.True(reverse.Found);
        Assert.Equal(new[] { (100.0, 100.0), (100.0, 0.0), (50.0, 0.0), (0.0, 0.0) },
            reverse.Points.Select(p => (p.X, p.Z)));
        도형(json, "road-junction-restaurant")["properties"]!["direction"] = "Forward";
        var snapshot = 읽기(json);
        Assert.True(경로(snapshot).Found);
        Assert.False(경로(snapshot, "restaurant-stop", "depot").Found);
    }

    [Fact]
    public void 더짧은검토경로를고르고동률은입력순서에무관하다()
    {
        var json = 표본();
        var shortcut = 도형(json, "unreviewed-shortcut");
        shortcut["properties"]!["accessReview"] = "Reviewed";
        shortcut["properties"]!["reviewEvidenceRef"] = "synthetic-test-only";
        var route = 경로(읽기(json));
        Assert.Equal(Math.Sqrt(20000), route.LengthMeters, 8);
        Assert.Equal("unreviewed-shortcut", Assert.Single(route.EdgeIds));
        var equal = shortcut.DeepClone();
        equal["id"] = "aaa-equal-road";
        json["features"]!.AsArray().Add(equal);
        var forward = 경로(읽기(json));
        json["features"] = new JsonArray(json["features"]!.AsArray().Reverse().Select(n => n!.DeepClone()).ToArray());
        var backward = 경로(읽기(json));
        Assert.Equal("aaa-equal-road", Assert.Single(forward.EdgeIds));
        Assert.Equal(forward.EdgeIds, backward.EdgeIds);
        Assert.Equal(forward.Points, backward.Points);
        Assert.NotEqual(forward.Revision, backward.Revision); // 정확 파일 바이트가 바뀌면 판본도 바뀐다.
    }

    [Fact]
    public void 현재노드와누락노드를구별한다()
    {
        var snapshot = 읽기(표본());
        var already = 경로(snapshot, "depot", "depot");
        Assert.True(already.Found);
        Assert.Equal("NeighborhoodAlreadyAtNode", already.ReasonCode);
        Assert.Equal(0, already.LengthMeters);
        Assert.Empty(already.EdgeIds);
        Assert.Single(already.Points);
        Assert.Equal("NeighborhoodNodeNotFound", 경로(snapshot, "missing", "depot").ReasonCode);
    }

    [Fact]
    public void 동일입력을재조회해도같고해시와판본변조는거부한다()
    {
        var bytes = 바이트(표본());
        var importer = new 동네공간ImportService();
        var hash = 해시(bytes);
        var first = importer.읽기(bytes, hash);
        var second = importer.읽기(bytes, hash.ToLowerInvariant());
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(경로(first).Points, 경로(second).Points);
        Assert.Equal("NeighborhoodRevisionMismatch", Assert.Throws<InvalidDataException>(() =>
            new 동네이동경로Engine().탐색(first, "stale", "depot", "restaurant-stop", 동네이동수단.Vehicle)).Message);
        bytes[0] = (byte)' ';
        Assert.Equal("NeighborhoodContentHashMismatch", Assert.Throws<InvalidDataException>(() => importer.읽기(bytes, hash)).Message);
        Assert.Equal(200, 경로(first).LengthMeters);
    }

    [Fact]
    public void 입력과출력컬렉션변경이기존사본에영향을주지않는다()
    {
        var json = 표본();
        var snapshot = 읽기(json);
        json["features"]!.AsArray().Clear();
        Assert.Equal(9, snapshot.Nodes.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<동네공간노드>)snapshot.Nodes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<동네평면좌표>)snapshot.Roads[0].Points).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)경로(snapshot).EdgeIds).Clear());
    }

    [Theory]
    [InlineData("unit", "degrees", "NeighborhoodCoordinateConventionUnsupported")]
    [InlineData("axisOrder", "NorthingEasting", "NeighborhoodCoordinateConventionUnsupported")]
    public void 축이나단위를추측해수정하지않는다(string field, string value, string error)
    {
        var json = 표본();
        json["coordinates"]![field] = value;
        실패(json, error);
    }

    [Theory]
    [InlineData("sourceCrs", "EPSG:4326", "NeighborhoodProjectedCrsUnsupported")]
    [InlineData("rawContentHashSha256", "not-a-hash", "NeighborhoodRawHashInvalid")]
    [InlineData("observedAtUtc", "2026-09-06T09:00:00+09:00", "NeighborhoodSourceTimeInvalid")]
    [InlineData("observedAtUtc", "2026-09-06T00:00:00", "NeighborhoodSourceTimeInvalid")]
    [InlineData("licenseReviewStatus", "ReviewedForPrototype", "NeighborhoodLicenseReviewInvalid")]
    public void 잘못된출처를거부한다(string field, string value, string error)
    {
        var json = 표본();
        json["provenance"]![field] = value;
        실패(json, error);
    }

    [Fact]
    public void 실제자료라고신고해도공간사용을자동승인하지않는다()
    {
        var json = 표본();
        var source = json["provenance"]!;
        source["dataKind"] = "PublicData";
        source["sourceId"] = "test-official-source-not-downloaded";
        source["projectedCrs"] = "EPSG:5186";
        source["sourceCrs"] = "EPSG:5179";
        source["licenseReviewStatus"] = "PendingReview";
        var snapshot = 읽기(json);
        Assert.False(snapshot.Source.IsSynthetic);
        Assert.False(snapshot.RuntimeAuthorized);
        source["licenseReviewStatus"] = "ReviewedForPrototype";
        Assert.False(읽기(json).RuntimeAuthorized);
        source["projectedCrs"] = "EPSG:4326";
        실패(json, "NeighborhoodProjectedCrsUnsupported");
    }

    [Fact]
    public void 경계밖점과원점을자동자르거나이동하지않는다()
    {
        var json = 표본();
        도형(json, "depot")["geometry"]!["coordinates"] = JsonNode.Parse("[899,2000]");
        실패(json, "NeighborhoodPointOutsideBounds");
        json = 표본();
        json["coordinates"]!["origin"] = JsonNode.Parse("[899,2000]");
        실패(json, "NeighborhoodOriginOutsideBounds");
        json = 표본();
        json["coordinates"]!["bounds"] = JsonNode.Parse("[900,1900,900,2400]");
        실패(json, "NeighborhoodBoundsInvalid");
    }

    [Theory]
    [InlineData("[[1000,2000],[1000,2000],[1100,2000]]", "NeighborhoodZeroLengthSegment")]
    [InlineData("[[1000,2000],[1100.01,2000]]", "NeighborhoodRoadEndpointMismatch")]
    [InlineData("[[1000,2000,0],[1100,2000,0]]", "NeighborhoodCoordinateDimensionInvalid")]
    public void 도로결손을자동보정하지않는다(string coordinates, string error)
    {
        var json = 표본();
        도형(json, "road-depot-junction")["geometry"]!["coordinates"] = JsonNode.Parse(coordinates);
        실패(json, error);
    }

    [Theory]
    [InlineData("fromNodeId", "missing", "NeighborhoodRoadNodeInvalid")]
    [InlineData("fromNodeId", "junction", "NeighborhoodRoadNodeInvalid")]
    [InlineData("direction", "Reverse", "NeighborhoodEnumInvalid")]
    [InlineData("direction", "1", "NeighborhoodEnumInvalid")]
    public void 잘못된도로계약을거부한다(string field, string value, string error)
    {
        var json = 표본();
        도형(json, "road-depot-junction")["properties"]![field] = value;
        실패(json, error);
    }

    [Fact]
    public void 출입구건물과차량접근과검토근거를검증한다()
    {
        var json = 표본();
        도형(json, "restaurant-door")["properties"]!["buildingId"] = "missing";
        실패(json, "NeighborhoodEntranceBuildingInvalid");
        json = 표본();
        도형(json, "walk-restaurant")["properties"]!["modes"] = JsonNode.Parse("[\"Vehicle\"]");
        실패(json, "NeighborhoodVehicleEntranceForbidden");
        json = 표본();
        도형(json, "road-depot-junction")["properties"]!.AsObject().Remove("reviewEvidenceRef");
        실패(json, "NeighborhoodAccessEvidenceMissing");
    }

    [Theory]
    [InlineData("[[[1105,2090],[1125,2090],[1125,2110],[1105,2110]]]", "NeighborhoodPolygonNotClosed")]
    [InlineData("[[[1105,2090],[1125,2110],[1125,2090],[1105,2110],[1105,2090]]]", "NeighborhoodPolygonSelfIntersection")]
    [InlineData("[[[1105,2090],[1125,2090],[1110,2090],[1105,2090]]]", "NeighborhoodPolygonSelfIntersection")]
    [InlineData("[[[1105,2090],[1125,2090],[1125,2110],[1105,2090]],[[1106,2091],[1107,2091],[1107,2092],[1106,2091]]]", "NeighborhoodPolygonHolesUnsupported")]
    public void 잘못된윤곽과미지원내곽을조용히단순화하지않는다(string coordinates, string error)
    {
        var json = 표본();
        도형(json, "restaurant-building")["geometry"]!["coordinates"] = JsonNode.Parse(coordinates);
        실패(json, error);
    }

    [Fact]
    public void 중복ID와중복JSON속성과과대입력을거부한다()
    {
        var json = 표본();
        도형(json, "junction")["id"] = "depot";
        실패(json, "NeighborhoodDuplicateFeatureId");
        var duplicate = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"a\",\"schemaVersion\":\"b\"}");
        var importer = new 동네공간ImportService();
        Assert.Equal("NeighborhoodDuplicateJsonProperty", Assert.Throws<InvalidDataException>(() =>
            importer.읽기(duplicate, 해시(duplicate))).Message);
        Assert.Equal("NeighborhoodInputSizeInvalid", Assert.Throws<InvalidDataException>(() =>
            importer.읽기(new byte[동네공간ImportService.MaximumBytes + 1], new string('A', 64))).Message);
    }

    [Fact]
    public void 누락필드와손상JSON은분류된오류로반환한다()
    {
        var json = 표본();
        json.AsObject().Remove("features");
        실패(json, "NeighborhoodFieldMissing");
        var bytes = Encoding.UTF8.GetBytes("{malformed");
        Assert.Equal("NeighborhoodJsonInvalid", Assert.Throws<InvalidDataException>(() =>
            new 동네공간ImportService().읽기(bytes, 해시(bytes))).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void 부적절한높이는미확인으로숨기지않고거부한다(double height)
    {
        var json = 표본();
        도형(json, "restaurant-building")["properties"]!["heightMeters"] = height;
        실패(json, "NeighborhoodDimensionInvalid");
    }

    private static JsonNode 표본()
    {
        using var stream = typeof(동네공간Tests).Assembly.GetManifestResourceStream("Ssalddel.Simulation.Tests.synthetic-block.v1.json")!;
        return JsonNode.Parse(stream)!;
    }
    private static JsonNode 도형(JsonNode json, string id) => json["features"]!.AsArray().Single(n => n!["id"]!.GetValue<string>() == id)!;
    private static byte[] 바이트(JsonNode json) => Encoding.UTF8.GetBytes(json.ToJsonString());
    private static string 해시(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static 동네공간Snapshot 읽기(JsonNode json)
    {
        var bytes = 바이트(json);
        return new 동네공간ImportService().읽기(bytes, 해시(bytes));
    }
    private static 동네이동경로후보 경로(동네공간Snapshot snapshot, string from = "depot",
        string to = "restaurant-stop", 동네이동수단 mode = 동네이동수단.Vehicle)
        => new 동네이동경로Engine().탐색(snapshot, snapshot.Revision, from, to, mode);
    private static void 실패(JsonNode json, string expected)
        => Assert.Equal(expected, Assert.Throws<InvalidDataException>(() => 읽기(json)).Message);
}
