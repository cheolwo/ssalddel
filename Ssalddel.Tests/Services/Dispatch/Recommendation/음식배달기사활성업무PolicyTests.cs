using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Controllers;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 음식배달기사활성업무PolicyTests
{
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 3, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 1, false)]
    public void 기존활성업무와_새수락을_합산해_세건상한을판정한다(
        int currentActiveDeliveries,
        int requestedDeliveries,
        bool expected)
        => Assert.Equal(expected, 음식배달기사활성업무Policy.수락가능한가(
            currentActiveDeliveries,
            requestedDeliveries));

    [Fact]
    public void 활성업무상한오류는_409와_안정오류코드를응답한다()
    {
        var result = Result.Fail<object>(new Error(
                음식배달기사활성업무Policy.초과안내(2, 2))
            .WithMetadata("StatusCode", StatusCodes.Status409Conflict)
            .WithMetadata("ErrorCode", 음식배달기사활성업무Policy.LimitExceededErrorCode));
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = Assert.IsType<ObjectResult>(controller.ToActionResult(result));
        var problem = Assert.IsType<ProblemDetails>(response.Value);

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Equal(
            음식배달기사활성업무Policy.LimitExceededErrorCode,
            problem.Extensions["errorCode"]);
    }

    [Fact]
    public void 수락Service는_기사별직렬화와_DB직렬화격리안에서_상한을검사한다()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel",
            "Services",
            "Dispatch",
            "Recommendation",
            "FoodDeliveryDriverWorkService.cs"));

        Assert.Contains("ConcurrentDictionary<string, SemaphoreSlim>", source);
        Assert.Contains("IsolationLevel.Serializable", source);
        Assert.Contains("currentActiveDeliveries", source);
        Assert.Contains("음식배달기사활성업무Policy.수락가능한가", source);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }

    private sealed class TestController : ControllerBase
    {
    }
}
