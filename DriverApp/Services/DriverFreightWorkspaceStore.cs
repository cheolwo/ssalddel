using Ssalddel.Contracts.Driver.Transport;

namespace DriverApp.Services;

public interface IDriverFreightWorkspaceStore
{
    기사화물운송작업공간응답 Current { get; }
    Task<기사화물운송작업공간응답> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 실제 기사 업무 화면이 sample 자료와 분리해 서버의 화물 작업공간을 보관한다.
/// </summary>
public sealed class DriverFreightWorkspaceStore : IDriverFreightWorkspaceStore
{
    private readonly IDriverTransportApiService _api;

    public DriverFreightWorkspaceStore(IDriverTransportApiService api)
    {
        _api = api;
    }

    public 기사화물운송작업공간응답 Current { get; private set; } = new();

    public async Task<기사화물운송작업공간응답> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Current = await _api.작업공간조회Async(cancellationToken) ?? new 기사화물운송작업공간응답();
        return Current;
    }
}
