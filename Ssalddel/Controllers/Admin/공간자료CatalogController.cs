using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Services.WorldProjection.SpatialCatalog;

namespace Ssalddel.Controllers.Admin;

[ApiController]
[Route(공간자료CatalogCodes.Route)]
[Authorize(Policy=공간자료CatalogCodes.Policy)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[SsalddelApiVersion(SsalddelProductVersion.V0_0)]
[SsalddelCodeMetadata(공간자료CatalogCodes.Feature,SsalddelCodeLayer.Api,
    "검증된 공간 문서 묶음과 구성요소·원문 관계를 관리자에게 읽기 전용 제공한다.",
    StepKey="query-api",FlowOrder=40,ExecutionStage=SsalddelCodeExecutionStage.Query,
    ReadsFrom=SsalddelCodeDataScope.DerivedWorld,Boundary="공개 배포·사람 검토 승인·게임 상태 변경·Mongo 직접 접속을 허용하지 않는다.")]
public sealed class 공간자료CatalogController(공간자료CatalogService service) : ControllerBase
{
    [HttpGet("snapshots")]
    public Task<IActionResult> 판본목록([FromQuery] 공간자료Query query,CancellationToken ct) => Guard(()=>service.SnapshotsAsync(query,ct),ct);
    [HttpGet("documents")]
    public Task<IActionResult> 문서목록([FromQuery] 공간자료Query query,CancellationToken ct) => Guard(()=>service.DocumentsAsync(query,ct),ct);
    [HttpGet("documents/{documentId}")]
    public Task<IActionResult> 문서(string documentId,[FromQuery] string bundleId,[FromQuery] bool includeSensitive,CancellationToken ct)
        => Guard(()=>service.DocumentAsync(bundleId,documentId,includeSensitive,ct),ct);
    [HttpGet("elements")]
    public Task<IActionResult> 구성요소([FromQuery] 공간자료Query query,CancellationToken ct) => Guard(()=>service.ElementsAsync(query,ct),ct);
    [HttpGet("relations")]
    public Task<IActionResult> 관계([FromQuery] 공간자료Query query,CancellationToken ct) => Guard(()=>service.RelationsAsync(query,ct),ct);
    [HttpGet("presentation")]
    public Task<IActionResult> 표현사본([FromQuery] 공간자료Query query,CancellationToken ct) => Guard(()=>service.PresentationAsync(query,ct),ct);

    private async Task<IActionResult> Guard<T>(Func<Task<T>> read,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { return Ok(await read()); }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(ArgumentException) { return Problem(statusCode:400,title:"SpatialQueryInvalid"); }
        catch(KeyNotFoundException) { return Problem(statusCode:404,title:"SpatialSnapshotOrDocumentUnavailable"); }
        catch(Exception) { return Problem(statusCode:503,title:"SpatialCatalogUnavailable"); }
    }
}
