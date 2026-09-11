using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식점조리시간ServiceTests
{
    [Fact]
    public async Task 최근28일_같은시간대_세표본의_산술평균을_참고값으로쓴다()
    {
        await using var db = CreateContext();
        var 기준시각 = new DateTime(2026, 9, 11, 3, 20, 0, DateTimeKind.Utc);
        db.음식주문.Add(Target("target", 기준시각));
        db.음식주문.AddRange(
            Sample("sample-1", 기준시각.AddDays(-1).Date.AddHours(3), 10),
            Sample("sample-2", 기준시각.AddDays(-2).Date.AddHours(3), 20),
            Sample("sample-3", 기준시각.AddDays(-3).Date.AddHours(3), 30));
        await db.SaveChangesAsync();

        var result = await new 음식점조리시간Service(db)
            .주문수락값결정Async("target", 10, null, false, 기준시각);

        Assert.NotNull(result);
        Assert.Equal(20, result.참고조리분);
        Assert.Equal(20, result.적용조리분);
        Assert.Equal(3, result.관측표본수);
        Assert.Equal(음식조리시간결정출처Code.플랫폼관측평균, result.결정출처Code);
    }

    [Fact]
    public async Task 관측표본이_세건미만이면_현재시간대_음식점설정을쓴다()
    {
        await using var db = CreateContext();
        var 기준시각 = new DateTime(2026, 9, 11, 3, 20, 0, DateTimeKind.Utc);
        db.음식주문.Add(Target("target", 기준시각));
        db.음식점조리시간설정.Add(new 음식점조리시간설정
        {
            음식점Id = 10,
            시작분 = 720,
            종료분 = 780,
            조리예상분 = 35,
            설정Revision = 1,
            변경요청Id = Guid.NewGuid(),
            변경UserId = "restaurant-user",
            CreatedAtUtc = 기준시각,
            UpdatedAtUtc = 기준시각
        });
        await db.SaveChangesAsync();

        var result = await new 음식점조리시간Service(db)
            .주문수락값결정Async("target", 10, null, false, 기준시각);

        Assert.NotNull(result);
        Assert.Equal(35, result.적용조리분);
        Assert.Equal(음식조리시간결정출처Code.음식점설정, result.결정출처Code);
    }

    [Fact]
    public async Task 음식점의_명시선택은_참고값보다_우선한다()
    {
        await using var db = CreateContext();
        var 기준시각 = new DateTime(2026, 9, 11, 3, 20, 0, DateTimeKind.Utc);
        db.음식주문.Add(Target("target", 기준시각));
        await db.SaveChangesAsync();

        var result = await new 음식점조리시간Service(db)
            .주문수락값결정Async("target", 10, 45, false, 기준시각);

        Assert.NotNull(result);
        Assert.Equal(20, result.참고조리분);
        Assert.Equal(45, result.음식점선택조리분);
        Assert.Equal(45, result.적용조리분);
        Assert.Equal(음식조리시간결정출처Code.음식점명시선택, result.결정출처Code);
    }

    [Fact]
    public async Task 같은범위의_겹치는시간구간은_거절한다()
    {
        await using var db = CreateContext();
        var service = new 음식점조리시간Service(db);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.설정교체Async(
            10,
            "restaurant-user",
            new 음식점조리시간설정변경요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                항목 =
                [
                    new() { 시작분 = 600, 종료분 = 720, 조리예상분 = 20 },
                    new() { 시작분 = 700, 종료분 = 800, 조리예상분 = 30 }
                ]
            }));

        Assert.Contains("겹칠 수 없습니다", error.Message, StringComparison.Ordinal);
    }

    private static 음식주문 Target(string orderNo, DateTime now)
        => new()
        {
            주문번호 = orderNo,
            음식점Id = 10,
            음식점명 = "테스트 음식점",
            상태 = 음식주문상태코드.주문대기,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static 음식주문 Sample(string orderNo, DateTime acceptedAtUtc, int durationMinutes)
    {
        var order = Target(orderNo, acceptedAtUtc);
        order.음식점수락시각Utc = DateTime.SpecifyKind(acceptedAtUtc, DateTimeKind.Utc);
        order.상태 = 음식주문상태코드.픽업대기;
        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = 음식주문상태코드.조리중,
            다음상태 = 음식주문상태코드.픽업대기,
            사유 = "음식점 픽업 준비 완료",
            전이시각Utc = order.음식점수락시각Utc.Value.AddMinutes(durationMinutes)
        });
        return order;
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-preparation-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
