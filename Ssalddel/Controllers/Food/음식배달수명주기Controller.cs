using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Food;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Controllers.Food;

[SsalddelApiVersion(SsalddelProductVersion.V3_0, FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow, WorkflowKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[RequireVersionFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
[ApiController]
[Authorize]
[Route("api/v1/food-orders/{orderNo}/lifecycle")]
public sealed class 음식배달수명주기Controller(I음식배달수명주기조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<음식배달수명주기Snapshot>> 상세(
        string orderNo,
        CancellationToken cancellationToken)
    {
        var result = await useCase.상세Async(orderNo, 현재사용자Id(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private string? 현재사용자Id()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.Identity?.Name;
}
