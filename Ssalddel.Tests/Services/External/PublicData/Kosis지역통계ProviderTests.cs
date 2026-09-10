using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Services.Community;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.External.PublicData.Korea;

namespace Ssalddel.Tests.Services.External.PublicData;

public sealed class Kosis지역통계ProviderTests
{
    [Fact]
    public async Task Collector_FailsClosedWhenCredentialIsMissing()
    {
        var handler = new RecordingHandler();
        var collector = new Kosis지역통계Collector(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://kosis.kr"),
        });

        var error = await Assert.ThrowsAsync<ExternalDataCollectionException>(() =>
            collector.CollectAsync(Source(), Request(), null));

        Assert.Equal(ExternalDataCollectionErrorCode.MissingCredential, error.ErrorCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Collector_UsesCurrentHttpsParameterEndpoint_AndRequestsLatestTwoPeriods()
    {
        var handler = new RecordingHandler();
        var collector = new Kosis지역통계Collector(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://kosis.kr"),
        });

        await using var payload = await collector.CollectAsync(Source(), Request(),
            new ExternalDataCredential(ExternalDataCredentialType.ApiKeyQuery, "PublicData:Kosis:ServiceKey", "secret"));
        using var document = await JsonDocument.ParseAsync(payload.Content);

        Assert.Equal(7, handler.Requests.Count);
        Assert.All(handler.Requests, uri =>
        {
            Assert.Equal("https", uri.Scheme);
            Assert.Equal("kosis.kr", uri.Host);
            Assert.Equal("/openapi/Param/statisticsParameterData.do", uri.AbsolutePath);
            Assert.Contains("newEstPrdCnt=2", uri.Query, StringComparison.Ordinal);
            Assert.Contains("smblChk=Y", uri.Query, StringComparison.Ordinal);
        });
        Assert.DoesNotContain("secret", document.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(7, payload.FetchedCount);
    }

    [Fact]
    public async Task Normalizer_PreservesLineage_AndRejectsMaskedValueInsteadOfZero()
    {
        var envelope = Envelope(new object[]
        {
            Table("DT_1B040A3", "demography.registered-population", "M", new[]
            {
                Row("11", "서울특별시", "202608", "T20", "총인구수", "명", "9,300,000"),
                Row("11070", "중랑구", "202608", "T20", "총인구수", "명", "-"),
            }),
        });
        var raw = Raw(envelope);
        var normalizer = new Kosis지역통계Normalizer(new StubResolver());

        var batch = await normalizer.NormalizeAsync(Source(), raw, new MemoryRawStorage(envelope));

        var record = Assert.Single(batch.Records);
        Assert.Equal(9_300_000m, record.NumericValue);
        Assert.Equal("region:kr:bjd:1100000000", record.RegionStableId);
        Assert.Equal("month", record.TemporalPrecisionCode);
        Assert.Contains("period=202608", record.DimensionKey, StringComparison.Ordinal);
        Assert.Contains("raw-sha256=", record.DimensionKey, StringComparison.Ordinal);
        Assert.Equal(1, batch.RejectedCount);
        Assert.DoesNotContain(batch.Records, item => item.NumericValue == 0m);
    }

    [Fact]
    public async Task Normalizer_AggregatesOfficialFiveYearAgeCodes_IntoThreeBands()
    {
        var ageCodes = new[]
        {
            "020", "050", "070", "100", "120", "130", "150", "160", "180", "190", "210", "230", "260",
            "280", "310", "330", "340", "360", "370", "380", "410", "430", "440"
        };
        var rows = ageCodes.Select(code => Row("11070", "중랑구", "2025", "T10", "연앙인구", "명", "10",
            c2: "0", c2Name: "계", c3: code)).ToArray();
        var envelope = Envelope(new object[]
        {
            Table("DT_1B040M5_1", "demography.population.age-bands", "Y", rows),
        });
        var batch = await new Kosis지역통계Normalizer(new StubResolver())
            .NormalizeAsync(Source(), Raw(envelope), new MemoryRawStorage(envelope));

        Assert.Equal(3, batch.Records.Count);
        Assert.Equal(30m, batch.Records.Single(item => item.MetricCode.EndsWith("age-0-14", StringComparison.Ordinal)).NumericValue);
        Assert.Equal(100m, batch.Records.Single(item => item.MetricCode.EndsWith("age-15-64", StringComparison.Ordinal)).NumericValue);
        Assert.Equal(100m, batch.Records.Single(item => item.MetricCode.EndsWith("age-65-plus", StringComparison.Ordinal)).NumericValue);
        Assert.Equal(0, batch.RejectedCount);
    }

    [Theory]
    [InlineData("11", "서울특별시", "region:kr:bjd:1100000000")]
    [InlineData("11070", "중랑구", "region:kr:bjd:1126000000")]
    [InlineData("21", "부산광역시", "region:kr:bjd:2600000000")]
    [InlineData("21090", "해운대구", "region:kr:bjd:2635000000")]
    public async Task EfResolver_MapsKosisNamesToExistingLegalRegionLedger(
        string code, string name, string expectedStableId)
    {
        await using var db = CreateDb();
        db.NormalizedRecords.AddRange(
            Legal("region:kr:bjd:1100000000", "서울특별시", "province", 1),
            Legal("region:kr:bjd:1126000000", "서울특별시 중랑구", "city-county-district", 2),
            Legal("region:kr:bjd:2600000000", "부산광역시", "province", 3),
            Legal("region:kr:bjd:2635000000", "부산광역시 해운대구", "city-county-district", 4));
        await db.SaveChangesAsync();

        var result = await new EfKosis행정구역Resolver(db).ResolveAsync(code, name);

        Assert.NotNull(result);
        Assert.Equal(expectedStableId, result.RegionStableId);
    }

    [Fact]
    public async Task UseCase_FiltersLevelPeriodAndMetric_AndReturnsOnlyPublicProjection()
    {
        await using var db = CreateDb();
        var run = new 외부데이터수집Run { Id = 10, RunKey = "run", SourceId = Kosis지역통계Dataset.SourceId, DatasetId = Kosis지역통계Dataset.DatasetId, StartedAtUtc = DateTimeOffset.UtcNow };
        var raw = new 외부데이터RawSnapshot
        {
            Id = 11, FirstCollectionRunId = 10, SourceId = Kosis지역통계Dataset.SourceId,
            DatasetId = Kosis지역통계Dataset.DatasetId, ContentHashSha256 = new string('a', 64),
            StorageContainer = "tests", StorageObjectName = "raw.json", StorageLocation = "memory://raw",
            CollectedAtUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow,
        };
        db.IngestionRuns.Add(run);
        db.RawSnapshots.Add(raw);
        db.NormalizedRecords.AddRange(
            Statistic(20, 11, "region:kr:bjd:1100000000", "서울특별시", "province", "demography.registered-population", "202608", 9_300_000m),
            Statistic(21, 11, "region:kr:bjd:1126000000", "서울특별시 중랑구", "city-county-district", "demography.registered-population", "202608", 380_000m),
            Statistic(22, 11, "region:kr:bjd:1126000000", "서울특별시 중랑구", "city-county-district", "economy.employment-rate", "202608", 61.2m));
        await db.SaveChangesAsync();
        var useCase = new 지역공공통계조회UseCase(db, TimeProvider.System);

        var result = await useCase.조회Async("KR", "Sigungu", "2026-08", "demography.registered-population");
        var region = Assert.Single(result.Regions);
        var metric = Assert.Single(region.Metrics);

        Assert.Equal("region:kr:bjd:1126000000", region.RegionStableId);
        Assert.Equal(380_000m, metric.Value);
        Assert.Equal(new string('a', 64), metric.RawContentHashSha256);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Address", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Driver", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Controller_ReturnsBadRequestForUnsupportedCountryWithoutProviderCall()
    {
        var controller = new 지역공공통계WorldController(new ThrowingUseCase());
        var result = await controller.조회("US", "Sido", "latest", null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static ExternalDataSourceDefinition Source() => new()
    {
        SourceId = Kosis지역통계Dataset.SourceId,
        DatasetId = Kosis지역통계Dataset.DatasetId,
        Name = "KOSIS 지역 통계",
        Provider = "KOSIS",
        AccessMethod = ExternalDataAccessMethod.HttpApi,
        CredentialType = ExternalDataCredentialType.ApiKeyQuery,
        RequiresCredential = true,
        ApiAvailable = true,
        CredentialReferences = ["PublicData:Kosis:ServiceKey"],
    };

    private static ExternalDataIngestionRequest Request() => new()
    {
        SourceId = Kosis지역통계Dataset.SourceId,
        DatasetId = Kosis지역통계Dataset.DatasetId,
    };

    private static byte[] Envelope(object[] tables) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = Kosis지역통계Dataset.SourceVersion,
        endpoint = "/openapi/Param/statisticsParameterData.do",
        latestCompletedPeriodCount = 2,
        tables,
    });

    private static object Table(string tableId, string metricCode, string periodKind, object[] rows)
        => new { tableId, metricCode, periodKind, rows };

    private static object Row(string code, string name, string period, string itemId, string itemName,
        string unit, string value, string c2 = "", string c2Name = "", string c3 = "") => new
    {
        ORG_ID = "101", TBL_ID = "table", TBL_NM = "통계표", C1 = code, C1_NM = name,
        C2 = c2, C2_NM = c2Name, C3 = c3, C3_NM = c3, ITM_ID = itemId, ITM_NM = itemName,
        UNIT_ID = unit, UNIT_NM = unit, PRD_SE = period.Length == 6 ? "M" : "Y", PRD_DE = period,
        DT = value, LST_CHN_DE = "20260901",
    };

    private static 외부데이터RawSnapshot Raw(byte[] bytes) => new()
    {
        Id = 7, SourceId = Kosis지역통계Dataset.SourceId, DatasetId = Kosis지역통계Dataset.DatasetId,
        SourceVersion = Kosis지역통계Dataset.SourceVersion, CollectedAtUtc = DateTimeOffset.UtcNow,
        ContentHashSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
    };

    private static PublicDataIngestionDbContext CreateDb() => new(
        new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseInMemoryDatabase($"kosis-{Guid.NewGuid():N}").Options);

    private static 외부데이터정규화Record Legal(string id, string name, string level, long rawId) => new()
    {
        RawSnapshotId = rawId, RecordKey = Guid.NewGuid().ToString("N"), StableId = id,
        SourceId = 대한민국법정동CodeDataset.SourceId, DatasetId = 대한민국법정동CodeDataset.DatasetId,
        RegionStableId = id, MetricCode = 대한민국법정동CodeDataset.MetricCode, TextValue = name,
        UnitCode = "text", EvidenceAsOfUtc = DateTimeOffset.UtcNow, CollectedAtUtc = DateTimeOffset.UtcNow,
        SpatialPrecisionCode = level, TemporalPrecisionCode = "collection-time", DataRevision = "test",
    };

    private static 외부데이터정규화Record Statistic(long id, long rawId, string regionId, string name,
        string level, string metric, string period, decimal value) => new()
    {
        Id = id, RawSnapshotId = rawId, RecordKey = Guid.NewGuid().ToString("N"), StableId = $"regional-stat:{id}",
        SourceId = Kosis지역통계Dataset.SourceId, DatasetId = Kosis지역통계Dataset.DatasetId,
        RegionStableId = regionId, MetricCode = metric, NumericValue = value, TextValue = name,
        UnitCode = metric.EndsWith("rate", StringComparison.Ordinal) ? "percent" : "person",
        EvidenceAsOfUtc = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero),
        CollectedAtUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        SpatialPrecisionCode = level, TemporalPrecisionCode = "month", QualityCode = "official-published",
        LimitationCode = "public-aggregate", DimensionKey = $"period={period}|table=test|item=T1|external-region-code=code|region-code-version=test|last-changed=20260901",
        SourceVersion = "test", DataRevision = "test",
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var json = JsonSerializer.Serialize(new[] { Row("11", "서울특별시", "202608", "T20", "총인구수", "명", "1") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class MemoryRawStorage(byte[] bytes) : IExternalDataRawStorage
    {
        public Task<ExternalDataRawStorageResult> StoreAsync(ExternalDataSourceDefinition source, ExternalDataCollectedPayload payload, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(외부데이터RawSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    private sealed class StubResolver : IKosis행정구역Resolver
    {
        public Task<Kosis행정구역Resolution?> ResolveAsync(string externalCode, string externalName, CancellationToken cancellationToken = default)
        {
            var value = externalCode switch
            {
                "11" => new Kosis행정구역Resolution("region:kr:bjd:1100000000", "서울특별시", "province"),
                "11070" => new Kosis행정구역Resolution("region:kr:bjd:1126000000", "서울특별시 중랑구", "city-county-district"),
                _ => null,
            };
            return Task.FromResult(value);
        }
    }

    private sealed class ThrowingUseCase : I지역공공통계조회UseCase
    {
        public Task<지역공공통계SnapshotDto> 조회Async(string countryCode, string regionLevel, string period, string? metrics, CancellationToken cancellationToken = default)
            => throw new ArgumentException("countryCode는 KR만 지원합니다.");
    }
}
