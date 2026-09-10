using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

public sealed class 음식배달수명주기표현Tests
{
    [Fact]
    public void Simulation사본과Unity표현은_같은상태코드를소비한다()
    {
        var source = new Simulation음식배달Snapshot
        {
            FoodOrderStableId = "sim-food-1", Revision = 4, StateCode = "픽업완료",
            RestaurantFacilityStableId = "restaurant-1", OrdererStableId = "orderer-1"
        };
        var snapshot = Simulation음식배달수명주기Adapter.ToLifecycleSnapshot(source, "simulation.r1");
        var presentation = new 음식배달수명주기표현(snapshot);

        Assert.Equal(음식배달상태원천코드.SimulationCore, snapshot.SourceCode);
        Assert.Equal(4, presentation.StepIndex);
        Assert.Contains("전달지", presentation.Label);
    }
}
