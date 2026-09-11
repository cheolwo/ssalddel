using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class 배차재추천상태PolicyTests
{
    [Fact]
    public void 중단한기사를제외하고_같은원장을_즉시재추천대기로되돌린다()
    {
        var now = new DateTime(2026, 9, 11, 4, 0, 0, DateTimeKind.Utc);
        var queue = new 운송원장
        {
            상태 = 상태값.배차대기상태.확정,
            배차큐단계 = 상태값.배차큐단계.확정,
            배차노출상태 = 상태값.배차노출상태.확정,
            확정기사Id = "driver-1",
            기사_운송자 = "driver-1",
            현재추천대상기사Id = "driver-1",
            추천시작시각 = now.AddMinutes(-1),
            추천만료시각 = now.AddMinutes(1),
            출발_픽업 = now.AddMinutes(-5),
            도착 = now.AddMinutes(-1)
        };

        배차재추천상태Policy.적용(queue, "driver-1", now);

        Assert.Equal(상태값.배차대기상태.대기, queue.상태);
        Assert.Equal(상태값.배차큐단계.배차추천, queue.배차큐단계);
        Assert.Equal(상태값.배차노출상태.추천대기, queue.배차노출상태);
        Assert.Equal("driver-1", queue.마지막거절기사Id);
        Assert.Null(queue.확정기사Id);
        Assert.Empty(queue.기사_운송자);
        Assert.Null(queue.현재추천대상기사Id);
        Assert.Null(queue.추천시작시각);
        Assert.Null(queue.추천만료시각);
        Assert.Null(queue.출발_픽업);
        Assert.Null(queue.도착);
        Assert.Equal(now, queue.UpdatedAt);
    }
}
