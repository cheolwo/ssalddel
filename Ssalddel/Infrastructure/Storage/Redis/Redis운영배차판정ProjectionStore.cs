using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using StackExchange.Redis;
using 살뜰.Services.Dispatch.Common;

namespace 살뜰.Infrastructure.Storage.Redis;

public sealed class Redis운영배차판정ProjectionStore : I운영배차판정ProjectionStore
{
    private const string AvailabilityKeyPrefix = "ssalddel:operational-dispatch:availability:";
    private const string MetricsKeyPrefix = "ssalddel:operational-dispatch:short-metrics:";
    private static readonly TimeSpan AvailabilityTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan MetricsTtl = TimeSpan.FromDays(4);
    private readonly IDatabase _database;

    public Redis운영배차판정ProjectionStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async ValueTask<운영배차수신상태Dto?> 수신상태조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
        => await 조회Async<운영배차수신상태Dto>(
            AvailabilityKeyPrefix + Key(주체Id),
            cancellationToken);

    public async ValueTask 수신상태저장Async(
        운영배차수신상태Dto 상태,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(상태);
        await 저장Async(
            AvailabilityKeyPrefix + Key(상태.주체Id),
            상태,
            AvailabilityTtl,
            cancellationToken);
    }

    public async ValueTask<운영배차단기지표Dto?> 단기지표조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
        => await 조회Async<운영배차단기지표Dto>(
            MetricsKeyPrefix + Key(주체Id),
            cancellationToken);

    public async ValueTask 단기지표저장Async(
        string 주체Id,
        운영배차단기지표Dto 지표,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(지표);
        await 저장Async(
            MetricsKeyPrefix + Key(주체Id),
            지표,
            MetricsTtl,
            cancellationToken);
    }

    private async ValueTask<T?> 조회Async<T>(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(key).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private async ValueTask 저장Async<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(key, JsonSerializer.Serialize(value), ttl).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string Key(string 주체Id)
    {
        var clean = 주체Id?.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? throw new ArgumentException("배차 판정 주체 ID가 필요합니다.", nameof(주체Id))
            : Uri.EscapeDataString(clean);
    }
}
