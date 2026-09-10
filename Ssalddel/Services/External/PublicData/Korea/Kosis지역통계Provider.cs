using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;

namespace 살뜰.Services.External.PublicData.Korea;

public static class Kosis지역통계Dataset
{
    public const string SourceId = "kosis-regional-statistics";
    public const string DatasetId = SourceId;
    public const string SourceVersion = "kosis-regional-statistics-v1";
    public const string RegionCodeVersion = "kosis-administrative-classification-2026-09";

    public static readonly IReadOnlySet<string> MetricCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "demography.registered-population",
        "demography.household-count",
        "demography.population.age-0-14",
        "demography.population.age-15-64",
        "demography.population.age-65-plus",
        "economy.establishment-count",
        "economy.worker-count",
        "economy.employment-rate",
    };
}

internal sealed record Kosis지역통계Query(
    string TableId,
    string MetricCode,
    string PeriodKind,
    string ItemId,
    string ObjectLevel2 = "",
    string ObjectLevel3 = "");

public sealed class Kosis지역통계Collector(HttpClient httpClient) : IExternalDataCollector
{
    private const string Endpoint = "/openapi/Param/statisticsParameterData.do";

    private static readonly Kosis지역통계Query[] Queries =
    [
        new("DT_1B040A3", "demography.registered-population", "M", "T20"),
        new("DT_1B040B3", "demography.household-count", "M", "all"),
        new("DT_1B040M5_1", "demography.population.age-bands", "Y", "T10", "all", "all"),
        new("DT_1YL20832", "economy.establishment-count", "Y", "all"),
        new("DT_1YL15014", "economy.worker-count", "Y", "all"),
        new("INH_1DA7014S_03", "economy.employment-rate", "M", "T90", "0"),
        new("INH_1ES3A02S_03", "economy.employment-rate", "M", "T90", "0"),
    ];

    public bool CanCollect(ExternalDataSourceDefinition source)
        => source.SourceId == Kosis지역통계Dataset.SourceId
           && source.DatasetId == Kosis지역통계Dataset.DatasetId;

    public async Task<ExternalDataCollectedPayload> CollectAsync(
        ExternalDataSourceDefinition source,
        ExternalDataIngestionRequest request,
        ExternalDataCredential? credential,
        CancellationToken cancellationToken = default)
    {
        if (!CanCollect(source))
            throw new ExternalDataCollectionException(ExternalDataCollectionErrorCode.CollectorMissing);
        if (credential is null || credential.Type != ExternalDataCredentialType.ApiKeyQuery)
            throw new ExternalDataCollectionException(ExternalDataCollectionErrorCode.MissingCredential);

        var tables = new List<object>(Queries.Length);
        var fetchedCount = 0;
        foreach (var query in Queries)
        {
            using var response = await SendAsync(query, credential.SecretValue, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw HttpError(response.StatusCode);

            JsonElement rows;
            try
            {
                using var document = JsonDocument.Parse(bytes);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("KosisArrayRequired");
                rows = document.RootElement.Clone();
            }
            catch (JsonException error)
            {
                throw new ExternalDataCollectionException(
                    ExternalDataCollectionErrorCode.InvalidPayload,
                    innerException: error);
            }

            fetchedCount += rows.GetArrayLength();
            tables.Add(new
            {
                tableId = query.TableId,
                metricCode = query.MetricCode,
                periodKind = query.PeriodKind,
                rows,
            });
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = Kosis지역통계Dataset.SourceVersion,
            endpoint = Endpoint,
            latestCompletedPeriodCount = 2,
            tables,
        });
        return new ExternalDataCollectedPayload(
            new MemoryStream(payload, writable: false),
            "kosis-regional-statistics.json",
            "application/json",
            null,
            Kosis지역통계Dataset.SourceVersion,
            fetchedCount);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Kosis지역통계Query query,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "getList",
            ["apiKey"] = apiKey,
            ["orgId"] = "101",
            ["tblId"] = query.TableId,
            ["objL1"] = "all",
            ["objL2"] = query.ObjectLevel2,
            ["objL3"] = query.ObjectLevel3,
            ["objL4"] = "",
            ["objL5"] = "",
            ["objL6"] = "",
            ["objL7"] = "",
            ["objL8"] = "",
            ["itmId"] = query.ItemId,
            ["prdSe"] = query.PeriodKind,
            ["newEstPrdCnt"] = "2",
            ["format"] = "json",
            ["jsonVD"] = "Y",
            ["smblChk"] = "Y",
            ["outputFields"] = "ORG_ID,TBL_ID,TBL_NM,C1,C1_NM,C2,C2_NM,C3,C3_NM,ITM_ID,ITM_NM,UNIT_ID,UNIT_NM,PRD_SE,PRD_DE,DT,LST_CHN_DE",
        };
        var queryString = string.Join('&', parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return await httpClient.GetAsync($"{Endpoint}?{queryString}", cancellationToken);
    }

