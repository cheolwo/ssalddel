namespace Ssalddel.Contracts.Common.WorldProjection;

public static class 지역공공통계Routes
{
    public const string Api = "api/v1/community/world-map/regional-statistics";
}

public sealed record 지역공공통계SnapshotDto(
    string SnapshotStableId,
    string Revision,
    DateTimeOffset GeneratedAtUtc,
    string CountryCode,
    string RegionLevel,
    string RequestedPeriod,
    IReadOnlyList<지역공공통계RegionDto> Regions);

public sealed record 지역공공통계RegionDto(
    string RegionStableId,
    string DisplayName,
    IReadOnlyList<지역공공통계MetricDto> Metrics);

public sealed record 지역공공통계MetricDto(
    string MetricCode,
    decimal Value,
    string UnitCode,
    string SourceId,
    string DatasetId,
    string TableId,
    string ItemId,
    string ExternalRegionCode,
    string RegionCodeVersion,
    string SourcePeriod,
    DateTimeOffset EvidenceAsOfUtc,
    DateTimeOffset CollectedAtUtc,
    string SourceUpdatedAt,
    string SpatialPrecisionCode,
    string TemporalPrecisionCode,
    string QualityCode,
    string Limitations,
    string RawContentHashSha256);
