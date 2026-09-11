using System.Collections.Concurrent;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Infrastructure.Storage.Memory;

public sealed class InMemory운영배차판정ProjectionStore : I운영배차판정ProjectionStore
{
    private readonly ConcurrentDictionary<string, string> _수신상태 = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _단기지표 = new(StringComparer.Ordinal);

    public ValueTask<운영배차수신상태Dto?> 수신상태조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(조회<운영배차수신상태Dto>(_수신상태, 주체Id));
    }

    public ValueTask 수신상태저장Async(
        운영배차수신상태Dto 상태,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(상태);
        cancellationToken.ThrowIfCancellationRequested();
        _수신상태[Key(상태.주체Id)] = JsonSerializer.Serialize(상태);
        return ValueTask.CompletedTask;
    }

    public ValueTask<운영배차단기지표Dto?> 단기지표조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(조회<운영배차단기지표Dto>(_단기지표, 주체Id));
    }

    public ValueTask 단기지표저장Async(
        string 주체Id,
        운영배차단기지표Dto 지표,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(지표);
        cancellationToken.ThrowIfCancellationRequested();
        _단기지표[Key(주체Id)] = JsonSerializer.Serialize(지표);
        return ValueTask.CompletedTask;
    }

    private static T? 조회<T>(ConcurrentDictionary<string, string> source, string 주체Id)
        => source.TryGetValue(Key(주체Id), out var json)
            ? JsonSerializer.Deserialize<T>(json)
            : default;

    private static string Key(string 주체Id)
    {
        var clean = 주체Id?.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? throw new ArgumentException("배차 판정 주체 ID가 필요합니다.", nameof(주체Id))
            : clean;
    }
}
