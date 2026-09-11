using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace 살뜰.Services.Dispatch.Continuity;

public sealed class 화물연속배차BackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<화물연속배차Options> _options;
    private readonly ILogger<화물연속배차BackgroundService> _logger;

    public 화물연속배차BackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<화물연속배차Options> options,
        ILogger<화물연속배차BackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            if (options.Enabled && !options.ShadowMode && options.운임정책활성)
            {
                try
                {
                    await 실행Async(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "화물 연속 배차 탐색 주기가 실패했습니다.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.탐색주기초, 10, 300)), stoppingToken);
        }
    }

    private async Task 실행Async(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        var useCase = scope.ServiceProvider.GetRequiredService<I화물연속배차UseCase>();
        var driverIds = await db.화물연속배차상태.AsNoTracking()
            .Where(x => x.활성여부)
            .OrderBy(x => x.UpdatedAt)
            .Select(x => x.기사Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var driverId in driverIds)
        {
            try
            {
                await useCase.추천탐색Async(driverId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "화물 연속 배차 기사 탐색이 실패했습니다. DriverId={DriverId}", driverId);
            }
        }
    }
}
