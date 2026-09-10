using System.Text.Json;
using Ssalddel.Simulation.Application;

// 읽기 전용 도구: 외부 통신·파일 생성·DB/세션/Unity 변경을 하지 않는다.
if (args.Length != 5 || !Enum.GetNames<동네이동수단>().Contains(args[4]) || !Enum.TryParse<동네이동수단>(args[4], out var mode)
    || !Enum.IsDefined(mode))
{
    Console.Error.WriteLine("사용법: <정규화 JSON 경로> <검토한 파일 SHA-256> <시작 노드 ID> <도착 노드 ID> <Vehicle|Pedestrian>");
    return 64;
}

try
{
    // 크기를 먼저 검사하고 상한+1 바이트만 읽어 동시 파일 증가도 제한한다.
    using var stream = File.OpenRead(args[0]);
    if (stream.Length > 동네공간ImportService.MaximumBytes)
        throw new InvalidDataException("NeighborhoodInputSizeInvalid");
    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    while (buffer.Length <= 동네공간ImportService.MaximumBytes)
    {
        var count = stream.Read(chunk, 0, Math.Min(chunk.Length,
            동네공간ImportService.MaximumBytes + 1 - (int)buffer.Length));
        if (count == 0) break;
        buffer.Write(chunk, 0, count);
    }
    var snapshot = new 동네공간ImportService().읽기(buffer.ToArray(), args[1]);
    var result = new 동네이동경로Engine().탐색(snapshot, snapshot.Revision, args[2], args[3], mode);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = "neighborhood-preflight-report.v1",
        evidence = "CandidateOnly",
        dataKind = snapshot.Source.IsSynthetic ? "SyntheticFixture" : "PublicDataCandidate",
        snapshotId = snapshot.StableId,
        normalizedContentHashSha256 = snapshot.Revision,
        sourceId = snapshot.Source.SourceId,
        datasetId = snapshot.Source.DatasetId,
        sourceVersion = snapshot.Source.SourceVersion,
        licenseReviewStatus = snapshot.Source.LicenseReviewStatus,
        runtimeAuthorized = snapshot.RuntimeAuthorized,
        nodeCount = snapshot.Nodes.Count,
        roadCount = snapshot.Roads.Count,
        buildingCount = snapshot.Buildings.Count,
        found = result.Found,
        reasonCode = result.ReasonCode,
        lengthMeters = result.LengthMeters,
        edgeIds = result.EdgeIds,
        localPoints = result.Points,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return result.Found ? 0 : 2;
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("NeighborhoodInputUnavailable");
    return 1;
}
