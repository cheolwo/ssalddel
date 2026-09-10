using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.BusinessWorkflow;
using Ssalddel.Client.Infrastructure.Simulation;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Tests;

public sealed class BusinessWorkflowRuntimeCompositionTests
{
    [Fact]
    public void 공통계약Assembly는_Http_Di_Unity_Mongo에의존하지않는다()
    {
        var references = typeof(IBusinessWorkflowRuntime).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", references);
        Assert.DoesNotContain(references,
            name => name.StartsWith("Microsoft.Extensions.DependencyInjection",
                StringComparison.Ordinal));
        Assert.DoesNotContain(references,
            name => name.StartsWith("UnityEngine", StringComparison.Ordinal));
        Assert.DoesNotContain(references,
            name => name.StartsWith("MongoDB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 다섯업무Port는_같은Facade와하위원장을공유한다()
    {
        var food = new RecordingFoodRuntime();
        var logistics = new RecordingLogisticsRuntime();
        var runtime = new BusinessWorkflowRuntime(
            food,
            logistics,
            WorkflowRules.BusinessWorkflowRuleEngine.기본,
            LocalDescriptor());

        Assert.Same(runtime, runtime.Orders);
        Assert.Same(runtime, runtime.Restaurants);
        Assert.Same(runtime, runtime.Dispatch);
        Assert.Same(runtime, runtime.Delivery);
        Assert.Same(runtime, runtime.Warehouse);

        await runtime.Orders.PreviewFoodDeliveryAsync(
            "session", new Simulation음식배달PreviewRequest());
        await runtime.Delivery.ConfirmFoodDeliveryAsync(
            "session", new Simulation음식배달ConfirmRequest());
        await runtime.Restaurants.ConfirmRestaurantResponseAsync(
            "session", new Simulation음식점응답Request());
        await runtime.Dispatch.PreviewFreightDispatchAsync(
            "session", new SimulationFreightDispatchPreviewRequest());
        await runtime.Warehouse.PreviewLogisticsMovementAsync(
            "session", new SimulationLogisticsMovementPreviewRequest());

        Assert.Equal(3, food.CallCount);
        Assert.Equal(2, logistics.CallCount);
        var descriptor = runtime.Descriptor;
        descriptor.ModeCode = BusinessWorkflowRuntimeModeCodes.RemoteHost;
        descriptor.ClassificationMetadata!.PrimaryCode = "MUTATED";
        Assert.Equal(BusinessWorkflowRuntimeModeCodes.LocalProcess,
            runtime.Descriptor.ModeCode);
        Assert.NotEqual("MUTATED",
            runtime.Descriptor.ClassificationMetadata!.PrimaryCode);
        Assert.False(runtime.Descriptor.ClassificationMetadata.IsExecutionAuthority);
    }

    [Fact]
    public async Task Remote조립은_기존SimulationHttp경로만사용한다()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://simulation.invalid/"),
        };
        var runtime = Infrastructure.RemoteBusinessWorkflowRuntimeFactory.Create(client);

        await runtime.Orders.PreviewFoodDeliveryAsync(
            "session:remote", new Simulation음식배달PreviewRequest());
        await runtime.Restaurants.ConfirmRestaurantResponseAsync(
            "session:remote", new Simulation음식점응답Request());
        await runtime.Dispatch.ConfirmFreightDispatchAsync(
            "session:remote", new SimulationFreightDispatchConfirmRequest());
        await runtime.Warehouse.ConfirmLogisticsMovementAsync(
            "session:remote", new SimulationLogisticsMovementConfirmRequest());

        Assert.Equal(BusinessWorkflowRuntimeModeCodes.RemoteHost,
            runtime.Descriptor.ModeCode);
        Assert.True(runtime.Descriptor.RequiresNetwork);
        Assert.Equal(
            [
                "POST api/simulation/v1/sessions/session%3Aremote/food-delivery-previews",
                "POST api/simulation/v1/sessions/session%3Aremote/restaurant-responses/confirm",
                "POST api/simulation/v1/sessions/session%3Aremote/freight-dispatches/confirm",
                "POST api/simulation/v1/sessions/session%3Aremote/logistics-movements/confirm",
            ],
            handler.Requests);
    }

    [Fact]
    public void Web과MobileDi는_한Scope에서같은Facade를역할별로해석한다()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HttpClient(new RecordingHandler())
        {
            BaseAddress = new Uri("https://simulation.invalid/"),
        });
        services.AddRemoteBusinessWorkflowRuntime();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider
            .GetRequiredService<IBusinessWorkflowRuntime>();

