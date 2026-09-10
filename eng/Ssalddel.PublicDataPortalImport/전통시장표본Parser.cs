using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ssalddel.Domain.PublicData;

internal static class 전통시장표본Parser
{
    public const string SourceId = "semas-traditional-market-status";
    public const string DatasetId = "data-go-kr-15012894-standard";
    public const string Revision = "jungnang-markets-20260908.r1";
    public const string Version = "reference-date:2025-11-10";
    private static readonly string[] Fields = ["ESTBL_YEAR", "REFERENCE_DATE", "MRKT_NM", "USE_GCCT", "LONGITUDE", "PRKPLCE_YN", "MRKT_TYPE", "RDNMADR", "INSTT_NM", "MRKT_ESTBL_CYCLE", "TRTMNT_PRDLST", "INSTT_CODE", "HOMEPAGE_URL", "PBLIC_TOILET_YN", "LNMADR", "PHONE_NUMBER", "LATITUDE", "STOR_NUMBER"];

    // 승인된 중랑구 표본만 처리한다. 포털 표기값을 보존하고 빈 값을 추정하지 않는다.
    public static List<외부데이터정규화Record> Parse(string json, DateTimeOffset collected)
    {
        if (Encoding.UTF8.GetByteCount(json) > 5 * 1024 * 1024) throw new InvalidDataException("PayloadTooLarge");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("ArrayRequired");
        var records = new List<외부데이터정규화Record>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (!row.TryGetProperty("RDNMADR", out var address) || address.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("AddressRequired");
            if (!address.GetString()!.StartsWith("서울특별시 중랑구 ", StringComparison.Ordinal)) continue;
            var properties = row.EnumerateObject().ToArray();
            if (properties.Length != Fields.Length || properties.Select(x => x.Name).Distinct().Count() != Fields.Length
                || Fields.Any(x => !row.TryGetProperty(x, out var v) || v.ValueKind != JsonValueKind.String))
                throw new InvalidDataException("ColumnsChanged");
            var values = Fields.Order(StringComparer.Ordinal).ToDictionary(x => x, x => row.GetProperty(x).GetString()!, StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(values["MRKT_NM"]) || values["INSTT_CODE"] != "B553077"
                || values["REFERENCE_DATE"] != "2025-11-10") throw new InvalidDataException("SourceIdentityOrDateChanged");
            if (!decimal.TryParse(values["LATITUDE"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lat) || lat is < 33 or > 39
                || !decimal.TryParse(values["LONGITUDE"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lon) || lon is < 124 or > 132
                || !int.TryParse(values["STOR_NUMBER"], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 0)
                throw new InvalidDataException("NumericValueInvalid");
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { values["MRKT_NM"], values["RDNMADR"] })))).ToLowerInvariant();
            if (!identities.Add(identity)) throw new InvalidDataException("DuplicateMarketIdentity");
            var text = JsonSerializer.Serialize(values, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            if (text.Length > 2000) throw new InvalidDataException("RecordTooLarge");
            // 날짜 정밀도만 있는 원천을 UTC 자정으로 정규화. 실제 관측 시각으로 해석하지 않는다.
            var date = new DateTimeOffset(2025, 11, 10, 0, 0, 0, TimeSpan.Zero);
            var dimension = "market-name-road-address-sha256=" + identity;
            records.Add(new 외부데이터정규화Record
            {
                SourceId=SourceId, DatasetId=DatasetId, StableId="market:semas:derived:"+identity,
                RegionStableId="region:kr:sig:11260", MetricCode="traditional-market-source-observation",
                RecordKey=외부데이터RecordKey.Create(SourceId,DatasetId,"region:kr:sig:11260","traditional-market-source-observation",date,dimension),
                TextValue=text, NumericValue=null, UnitCode="source-record", EvidenceAsOfUtc=date, CollectedAtUtc=collected,
                SpatialPrecisionCode="source-point-unverified", TemporalPrecisionCode="date-only",
                QualityCode="PendingHumanReview", LimitationCode="PrivateReviewOnly;NoPublication;NoRuntime;DerivedIdentity;NotCurrentOperation",
                DimensionKey=dimension, SourceVersion=Version, DataRevision=Revision, FirstSeenAtUtc=collected, LastSeenAtUtc=collected,
            });
        }
        return records.OrderBy(x => x.RecordKey, StringComparer.Ordinal).ToList();
    }
}
