using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using 살뜰.Services.Dispatch.Queue;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Development.FoodObserver;
using Ssalddel.Services.Food;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식배달관찰검증Tests
{
    [Fact]
    public void 검증_운행입력은_기존_업무_Validator를_통과한다()
    {
        var input = 음식배달관찰검증Runner.운행시작입력();
        var command = new Ssalddel.Application.Driver.Work.운행시작Command("synthetic-driver",
            input.시작모드, null, input.시작위치, null, null, null, null, false, null, null, false, false);
        Assert.True(new Ssalddel.Application.Driver.Work.운행시작CommandValidator().Validate(command).IsValid);
        Assert.False(input.커뮤니티운행공개);
    }

    [Fact]
    public void 기본검증시간은_5분이며_세계수명과_분리된다()
        => Assert.Equal(300, new 음식배달관찰검증Options().DurationSeconds);

    [Theory]
    [InlineData("Operational", "ssalddel_food_observer", "Development", 300)]
    [InlineData("Simulation", "ssalddel_dev", "Development", 300)]
    [InlineData("Simulation", "ssalddel_food_observer", "Production", 300)]
    [InlineData("Simulation", "ssalddel_food_observer", "Development", 1800)]
    public void 안전경계_밖에서는_시작하지_않는다(string mode, string database, string environment, int seconds)
        => Assert.Throws<InvalidOperationException>(() => Options(seconds).Validate(Configuration(mode, database), new EnvironmentStub(environment)));

    [Fact]
    public void 격리된_Simulation_호스트만_허용한다()
        => Options(300).Validate(Configuration("Simulation", "ssalddel_food_observer"), new EnvironmentStub("Development"));

    [Fact]
    public void 기사배정_후_조리준비는_배정을_취소하지_않는다()
    {
        var result = 음식점주문진행Policy.판정(음식주문상태코드.기사배정,
            new 음식점주문진행변경요청 { 작업 = 음식점주문진행작업코드.픽업준비 });
        Assert.Equal(음식주문상태코드.기사배정, result.다음상태);
        Assert.Equal(0, result.조리예상분);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문대기)]
    [InlineData(음식주문상태코드.픽업완료)]
    [InlineData(음식주문상태코드.전달완료)]
    public void 조리와_무관한_상태는_픽업준비로_되돌리지_않는다(string status)
        => Assert.Throws<InvalidOperationException>(() => 음식점주문진행Policy.판정(status,
            new 음식점주문진행변경요청 { 작업 = 음식점주문진행작업코드.픽업준비 }));

    [Fact]
    public async Task 좌표표본은_알려지지_않은_주소를_성공으로_위장하지_않는다()
    {
        var provider = new 검증표본좌표Service();
        var known = await provider.주소정보조회Async(검증표본좌표Service.CustomerAddress);
        Assert.Equal(127.087m, known!.경도);
        await Assert.ThrowsAsync<ArgumentException>(() => provider.주소정보조회Async("임의 주소"));
    }

    private static 음식배달관찰검증Options Options(int seconds) => new()
        { Enabled = true, AccessKey = new('x', 32), AccountPassword = new('x', 32), DurationSeconds = seconds };
    private static IConfiguration Configuration(string mode, string database) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["SsalddelExecution:Mode"] = mode,
        ["DOTNET_RUNNING_IN_CONTAINER"] = "true",
        ["ConnectionStrings:DefaultConnection"] = $"server=mysql;database={database};user=observer;",
        ["MongoDb:Database"] = "ssalddel_food_observer",
        ["MongoDb:ConnectionString"] = "mongodb://mongo:27017"
    }).Build();
    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