    private static ExternalDataCollectionException HttpError(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized => new(ExternalDataCollectionErrorCode.Unauthorized),
            HttpStatusCode.Forbidden => new(ExternalDataCollectionErrorCode.Forbidden),
            HttpStatusCode.NotFound => new(ExternalDataCollectionErrorCode.NotFound),
            HttpStatusCode.TooManyRequests => new(ExternalDataCollectionErrorCode.RateLimited, true),
            _ => new(ExternalDataCollectionErrorCode.InvalidPayload),
        };
}

public sealed record Kosis행정구역Resolution(
    string RegionStableId,
    string DisplayName,
    string SpatialPrecisionCode);

public interface IKosis행정구역Resolver
{
    Task<Kosis행정구역Resolution?> ResolveAsync(
        string externalCode,
        string externalName,
        CancellationToken cancellationToken = default);
}

public sealed class EfKosis행정구역Resolver(PublicDataIngestionDbContext dbContext)
    : IKosis행정구역Resolver
{
    private static readonly IReadOnlyDictionary<string, string> ProvinceNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["11"] = "서울특별시", ["21"] = "부산광역시", ["22"] = "대구광역시",
            ["23"] = "인천광역시", ["24"] = "광주광역시", ["25"] = "대전광역시",
            ["26"] = "울산광역시", ["29"] = "세종특별자치시", ["31"] = "경기도",
            ["32"] = "강원특별자치도", ["33"] = "충청북도", ["34"] = "충청남도",
            ["35"] = "전북특별자치도", ["36"] = "전라남도", ["37"] = "경상북도",
            ["38"] = "경상남도", ["39"] = "제주특별자치도",
        };

    public async Task<Kosis행정구역Resolution?> ResolveAsync(
        string externalCode,
        string externalName,
        CancellationToken cancellationToken = default)
    {
        var code = externalCode.Trim();
        var name = externalName.Trim();
        if (code.Length < 2 || !ProvinceNames.TryGetValue(code[..2], out var provinceName))
            return null;
        var level = code.Length <= 2 || string.Equals(name, provinceName, StringComparison.Ordinal)
            ? "province"
            : "city-county-district";
        var fullName = level == "province" ? provinceName : $"{provinceName} {name}";
        var matches = await dbContext.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == 대한민국법정동CodeDataset.SourceId
                           && item.DatasetId == 대한민국법정동CodeDataset.DatasetId
                           && item.MetricCode == 대한민국법정동CodeDataset.MetricCode
                           && item.SpatialPrecisionCode == level
                           && item.TextValue == fullName)
            .Select(item => new Kosis행정구역Resolution(
                item.RegionStableId,
                item.TextValue,
                item.SpatialPrecisionCode))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return matches.Length == 1 ? matches[0] : null;
    }
}

