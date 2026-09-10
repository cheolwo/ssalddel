using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 승인된 원본 두 개의 계보만 다룬다. HTTP host, 수집기, migration, 건물 정규화는 시작하지 않는다.
var 결과 = new Dictionary<string, object?>
{
    ["database"] = "hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev",
    ["databaseWriteAttempted"] = false, ["committed"] = false,
    ["reviewStatus"] = "PendingHumanReview", ["runtimeAuthorized"] = false,
};
var 파일잠금 = new List<FileStream>();
try
{
    if (args.Length != 2 || args[0] is not ("preview" or "apply" or "verify"))
    {
        Console.Error.WriteLine("사용법: preview|apply|verify 저장소절대경로");
        return 64;
    }
    var 모드 = args[0];
    var 저장소 = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
    var 원본폴더 = Path.Combine(저장소, "artifacts/local/neighborhood-source-acquisition");
    var 입력들 = new[]
    {
        new 원본입력("AL_D010_11_20260809.zip", 동네공간SourceRegistration.Gis건물DatasetId,
            "2026-08-09", "al-d010-seoul-20260809", "application/zip", 135675376,
            "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"),
        new 원본입력("국가중점데이터_컬럼정의서(26.01.02)_배포용.xlsx", 동네공간SourceRegistration.Gis정의서DatasetId,
            "2026-01-02", "national-spatial-columns-20260102",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 224115,
            "46DD29C6AB681C1E34CF00D91F8F2FE68B7E1868A853315EAA292838238ECB0F"),
    };
    foreach (var 입력 in 입력들)
    {
        // hash 확인 이후 저장 완료까지 교체·변조를 막고 서비스의 읽기만 허용한다.
        var 파일 = File.Open(Path.Combine(원본폴더, 입력.파일명), FileMode.Open, FileAccess.Read, FileShare.Read);
        파일잠금.Add(파일);
        확인(파일.Length == 입력.크기, "SourceLengthMismatch");
        확인(Convert.ToHexString(await SHA256.HashDataAsync(파일)) == 입력.해시, "SourceHashMismatch");
    }

    // Docker의 비밀값은 메모리 안에서만 읽는다. 원문·연결 문자열·예외 메시지는 출력하지 않는다.
    using var 컨테이너문서 = await 컨테이너확인();
    var 컨테이너 = 컨테이너문서.RootElement[0];
    확인(컨테이너.GetProperty("Name").GetString() == "/hongdal-mysql-1"
        && 컨테이너.GetProperty("State").GetProperty("Running").GetBoolean(), "ContainerMismatch");
    var 설정 = 컨테이너.GetProperty("Config");
    var 라벨 = 설정.GetProperty("Labels");
    확인(라벨.GetProperty("com.docker.compose.project").GetString() == "hongdal"
        && 라벨.GetProperty("com.docker.compose.service").GetString() == "mysql"
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            라벨.GetProperty("com.docker.compose.project.working_dir").GetString()!)), 저장소, StringComparison.OrdinalIgnoreCase)
        && 라벨.GetProperty("com.docker.compose.project.config_files").GetString()!
            .EndsWith("docker-compose.dev-deps.yml", StringComparison.Ordinal), "ComposeMismatch");
    확인(컨테이너.GetProperty("NetworkSettings").GetProperty("Ports").GetProperty("3306/tcp")
        .EnumerateArray().Any(x => x.GetProperty("HostPort").GetString() == "13306"), "PortMismatch");
    var 환경 = 설정.GetProperty("Env").EnumerateArray().Select(x => x.GetString()!.Split('=', 2))
        .ToDictionary(x => x[0], x => x[1]);
    확인(환경["MYSQL_DATABASE"] == "hongdal_dev" && 환경["MYSQL_USER"] != "root", "DatabaseMismatch");
    var 연결 = new MySqlConnectionStringBuilder
    {
        Server = "127.0.0.1", Port = 13306, Database = "hongdal_dev",
        UserID = 환경["MYSQL_USER"], Password = 환경["MYSQL_PASSWORD"],
        PersistSecurityInfo = false, Pooling = false, ConnectionTimeout = 10, DefaultCommandTimeout = 30,
    };
    var 옵션 = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
        .UseMySql(연결.ConnectionString, new MySqlServerVersion(new Version(8, 4, 0))).Options;
    // .NET 10의 배열 Contains가 Span 식으로 선택되지 않도록 EF 조회에는 List를 쓴다.
    var 식별자들 = 입력들.Select(x => x.DatasetId).ToList();
    await using (var 사전조회 = new PublicDataIngestionDbContext(옵션))
    {
        // 현재 표·열을 읽어 확인할 뿐 스키마를 생성하거나 갱신하지 않는다.
        _ = await 사전조회.IngestionRuns.AsNoTracking().Take(1).ToListAsync();
        var 기존 = await 사전조회.RawSnapshots.AsNoTracking()
            .Where(x => x.SourceId == "vworld" && 식별자들.Contains(x.DatasetId)).ToListAsync();
        결과["beforeCount"] = 기존.Count;
        foreach (var 기록 in 기존)
        {
            var 입력 = 입력들.Single(x => x.DatasetId == 기록.DatasetId);
            확인(string.Equals(기록.ContentHashSha256, 입력.해시, StringComparison.OrdinalIgnoreCase)
                && 기록.SourceVersion == 입력.판본
                && 기록.ContentLength == 입력.크기
                && 기록.OriginalFileName == 입력.파일명
                && 기록.StorageObjectName == Path.GetRelativePath(저장소, Path.Combine(원본폴더, 입력.파일명)).Replace('\\', '/'),
                "ExistingSourceConflict");
        }
    }

    if (모드 == "apply")
    {
        await using var 저장 = new PublicDataIngestionDbContext(옵션);
        await 저장.Database.OpenConnectionAsync();
        await using var 잠금명령 = 저장.Database.GetDbConnection().CreateCommand();
        잠금명령.CommandText = "SELECT GET_LOCK('mirror:neighborhood:raw-source-registration', 0)";
        확인(Convert.ToInt32(await 잠금명령.ExecuteScalarAsync()) == 1, "SourceRegistrationBusy");
        // 연결 종료 때 advisory lock이 해제된다. 같은 도구의 동시 실행은 쓰기를 시작하지 않는다.
        await using var 트랜잭션 = await 저장.Database.BeginTransactionAsync();
        var 서비스 = new 평창군공공공간원본등록Service(저장);
        var 추가수 = 0;
        var 기존수 = 0;
        결과["databaseWriteAttempted"] = true;
        foreach (var 입력 in 입력들)
        {
            var 경로 = Path.Combine(원본폴더, 입력.파일명);
            var 등록 = await 서비스.RegisterFileAsync(경로,
                new 공공공간원본등록Request("vworld", 입력.DatasetId, 입력.판본, 입력.개정,
                    DateTimeOffset.Parse(입력.판본 + "T00:00:00Z"), 입력.ContentType,
                    Path.GetRelativePath(저장소, 경로)));
            if (등록.Inserted)
            {
                추가수++;
                var 사본 = await 저장.RawSnapshots.SingleAsync(x => x.Id == 등록.RawSnapshotId);
                var 실행 = await 저장.IngestionRuns.SingleAsync(x => x.Id == 사본.FirstCollectionRunId);
                실행.StatusCode = 외부데이터수집StatusCodes.Partial;
                실행.ErrorCode = "NeighborhoodSourceReviewPending";
                실행.ErrorSummary = "원본 계보만 등록. 정규화·적용 권리 검토보류. docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/source-acquisition.md";
                await 저장.SaveChangesAsync();
            }
            else 기존수++;
        }
        await 트랜잭션.CommitAsync();
        결과["committed"] = true;
        결과["inserted"] = 추가수;
        결과["existing"] = 기존수;
    }

    // 저장 문맥과 다른 연결에서 다시 읽는다. verify/preview는 어떤 행도 수정하지 않는다.
    await using var 재조회 = new PublicDataIngestionDbContext(옵션);
    var 행들 = await 재조회.RawSnapshots.AsNoTracking()
        .Where(x => x.SourceId == "vworld" && 식별자들.Contains(x.DatasetId))
        .Include(x => x.FirstCollectionRun).OrderBy(x => x.DatasetId).ToListAsync();
    if (모드 != "preview") 확인(행들.Count == 2, "StoredSourceCountMismatch");
    foreach (var 행 in 행들)
    {
        var 입력 = 입력들.Single(x => x.DatasetId == 행.DatasetId);
        확인(string.Equals(행.ContentHashSha256, 입력.해시, StringComparison.OrdinalIgnoreCase)
            && 행.ContentLength == 입력.크기 && 행.SourceVersion == 입력.판본
            && 행.StorageContainer == "local-private-public-spatial"
            && 행.FirstCollectionRun!.StatusCode == 외부데이터수집StatusCodes.Partial
            && 행.FirstCollectionRun.ErrorCode == "NeighborhoodSourceReviewPending"
            && 행.FirstCollectionRun.NormalizedCount == 0, "StoredSourceMismatch");
    }
    var ids = 행들.Select(x => x.Id).ToList();
    확인(!await 재조회.NormalizedRecords.AnyAsync(x => ids.Contains(x.RawSnapshotId))
        && !await 재조회.BuildingRegisterTitles.AnyAsync(x => ids.Contains(x.EvidenceSnapshotId)), "UnexpectedNormalization");
    결과["mode"] = 모드;
    결과["rows"] = 행들.Select(x => new
    {
        x.Id, x.FirstCollectionRunId, x.SourceId, x.DatasetId, x.SourceVersion,
        x.ContentHashSha256, x.ContentLength, x.StorageContainer,
        status = x.FirstCollectionRun!.StatusCode, review = x.FirstCollectionRun.ErrorCode,
    });
    Console.WriteLine(JsonSerializer.Serialize(결과, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception 오류)
{
    결과["errorCode"] = 오류 is 작업차단Exception ? 오류.Message : 오류.GetType().Name;
    if (오류 is not 작업차단Exception)
    {
        // 비밀값이 섞일 수 있는 예외 본문 대신 형식과 호출 위치만 남긴다.
        결과["innerErrorType"] = 오류.InnerException?.GetType().Name;
        결과["failureMethods"] = new StackTrace(오류).GetFrames()?.Take(8)
            .Select(x => x.GetMethod())
            .Select(x => $"{x?.DeclaringType?.FullName}.{x?.Name}");
    }
    Console.WriteLine(JsonSerializer.Serialize(결과));
    return 1;
}
finally
{
    foreach (var 잠금 in 파일잠금) await 잠금.DisposeAsync();
}

static void 확인(bool 값, string 코드)
{
    if (!값) throw new 작업차단Exception(코드);
}

static async Task<JsonDocument> 컨테이너확인()
{
    var 시작 = new ProcessStartInfo("docker")
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
    };
    시작.ArgumentList.Add("inspect");
    시작.ArgumentList.Add("hongdal-mysql-1");
    using var 프로세스 = Process.Start(시작)!;
    var 내용 = 프로세스.StandardOutput.ReadToEndAsync();
    var 오류 = 프로세스.StandardError.ReadToEndAsync();
    using var 제한 = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try { await 프로세스.WaitForExitAsync(제한.Token); }
    catch { if (!프로세스.HasExited) 프로세스.Kill(); throw; }
    await 오류;
    확인(프로세스.ExitCode == 0, "DockerInspectionFailed");
    return JsonDocument.Parse(await 내용);
}

internal sealed record 원본입력(string 파일명, string DatasetId, string 판본, string 개정, string ContentType, long 크기, string 해시);
internal sealed class 작업차단Exception(string 코드) : Exception(코드);
