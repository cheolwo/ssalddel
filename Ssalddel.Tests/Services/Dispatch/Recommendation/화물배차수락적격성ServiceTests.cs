using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 화물배차수락적격성ServiceTests
{
    [Fact]
    public async Task 진행중운송이있는데_차량제원이없으면_추가수락을차단한다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 차량 = "등록되지않은차량" });
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "active-1",
            운송번호 = "active-1",
            기사_운송자 = "driver-1",
            상태 = "상차완료"
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).평가Async(
            "driver-1",
            Candidate("candidate-1"),
            [],
            CancellationToken.None);

        Assert.False(result.수락가능);
        Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    [Fact]
    public async Task 첫단독운송은_차량제원경고를확인한뒤_수락할수있다()
    {
        await using var db = CreateContext();
        db.용달기사.Add(new 용달기사 { 기사Id = "driver-1", 차량 = "등록되지않은차량" });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = Candidate("candidate-1");

        var withoutAcknowledgement = await service.평가Async("driver-1", request, [], CancellationToken.None);
        var acknowledged = await service.평가Async(
            "driver-1",
            request,
            [화물배차수락경고코드.차량주의사항],
            CancellationToken.None);

        Assert.False(withoutAcknowledgement.수락가능);
        Assert.Equal(화물배차수락오류코드.경고확인필요, withoutAcknowledgement.오류코드);
        Assert.True(acknowledged.수락가능);
    }

    private static 화물배차수락적격성Service CreateService(SsalddelContext db)
    {
        var route = new StubRouteService();
        return new 화물배차수락적격성Service(
            db,
            new StubLocationStore(),
            new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(db),
            new 운송일정삽입평가Service(route));
    }

    private static 화주운송의뢰 Candidate(string id) => new()
    {
        의뢰Id = id,
        운송방식 = "혼적",
        화물종류 = "상자",
        픽업_도로명주소 = "상차지",
        하차_도로명주소 = "하차지"
    };

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"freight-acceptance-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class StubLocationStore : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot snapshot) { }
        public bool TryGetLatest(string driverId, out DriverLocationSnapshot snapshot)
        {
            snapshot = null!;
            return false;
        }
    }

    private sealed class StubRouteService : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => null;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