        Assert.Same(facade.Orders, scope.ServiceProvider
            .GetRequiredService<I주문업무Runtime>());
        Assert.Same(facade.Restaurants, scope.ServiceProvider
            .GetRequiredService<I음식점업무Runtime>());
        Assert.Same(facade.Dispatch, scope.ServiceProvider
            .GetRequiredService<I배차업무Runtime>());
        Assert.Same(facade.Delivery, scope.ServiceProvider
            .GetRequiredService<I배송업무Runtime>());
        Assert.Same(facade.Warehouse, scope.ServiceProvider
            .GetRequiredService<I창고업무Runtime>());
    }

    [Fact]
    public void LocalDi는_한LocalSimulationRuntime을다섯역할에공유한다()
    {
        var services = new ServiceCollection();
        services.AddLocalBusinessWorkflowRuntime(_ => new Application.LocalSimulationRuntime(
            new Infrastructure.InMemory경영SimulationSessionStore(),
            new Infrastructure.InMemorySimulationSessionSaveStore(),
            new 사용하지않는LocalSaveSlotStore()));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider
            .GetRequiredService<IBusinessWorkflowRuntime>();

        Assert.Equal(BusinessWorkflowRuntimeModeCodes.LocalProcess,
            facade.Descriptor.ModeCode);
        Assert.False(facade.Descriptor.RequiresNetwork);
        Assert.Same(facade, facade.Orders);
        Assert.Same(facade, facade.Restaurants);
        Assert.Same(facade, facade.Dispatch);
        Assert.Same(facade, facade.Delivery);
        Assert.Same(facade, facade.Warehouse);
    }

    [Fact]
    public void Local과Remote를같이등록하면_조용히혼합하지않고거부한다()
    {
        var services = new ServiceCollection();
        services.AddRemoteBusinessWorkflowRuntime();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddLocalBusinessWorkflowRuntime(_ =>
                throw new InvalidOperationException("FactoryMustNotRun")));

        Assert.Contains("이미 등록", error.Message);
    }

    [Fact]
    public async Task Remote실패는_Local실행으로자동전환하지않는다()
    {
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://simulation.invalid/"),
        };
        var runtime = Infrastructure.RemoteBusinessWorkflowRuntimeFactory.Create(client);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            runtime.Orders.PreviewFoodDeliveryAsync(
                "session:no-fallback",
                new Simulation음식배달PreviewRequest()).AsTask());

        Assert.Single(handler.Requests);
    }

    [Fact]
    public void 실행위치와Network경계가다르면_조립전에거부한다()
    {
        var descriptor = LocalDescriptor();
        descriptor.RequiresNetwork = true;

        var error = Assert.Throws<ArgumentException>(() =>
            new BusinessWorkflowRuntime(
                new RecordingFoodRuntime(),
                new RecordingLogisticsRuntime(),
                WorkflowRules.BusinessWorkflowRuleEngine.기본,
                descriptor));

        Assert.Contains("네트워크 경계", error.Message);
    }

    private static BusinessWorkflowRuntimeDescriptor LocalDescriptor()
        => new()
        {
            RuntimeStableId = "business-workflow-runtime:test-local",
            ModeCode = BusinessWorkflowRuntimeModeCodes.LocalProcess,
            RequiresNetwork = false,
            ContractRevision = Application.LocalBusinessWorkflowRuntimeFactory.ContractRevision,
            ClassificationMetadata = WorkflowRules.BusinessWorkflowRuleEngine.기본
                .Engine정보조회().ClassificationMetadata,
        };

    private sealed class RecordingFoodRuntime : ISimulationFoodOrderRuntime
    {
        public int CallCount { get; private set; }

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmRestaurantResponseAsync(string sessionStableId,
                Simulation음식점응답Request request,
                CancellationToken token = default)
        {
            CallCount++;
            return ValueTask.FromResult(new 경영SimulationSessionSnapshot());
        }

        public ValueTask<Simulation음식배달PreviewSnapshot>
            PreviewFoodDeliveryAsync(string sessionStableId,
                Simulation음식배달PreviewRequest request,
                CancellationToken token = default)
        {
            CallCount++;
            return ValueTask.FromResult(new Simulation음식배달PreviewSnapshot());
        }

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFoodDeliveryAsync(string sessionStableId,
                Simulation음식배달ConfirmRequest request,
                CancellationToken token = default)
        {
            CallCount++;
            return ValueTask.FromResult(new 경영SimulationSessionSnapshot());
        }

        public ValueTask<SimulationDecisionPreviewSnapshot>
            PreviewFoodDeliveryReceiptAsync(string sessionStableId,
                Simulation음식배달수령PreviewRequest request,
                CancellationToken token = default)
        {
            CallCount++;
            return ValueTask.FromResult(new SimulationDecisionPreviewSnapshot());
        }

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFoodDeliveryReceiptAsync(string sessionStableId,
                Simulation음식배달수령ConfirmRequest request,
                CancellationToken token = default)
        {
            CallCount++;
            return ValueTask.FromResult(new 경영SimulationSessionSnapshot());
        }
    }

    private sealed class RecordingLogisticsRuntime : ISimulationLogisticsRuntime
    {
        public int CallCount { get; private set; }

        public ValueTask<SimulationLogisticsMovementPreviewSnapshot>
            PreviewLogisticsMovementAsync(string sessionStableId,
                SimulationLogisticsMovementPreviewRequest request,
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new SimulationLogisticsMovementPreviewSnapshot());
        }

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmLogisticsMovementAsync(string sessionStableId,
                SimulationLogisticsMovementConfirmRequest request,
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new 경영SimulationSessionSnapshot());
        }

        public ValueTask<SimulationFreightDispatchPreviewSnapshot>
            PreviewFreightDispatchAsync(string sessionStableId,
                SimulationFreightDispatchPreviewRequest request,
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new SimulationFreightDispatchPreviewSnapshot());
        }

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFreightDispatchAsync(string sessionStableId,
                SimulationFreightDispatchConfirmRequest request,
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new 경영SimulationSessionSnapshot());
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;

        public RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
            => this.statusCode = statusCode;

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.Method.Method + " "
                + request.RequestUri!.PathAndQuery.TrimStart('/'));
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class 사용하지않는LocalSaveSlotStore
        : ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId, SimulationSessionSavePackage package)
            => throw new InvalidOperationException("LocalSaveNotExpected");

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
            => throw new InvalidOperationException("LocalSaveNotExpected");
    }
}
