using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송현재조회QueryHandlerTests
{
    [Fact]
    public async Task 최신수정건보다_가장먼저해야할현장행동을_현재운송으로선택한다()
    {
        await using var db = CreateContext();
        db.운송원장.AddRange(
            new 운송원장
            {
                의뢰Id = "pickup-next",
                운송번호 = "pickup-next",
                기사_운송자 = "driver-1",
                상태 = "배차확정",
                UpdatedAt = DateTime.UtcNow
            },
            new 운송원장
            {
                의뢰Id = "dropoff-now",
                운송번호 = "dropoff-now",
                기사_운송자 = "driver-1",
                상태 = "하차지도착",
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new 운송원장
            {
                의뢰Id = "food-must-not-enter-freight-workspace",
                운송번호 = "food-must-not-enter-freight-workspace",
                기사_운송자 = "driver-1",
                배차업무유형 = 상태값.배차업무유형.음식배달,
                상태 = "하차지도착",
                UpdatedAt = DateTime.UtcNow.AddHours(-2)
            });
        await db.SaveChangesAsync();

        var result = await new 운송현재조회QueryHandler(db).Handle(
            new 운송현재조회Query("driver-1"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("dropoff-now", result!.운송번호);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"current-freight-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
