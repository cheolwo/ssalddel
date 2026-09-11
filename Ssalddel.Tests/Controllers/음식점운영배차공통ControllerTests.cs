using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Food;
using Ssalddel.Security;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Tests.Controllers;

public sealed class 음식점운영배차공통ControllerTests
{
    [Fact]
    public void 단기지표는_음식점정책과전용경로를사용한다()
    {
        var type = typeof(음식점운영배차공통Controller);
        var method = type.GetMethod(nameof(음식점운영배차공통Controller.단기지표조회));

        Assert.Equal(
            "api/v1/food-orders/restaurant/operational-activity",
            type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("음식점운영자전용", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("short-metrics", method?.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public async Task 음식점클레임을_내부주체Id로변환해_단기지표를조회한다()
    {
        var useCase = new RecordingUseCase();
        var controller = new 음식점운영배차공통Controller(useCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(음식점접근ClaimTypes.음식점Id, "42")],
                        "test"))
                }
            }
        };

        var result = await controller.단기지표조회(default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("restaurant:42", useCase.SubjectId);
    }

    private sealed class RecordingUseCase : I운영배차공통UseCase
    {
        public string? SubjectId { get; private set; }

        public Task<운영배차단기지표Dto> 단기지표조회Async(
            string 주체Id,
            CancellationToken cancellationToken = default)
        {
            SubjectId = 주체Id;
            return Task.FromResult(new 운영배차단기지표Dto());
        }

        public Task<운영배차수신상태Dto> 수신상태조회Async(
            string 주체Id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<운영배차수신상태Dto> 기사의사변경Async(
            string 기사Id,
            운영배차수신의사변경요청 요청,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<운영배차수신상태Dto> 서버실효상태변경Async(
            string 주체Id,
            Guid 요청Id,
            string 실효상태Code,
            string 실효사유Code,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> 활동사건기록Async(
            운영배차활동사건Dto 사건,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
