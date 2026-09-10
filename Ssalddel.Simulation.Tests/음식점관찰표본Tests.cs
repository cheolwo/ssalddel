using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "음식점 관찰 입력과 Presenter를 실제 로컬 Core·저장 경계에 연결해 검증한다.",
    Boundary = ".NET 시험이며 실제 Unity Scene·입력·Game View 증거가 아니다.")]
public sealed class 음식점관찰표본Tests
{
    [Fact]
    public async Task 명시주문과수동Tick만_접수조리상태를바꾸고_저장뒤자동진행하지않는다()
    {
        var path = Path.Combine(Path.GetTempPath(), "restaurant-observer-tests", Guid.NewGuid().ToString("N"));
        using var runtime = Create(path);
        var session = await runtime.Sessions.CreateAsync(음식점관찰표본.Create(Guid.NewGuid()));
        var id = session.SessionStableId;
        var card = new 음식점정책카드Presenter(runtime, id, 음식점관찰표본.FacilityId);
        await card.조회Async();
        Assert.Empty(card.주문목록);
        Assert.Equal(0, card.현재Tick);
        Assert.Equal(1, card.조리자리수);
        for (var i = 0; i < 3; i++)
        {
            await runtime.ConfirmFoodDeliveryAsync(id, 음식점관찰표본.주문("order:" + i, card.판본, "food-order:" + i));
            await card.조회Async();
        }
        Assert.All(card.주문목록, row => Assert.Equal("접수 대기", row.표시상태));
        await Tick();
        Assert.Equal(2, card.주문목록.Count(row => row.표시상태 == "조리 대기"));
        Assert.Single(card.주문목록, row => row.표시상태 == "조리 배정·시작 대기");
        await Tick();
        Assert.Single(card.주문목록, row => row.표시상태 == "조리중");
        await card.적용Async("pause", card.판본, 1, 5, false);
        await Tick();
        Assert.Single(card.주문목록, row => row.표시상태 == "픽업 준비");
        Assert.Equal(2, card.주문목록.Count(row => row.표시상태 == "조리 대기"));
        var saved = await runtime.Sessions.SaveSlotAsync(id, new SimulationLocalSaveSlotRequest
        { SlotStableId = 음식점관찰표본.SlotId, ExpectedRevision = card.판본 });
        using var restored = Create(path);
        var loaded = await restored.Sessions.LoadSlotAsync(음식점관찰표본.SlotId);
        Assert.Equal(saved.SavedWorldTick, loaded.Restore.Session.CurrentTick);
        Assert.Equal(3, loaded.Restore.Session.FoodDeliveries.Length);
        var after = new 음식점정책카드Presenter(restored, loaded.Restore.Session.SessionStableId, 음식점관찰표본.FacilityId);
        await after.조회Async(); await after.조회Async();
        Assert.Equal(card.판본, after.판본);
        Assert.Equal(card.현재Tick, after.현재Tick);
        Assert.Equal(card.주문목록.Select(x => x.표시상태), after.주문목록.Select(x => x.표시상태));
        await after.적용Async("resume", after.판본, 2, 2, true);
        await restored.Sessions.AdvanceWorldTickAsync(loaded.Restore.Session.SessionStableId,
            new 경영SimulationTick진행Request { CommandId = "resume-tick", ExpectedRevision = after.판본, TickCount = 1 });
        await after.조회Async();
        Assert.Equal(2, after.주문목록.Count(x => x.표시상태 == "조리 배정·시작 대기"));
        async Task Tick()
        {
            await runtime.Sessions.AdvanceWorldTickAsync(id, new 경영SimulationTick진행Request
            { CommandId = "tick:" + card.현재Tick, ExpectedRevision = card.판본, TickCount = 1 });
            await card.조회Async();
        }
    }
    private static LocalSimulationRuntime Create(string path) => new(
        new InMemory경영SimulationSessionStore(), new InMemorySimulationSessionSaveStore(), new FileSimulationLocalSaveSlotStore(path));
}
