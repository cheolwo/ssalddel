using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using StackExchange.Redis;
using 살뜰.Services.Dispatch.Continuity;

namespace 살뜰.Infrastructure.Storage.Redis;

public sealed class Redis화물연속배차ProjectionStore : I화물연속배차ProjectionStore
{
    private const string KeyPrefix = "ssalddel:freight-continuity:";
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(2);
    private readonly IDatabase _database;

    public Redis화물연속배차ProjectionStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async ValueTask<화물연속배차상태Dto?> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(KeyPrefix + Key(기사Id)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<화물연속배차상태Dto>(value.ToString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask 저장Async(화물연속배차상태Dto 상태, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(상태);
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(
            KeyPrefix + Key(상태.기사Id),
            JsonSerializer.Serialize(상태),
            Ttl).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string Key(string 기사Id)
        => string.IsNullOrWhiteSpace(기사Id)
            ? throw new ArgumentException("기사 ID가 필요합니다.", nameof(기사Id))
            : Uri.EscapeDataString(기사Id.Trim());
}
