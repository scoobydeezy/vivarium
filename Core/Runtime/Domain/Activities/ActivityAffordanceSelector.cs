using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    public static class ActivityAffordanceSelector
    {
        public static bool TryFindNearest(
            WorldState world,
            LocationId origin,
            AuthoredId activityDefinitionId,
            out LocationId locationId)
        {
            locationId = LocationId.None;
            SimDuration bestCost = default;
            foreach (LocationId candidate in world.Locations.Affording(activityDefinitionId))
            {
                LocationNode node = world.Locations.Get(candidate);
                if (!node.IsOccupiable || !world.TravelNetwork.TryPlanRoute(origin, candidate, out TravelPlan plan))
                    continue;
                if (!locationId.IsSet || plan.TotalCost < bestCost ||
                    (plan.TotalCost == bestCost && candidate.CompareTo(locationId) < 0))
                {
                    locationId = candidate;
                    bestCost = plan.TotalCost;
                }
            }
            return locationId.IsSet;
        }
    }
}
