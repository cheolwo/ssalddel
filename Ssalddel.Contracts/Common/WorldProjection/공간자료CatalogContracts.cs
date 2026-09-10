using System.Text.Json;

namespace Ssalddel.Contracts.Common.WorldProjection;

public static class 공간자료CatalogCodes
{
    public const string Route = "api/v1/admin/spatial-catalog";
    public const string Policy = "서버관리자전용";
    public const string AdapterVersion = "spatial-catalog.r3";
    public const string Feature = "spatial-json-catalog";
}

// Mongo/BSON 형식은 서버 안에만 둔다. 모든 소비자는 같은 묶음 ID와 일반 JSON을 읽는다.
public sealed record 공간자료Page(string BundleId, int Total, int Skip, int Take,
    int? NextSkip, bool PrivateReviewOnly, IReadOnlyList<JsonElement> Items);

public sealed record 공간자료Detail(string BundleId, bool SensitiveIncluded,
    bool GameStateConnected, JsonElement Document);

public sealed class 공간자료Query
{
    public string? BundleId { get; set; }
    public string? Dataset { get; set; }
    public string? AreaStableId { get; set; }
    public string? Kind { get; set; }
    public string? Revision { get; set; }
    public string? ReviewState { get; set; }
    public string? DocumentId { get; set; }
    public string? StableId { get; set; }
    public string? Layer { get; set; }
    public string? Tile { get; set; }
    public string? RelationKey { get; set; }
    public string Direction { get; set; } = "both";
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}
