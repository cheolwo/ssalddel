using System.Collections.Concurrent;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Continuity;

namespace Ssalddel.Infrastructure.Storage.Memory;

public sealed class InMemory화물연속배차ProjectionStore : I화물연속배차ProjectionStore
{
    private readonly ConcurrentDictionary<string, string> _items = new(StringComparer.Ordinal);

    public ValueTask<화물연속배차상태Dto?> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(기사Id);
        return ValueTask.FromResult(_items.TryGetValue(key, out var json)
            ? JsonSerializer.Deserialize<화물연속배차상태Dto>(json)
            : null);
    }

    public ValueTask 저장Async(화물연속배차상태Dto 상태, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(상태);
        cancellationToken.ThrowIfCancellationRequested();
        _items[Key(상태.기사Id)] = JsonSerializer.Serialize(상태);
        return ValueTask.CompletedTask;
    }

    private static string Key(string 기사Id)
        => string.IsNullOrWhiteSpace(기사Id)
            ? throw new ArgumentException("기사 ID가 필요합니다.", nameof(기사Id))
            : 기사Id.Trim();
}
