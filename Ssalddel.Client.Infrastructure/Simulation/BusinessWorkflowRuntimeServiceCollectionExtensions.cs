using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.BusinessWorkflow;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Client.Infrastructure.Simulation;

/// <summary>
/// 웹·모바일의 DI와 개발용 로컬 호스트가 같은 업무 흐름 포트를 선택하도록 한다.
/// Local과 Remote를 동시에 등록하거나 실패 시 자동 전환하지 않는다.
/// </summary>
public static class BusinessWorkflowRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteBusinessWorkflowRuntime(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureNotRegistered(services);

        services.AddScoped<IBusinessWorkflowRuntime>(provider =>
            RemoteBusinessWorkflowRuntimeFactory.Create(
                provider.GetRequiredService<HttpClient>()));
        return AddPorts(services);
    }

    public static IServiceCollection AddLocalBusinessWorkflowRuntime(
        this IServiceCollection services,
        Func<IServiceProvider, LocalSimulationRuntime> runtimeFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runtimeFactory);
        EnsureNotRegistered(services);
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(LocalSimulationRuntime)))
            throw new InvalidOperationException(
                "LocalSimulationRuntime이 이미 등록되어 있습니다.");

        services.Add(ServiceDescriptor.Scoped(
            typeof(LocalSimulationRuntime),
            provider => runtimeFactory(provider)));
        services.AddScoped<IBusinessWorkflowRuntime>(provider =>
            LocalBusinessWorkflowRuntimeFactory.Create(
                provider.GetRequiredService<LocalSimulationRuntime>(),
                BusinessWorkflowRuleEngine.기본));
        return AddPorts(services);
    }

    private static IServiceCollection AddPorts(IServiceCollection services)
    {
        services.AddScoped<I주문업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Orders);
        services.AddScoped<I음식점업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Restaurants);
        services.AddScoped<I배차업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Dispatch);
        services.AddScoped<I배송업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Delivery);
        services.AddScoped<I창고업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Warehouse);
        return services;
    }

    private static void EnsureNotRegistered(IServiceCollection services)
    {
        var serviceTypes = new[]
        {
            typeof(IBusinessWorkflowRuntime),
            typeof(I주문업무Runtime),
            typeof(I음식점업무Runtime),
            typeof(I배차업무Runtime),
            typeof(I배송업무Runtime),
            typeof(I창고업무Runtime),
        };
        var duplicate = services.FirstOrDefault(descriptor =>
            serviceTypes.Contains(descriptor.ServiceType));
        if (duplicate != null)
            throw new InvalidOperationException(
                $"업무 흐름 Runtime 서비스가 이미 등록되어 있습니다: "
                + duplicate.ServiceType.Name);
    }
}
