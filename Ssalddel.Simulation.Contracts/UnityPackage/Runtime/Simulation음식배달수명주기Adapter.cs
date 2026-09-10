using System;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Contracts
{
    public static class Simulation음식배달수명주기Adapter
    {
        public static 음식배달수명주기Snapshot ToLifecycleSnapshot(
            Simulation음식배달Snapshot source,
            string sourceRevision)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.FoodOrderStableId))
                throw new ArgumentException("FoodDeliveryOrderIdentityMissing", nameof(source));
            return new 음식배달수명주기Snapshot
            {
                SourceCode = 음식배달상태원천코드.SimulationCore,
                SourceRevision = sourceRevision ?? string.Empty,
                OrderStableId = source.FoodOrderStableId,
                OrderRevision = source.Revision,
                OrderStateCode = source.StateCode,
                RestaurantStableId = source.RestaurantFacilityStableId,
                OrdererStableId = source.OrdererStableId,
                AcceptedAtUtc = null,
                SourceRefs = source.SourceStableIds?.ToArray() ?? Array.Empty<string>()
            };
        }
    }
}
