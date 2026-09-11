using Ssalddel.Contracts.Common.Dispatch;
using StackExchange.Redis;
using 살뜰.Infrastructure.Storage.Redis;

namespace Ssalddel.Tests.Services.Dispatch.Common;

public sealed class 운영배차RedisProjectionIntegrationTests
{
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public async Task 설정된_Redis에서_수신상태와_단기지표를_저장하고_다시_읽는다()
    {
        var connectionString = Environment.GetEnvironmentVariable("SSALDDEL_TEST_REDIS_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var store = new Redis운영배차판정ProjectionStore(redis);
        var subjectId = $"test-driver-{Guid.NewGuid():N}";
        var database = redis.GetDatabase();
        var availabilityKey = $"ssalddel:operational-dispatch:availability:{Uri.EscapeDataString(subjectId)}";
        var metricsKey = $"ssalddel:operational-dispatch:short-metrics:{Uri.EscapeDataString(subjectId)}";

        try
        {
            await store.수신상태저장Async(new 운영배차수신상태Dto
            {
                주체Id = subjectId,
                수신의사Code = 운영배차수신의사Code.On,
                실효상태Code = 운영배차실효상태Code.연결확인불가,
                서버관측시각Utc = DateTimeOffset.UtcNow
            });
            await store.단기지표저장Async(subjectId, new 운영배차단기지표Dto
            {
                운영시장시간대Id = 운영배차시장시간대Code.대한민국,
                생성시각Utc = DateTimeOffset.UtcNow,
                오늘 = new 운영배차일별지표Dto
                {
                    운영시장날짜 = "2026-09-10",
                    유효제안수 = 2,
                    거절수 = 1,
                    거절률 = 0.5m
                }
            });

            var availability = await store.수신상태조회Async(subjectId);
            var metrics = await store.단기지표조회Async(subjectId);

            Assert.Equal(운영배차수신의사Code.On, availability?.수신의사Code);
            Assert.Equal(운영배차실효상태Code.연결확인불가, availability?.실효상태Code);
            Assert.Equal(0.5m, metrics?.오늘.거절률);
            var availabilityTtl = await database.KeyTimeToLiveAsync(availabilityKey);
            var metricsTtl = await database.KeyTimeToLiveAsync(metricsKey);
            Assert.True(availabilityTtl.HasValue && availabilityTtl.Value > TimeSpan.Zero);
            Assert.True(metricsTtl.HasValue && metricsTtl.Value > TimeSpan.Zero);
        }
        finally
        {
            await database.KeyDeleteAsync(new RedisKey[] { availabilityKey, metricsKey });
        }
    }
}