public sealed class Kosis지역통계Normalizer(IKosis행정구역Resolver regionResolver)
    : IExternalDataNormalizer
{
    private static readonly IReadOnlySet<string> Age0To14 = Set("020", "050", "070");
    private static readonly IReadOnlySet<string> Age15To64 = Set("100", "120", "130", "150", "160", "180", "190", "210", "230", "260");
    private static readonly IReadOnlySet<string> Age65Plus = Set("280", "310", "330", "340", "360", "370", "380", "410", "430", "440");

    public bool CanNormalize(ExternalDataSourceDefinition source)
        => source.SourceId == Kosis지역통계Dataset.SourceId
           && source.DatasetId == Kosis지역통계Dataset.DatasetId;

    public async Task<ExternalDataNormalizationBatch> NormalizeAsync(
        ExternalDataSourceDefinition source,
        외부데이터RawSnapshot rawSnapshot,
        IExternalDataRawStorage rawStorage,
        CancellationToken cancellationToken = default)
    {
        if (!CanNormalize(source))
            throw new ExternalDataCollectionException(ExternalDataCollectionErrorCode.NormalizerMissing);
        try
        {
            await using var stream = await rawStorage.OpenReadAsync(rawSnapshot, cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tables = document.RootElement.GetProperty("tables");
            var records = new List<외부데이터정규화Record>();
            var rejected = 0;
            foreach (var table in tables.EnumerateArray())
            {
                var metric = Required(table, "metricCode");
                var tableId = Required(table, "tableId");
                var periodKind = Required(table, "periodKind");
                var rows = table.GetProperty("rows").EnumerateArray().Select(item => item.Clone()).ToArray();
                if (metric == "demography.population.age-bands")
                    rejected += await NormalizeAgeBandsAsync(rows, tableId, periodKind, rawSnapshot, records, cancellationToken);
                else
                    rejected += await NormalizeRowsAsync(rows, metric, tableId, periodKind, rawSnapshot, records, cancellationToken);
            }
            var revision = $"kosis-{rawSnapshot.ContentHashSha256[..Math.Min(20, rawSnapshot.ContentHashSha256.Length)]}";
            foreach (var record in records) record.DataRevision = revision;
            return new ExternalDataNormalizationBatch(records, rejected, revision);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ExternalDataCollectionException(ExternalDataCollectionErrorCode.InvalidPayload, innerException: error);
        }
    }

    private async Task<int> NormalizeRowsAsync(
        IReadOnlyCollection<JsonElement> rows,
        string metric,
        string tableId,
        string periodKind,
        외부데이터RawSnapshot raw,
        ICollection<외부데이터정규화Record> output,
        CancellationToken cancellationToken)
    {
        var rejected = 0;
        foreach (var row in rows)
        {
            var itemName = Value(row, "ITM_NM");
            if (!ExpectedItem(metric, itemName)) continue;
            if (!TryNumeric(Value(row, "DT"), out var value)) { rejected++; continue; }
            var region = await ResolveAsync(row, cancellationToken);
            if (region is null) { rejected++; continue; }
            var period = Value(row, "PRD_DE");
            if (!TryEvidenceDate(period, periodKind, out var evidenceAt)) { rejected++; continue; }
            output.Add(Map(raw, row, region, metric, tableId, periodKind, period, evidenceAt, value));
        }
        return rejected;
    }

    private async Task<int> NormalizeAgeBandsAsync(
        IReadOnlyCollection<JsonElement> rows,
        string tableId,
        string periodKind,
        외부데이터RawSnapshot raw,
        ICollection<외부데이터정규화Record> output,
        CancellationToken cancellationToken)
    {
        var rejected = 0;
        var groups = rows
            .Where(row => IsTotalSex(Value(row, "C2"), Value(row, "C2_NM")))
            .GroupBy(row => $"{Value(row, "C1")}|{Value(row, "C1_NM")}|{Value(row, "PRD_DE")}", StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var first = group.First();
            var region = await ResolveAsync(first, cancellationToken);
            var period = Value(first, "PRD_DE");
            if (region is null || !TryEvidenceDate(period, periodKind, out var evidenceAt)) { rejected++; continue; }
            var values = group
                .Where(row => TryNumeric(Value(row, "DT"), out _))
                .ToDictionary(row => Value(row, "C3"), row => decimal.Parse(Value(row, "DT").Replace(",", ""), CultureInfo.InvariantCulture), StringComparer.Ordinal);
            foreach (var band in new[]
                     {
                         (Codes: Age0To14, Metric: "demography.population.age-0-14"),
                         (Codes: Age15To64, Metric: "demography.population.age-15-64"),
                         (Codes: Age65Plus, Metric: "demography.population.age-65-plus"),
                     })
            {
                if (!band.Codes.All(values.ContainsKey)) { rejected++; continue; }
                output.Add(Map(raw, first, region, band.Metric, tableId, periodKind, period, evidenceAt,
                    band.Codes.Sum(code => values[code]), string.Join(',', band.Codes.OrderBy(code => code, StringComparer.Ordinal))));
            }
        }
        return rejected;
    }

    private async Task<Kosis행정구역Resolution?> ResolveAsync(JsonElement row, CancellationToken cancellationToken)
        => await regionResolver.ResolveAsync(Value(row, "C1"), Value(row, "C1_NM"), cancellationToken);

    private static 외부데이터정규화Record Map(
        외부데이터RawSnapshot raw,
        JsonElement row,
        Kosis행정구역Resolution region,
        string metric,
        string tableId,
        string periodKind,
        string period,
        DateTimeOffset evidenceAt,
        decimal value,
        string ageCodes = "")
    {
        var itemId = Value(row, "ITM_ID");
        var externalCode = Value(row, "C1");
        var dimension = string.Join('|', new[]
        {
            $"period={Escape(period)}", $"table={Escape(tableId)}", $"item={Escape(itemId)}",
            $"external-region-code={Escape(externalCode)}", $"region-code-version={Kosis지역통계Dataset.RegionCodeVersion}",
            $"last-changed={Escape(Value(row, "LST_CHN_DE"))}", $"classification={Escape(ageCodes)}",
            $"raw-sha256={raw.ContentHashSha256}"
        });
        var recordKey = 외부데이터RecordKey.Create(raw.SourceId, raw.DatasetId, region.RegionStableId,
            metric, evidenceAt, dimension);
        return new 외부데이터정규화Record
        {
            RawSnapshotId = raw.Id,
            RecordKey = recordKey,
            StableId = $"regional-stat:{recordKey}",
            SourceId = raw.SourceId,
            DatasetId = raw.DatasetId,
            RegionStableId = region.RegionStableId,
            MetricCode = metric,
            NumericValue = value,
            TextValue = region.DisplayName,
            UnitCode = NormalizeUnit(Value(row, "UNIT_NM"), metric),
            EvidenceAsOfUtc = evidenceAt,
            CollectedAtUtc = raw.CollectedAtUtc,
            SpatialPrecisionCode = region.SpatialPrecisionCode,
            TemporalPrecisionCode = periodKind == "M" ? "month" : "year",
            QualityCode = "official-published",
            LimitationCode = "kosis-published-aggregate;not-person-or-address-data",
            DimensionKey = dimension.Length <= 500 ? dimension : dimension[..500],
            SourceVersion = raw.SourceVersion,
            FirstSeenAtUtc = raw.CollectedAtUtc,
            LastSeenAtUtc = raw.CollectedAtUtc,
        };
    }

    private static bool ExpectedItem(string metric, string itemName) => metric switch
    {
        "demography.registered-population" => itemName.Contains("총인구", StringComparison.Ordinal) || itemName.Contains("인구수", StringComparison.Ordinal),
        "demography.household-count" => itemName.Contains("세대", StringComparison.Ordinal) || itemName.Contains("가구", StringComparison.Ordinal),
        "economy.establishment-count" => itemName.Contains("사업체", StringComparison.Ordinal),
        "economy.worker-count" => itemName.Contains("종사자", StringComparison.Ordinal),
        "economy.employment-rate" => itemName.Contains("고용률", StringComparison.Ordinal),
        _ => false,
    };

    private static bool TryNumeric(string text, out decimal value)
        => decimal.TryParse(text.Replace(",", ""), NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);

    private static bool TryEvidenceDate(string period, string kind, out DateTimeOffset value)
    {
        value = default;
        if (kind == "M" && period.Length == 6
                        && int.TryParse(period[..4], out var year)
                        && int.TryParse(period[4..], out var month)
                        && month is >= 1 and <= 12)
        {
            value = new DateTimeOffset(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, TimeSpan.Zero);
            return true;
        }
        if (kind == "Y" && period.Length == 4 && int.TryParse(period, out year))
        {
            value = new DateTimeOffset(year, 12, 31, 0, 0, 0, TimeSpan.Zero);
            return true;
        }
        return false;
    }

    private static string Required(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"KosisFieldMissing:{name}");
    private static string Value(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.ToString().Trim() : string.Empty;
    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);
    private static bool IsTotalSex(string code, string name)
        => code is "0" or "00" || name is "계" or "전체" or "총계";
    private static string NormalizeUnit(string unit, string metric)
        => metric == "economy.employment-rate" ? "percent"
            : unit.Contains("가구", StringComparison.Ordinal) || unit.Contains("세대", StringComparison.Ordinal) ? "household"
            : unit.Contains("개", StringComparison.Ordinal) || metric.Contains("establishment", StringComparison.Ordinal) ? "establishment"
            : "person";
    private static IReadOnlySet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
}
