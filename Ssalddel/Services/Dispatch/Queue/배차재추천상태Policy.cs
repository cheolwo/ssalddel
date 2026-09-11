using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

public static class 배차재추천상태Policy
{
    public static void 적용(운송원장 queue, string 제외기사Id, DateTime 변경시각Utc)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (string.IsNullOrWhiteSpace(제외기사Id))
            throw new ArgumentException("재추천에서 제외할 기사 ID가 필요합니다.", nameof(제외기사Id));

        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        queue.마지막거절기사Id = 제외기사Id.Trim();
        queue.확정기사Id = null;
        queue.기사_운송자 = string.Empty;
        queue.현재추천대상기사Id = null;
        queue.추천시작시각 = null;
        queue.추천만료시각 = null;
        queue.출발_픽업 = null;
        queue.도착 = null;
        queue.UpdatedAt = DateTime.SpecifyKind(변경시각Utc, DateTimeKind.Utc);
    }
}
