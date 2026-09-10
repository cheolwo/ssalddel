using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

namespace Ssalddel.Services.Community;

public interface I지역공공통계조회UseCase
{
    Task<지역공공통계SnapshotDto> 조회Async(
        string countryCode,
        string regionLevel,
        string period,
        string? metrics,
        CancellationToken cancellationToken = default);
}

public sealed partial class 지역공공통계조회UseCase(
    PublicDataIngestionDbContext dbContext,
    TimeProvider timeProvider) : I지역공공통계조회UseCase
{
    public async Task<지역공공통계SnapshotDto> 조회Async(
        string countryCode,
        string regionLevel,
        string period,
        string? metrics,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(countryCode?.Trim(), "KR", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("countryCode는 KR만 지원합니다.", nameof(countryCode));
        var spatial = regionLevel?.Trim() switch
        {
            "Sido" => "province",
            "Sigungu" => "city-county-district",
            _ => throw new ArgumentException("regionLevel은 Sido 또는 Sigungu여야 합니다.", nameof(regionLevel)),
        };
        var requestedPeriod = string.IsNullOrWhiteSpace(period) ? "latest" : period.Trim();
        if (requestedPeriod != "latest" && !YearMonth().IsMatch(requestedPeriod))
            throw new ArgumentException("period는 latest 또는 YYYY-MM이어야 합니다.", nameof(period));
        var requestedMetrics = ParseMetrics(metrics);

        var rows = await dbContext.NormalizedRecords.AsNoTracking()
            .Include(item => item.RawSnapshot)
            .Where(item => item.SourceId == Kosis지역통계Dataset.SourceId
                           && item.DatasetId == Kosis지역통계Dataset.DatasetId
                           && item.SpatialPrecisionCode == spatial
                           && requestedMetrics.Contains(item.MetricCode)
                           && item.NumericValue != null)
            .ToArrayAsync(cancellationToken);

        if (requestedPeriod != "latest")
        {
            var month = requestedPeriod.Replace("-", "", StringComparison.Ordinal);
            var year = requestedPeriod[..4];
            rows = rows.Where(item =>
                (item.TemporalPrecisionCode == "month" && Dimension(item.DimensionKey, "period") == month)
                || (item.TemporalPrecisionCode == "year" && Dimension(item.DimensionKey, "period") == year))
                .ToArray();
        }

        var selected = rows
            .GroupBy(item => (item.RegionStableId, item.MetricCode))
            .Select(group => group
                .OrderByDescending(item => item.EvidenceAsOfUtc)
                .ThenByDescending(item => item.CollectedAtUtc)
                .First())
            .OrderBy(item => item.TextValue, StringComparer.Ordinal)
            .ThenBy(item => item.MetricCode, StringComparer.Ordinal)
            .ToArray();
        var regions = selected
            .GroupBy(item => (item.RegionStableId, item.TextValue))
            .Select(group => new 지역공공통계RegionDto(
                group.Key.RegionStableId,
                group.Key.TextValue,
                group.Select(ToMetric).OrderBy(item => item.MetricCode, StringComparer.Ordinal).ToArray()))
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();
        var canonical = string.Join('\n', selected.Select(item => string.Join('|',
            item.RecordKey, item.DataRevision, item.NumericValue, item.RawSnapshot?.ContentHashSha256)));
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16].ToLowerInvariant();
        return new 지역공공통계SnapshotDto(
            $"regional-statistics:kr:{regionLevel.ToLowerInvariant()}:{requestedPeriod.ToLowerInvariant()}",
            revision,
            timeProvider.GetUtcNow(),
            "KR",
            regionLevel,
            requestedPeriod,
            regions);
    }

    private static 지역공공통계MetricDto ToMetric(Ssalddel.Domain.PublicData.외부데이터정규화Record item)
        => new(
            item.MetricCode,
            item.NumericValue!.Value,
            item.UnitCode,
            item.SourceId,
            item.DatasetId,
            Dimension(item.DimensionKey, "table"),
            Dimension(item.DimensionKey, "item"),
            Dimension(item.DimensionKey, "external-region-code"),
            Dimension(item.DimensionKey, "region-code-version"),
            Dimension(item.DimensionKey, "period"),
            item.EvidenceAsOfUtc,
            item.CollectedAtUtc,
            Dimension(item.DimensionKey, "last-changed"),
            item.SpatialPrecisionCode,
            item.TemporalPrecisionCode,
            item.QualityCode,
            item.LimitationCode,
            item.RawSnapshot?.ContentHashSha256 ?? string.Empty);

    private static HashSet<string> ParseMetrics(string? metrics)
    {
        var values = string.IsNullOrWhiteSpace(metrics)
            ? Kosis지역통계Dataset.MetricCodes
            : metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
        if (values.Count == 0 || values.Any(value => !Kosis지역통계Dataset.MetricCodes.Contains(value)))
            throw new ArgumentException("지원하지 않는 metrics 값이 있습니다.", nameof(metrics));
        return values.ToHashSet(StringComparer.Ordinal);
    }

    private static string Dimension(string dimensionKey, string key)
    {
        var prefix = key + "=";
        var value = dimensionKey.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal));
        return value is null ? string.Empty : Uri.UnescapeDataString(value[prefix.Length..]);
    }

    [GeneratedRegex("^[0-9]{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex YearMonth();
}
