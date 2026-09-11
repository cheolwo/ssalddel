using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Filters;
using Ssalddel.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Food;

[SsalddelApiVersion(
    SsalddelProductVersion.V3_0,
    FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow,
    WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Authorize(Policy = "음식점운영자전용")]
[Route("api/v1/food-orders/restaurant/operational-activity")]
public sealed class 음식점운영배차공통Controller(
    I운영배차공통UseCase useCase) : ControllerBase
{
    [HttpGet("short-metrics")]
    public async Task<ActionResult<운영배차단기지표Dto>> 단기지표조회(
        CancellationToken cancellationToken)
    {
        var restaurantId = 음식점접근범위Resolver.음식점Id조회(User);
        if (restaurantId is null)
        {
            return Forbid();
        }

        return Ok(await useCase.단기지표조회Async(
            $"restaurant:{restaurantId.Value}",
            cancellationToken));
    }
}
