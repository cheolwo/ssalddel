using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Community;

namespace Ssalddel.Controllers.Common;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.CommunityWorldMapObservation,
    SsalddelCodeLayer.Api,
    "KOSIS 시도·시군구 인구·생활경제 공표값의 읽기 전용 조회",
    ContractType = typeof(I지역공공통계조회UseCase),
    FlowOrder = 31,
    Effects = SsalddelCodeEffect.None,
    Boundary = "개인·주소·주문 자료를 노출하지 않고 외부 API 호출이나 Simulation 상태 변경을 만들지 않습니다.")]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelApiGrowthTrack(SsalddelApiGrowthTrack.Community)]
[ApiController]
[AllowAnonymous]
[Route(지역공공통계Routes.Api)]
[SsalddelApiContractName("RegionalPublicStatisticsController")]
public sealed class 지역공공통계WorldController(I지역공공통계조회UseCase useCase) : ControllerBase
{
    [HttpGet]
    [SsalddelApiContractName("GetRegionalStatistics")]
    public async Task<ActionResult<지역공공통계SnapshotDto>> 조회(
        [FromQuery] string countryCode = "KR",
        [FromQuery] string regionLevel = "Sido",
        [FromQuery] string period = "latest",
        [FromQuery] string? metrics = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await useCase.조회Async(countryCode, regionLevel, period, metrics, cancellationToken));
        }
        catch (ArgumentException error)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "지역 통계 조회 조건을 확인해 주세요",
                Detail = error.Message,
            });
        }
    }
}
