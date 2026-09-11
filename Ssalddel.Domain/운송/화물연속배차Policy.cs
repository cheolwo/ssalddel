namespace 살뜰.도메인.운송;

public sealed class 화물연속배차Policy
{
    public int 예약보유초(bool 이동중, bool 도착임박)
        => 도착임박 ? 120 : 이동중 ? 300 : 180;

    public 화물시간약속계산결과 시간약속계산(
        DateTime 기준시각Utc,
        int 픽업서비스분,
        int 기준경로분,
        int 하차서비스분,
        decimal 불확실성비율,
        int 최소완충분,
        DateTime? 화주최종요청시각Utc)
    {
        if (픽업서비스분 < 0 || 기준경로분 <= 0 || 하차서비스분 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(기준경로분), "서비스 시간과 경로 시간은 유효해야 합니다.");
        }

        var target = 기준시각Utc.AddMinutes(픽업서비스분 + 기준경로분 + 하차서비스분);
        var buffer = Math.Max(최소완충분, (int)Math.Ceiling(기준경로분 * Math.Max(0m, 불확실성비율)));
        var final = target.AddMinutes(buffer);
        if (화주최종요청시각Utc.HasValue && 화주최종요청시각Utc.Value < final)
        {
            final = 화주최종요청시각Utc.Value;
            buffer = Math.Max(0, (int)Math.Floor((final - target).TotalMinutes));
        }

        if (final < target)
        {
            throw new InvalidOperationException("화주의 최종 도착 한계가 목표 도착 시각보다 빠릅니다.");
        }

        return new 화물시간약속계산결과(target, final, buffer);
    }

    public string 위험판정(
        DateTime 목표도착시각Utc,
        DateTime 최종도착한계시각Utc,
        DateTime 예상도착시각Utc,
        DateTime 보수예상도착시각Utc,
        decimal 주의남은완충비율 = 0.5m)
    {
        if (보수예상도착시각Utc > 최종도착한계시각Utc)
        {
            return "Critical";
        }

        var originalBuffer = Math.Max(0d, (최종도착한계시각Utc - 목표도착시각Utc).TotalMinutes);
        var remainingBuffer = (최종도착한계시각Utc - 보수예상도착시각Utc).TotalMinutes;
        if (예상도착시각Utc > 목표도착시각Utc
            || (originalBuffer > 0d && remainingBuffer <= originalBuffer * (double)주의남은완충비율))
        {
            return "Watch";
        }

        return "Normal";
    }

    public bool 추가삽입가능(IEnumerable<(DateTime 보수예상도착시각Utc, DateTime 최종도착한계시각Utc)> 약속목록)
        => 약속목록.All(x => x.보수예상도착시각Utc <= x.최종도착한계시각Utc);

    public 화물최소지급계산결과 최소지급액계산(
        decimal 차량기본액,
        decimal 공차거리Km,
        decimal 공차Km단가,
        decimal 적재거리Km,
        decimal 적재Km단가,
        decimal 예상업무시간분,
        decimal 시간당단가,
        decimal 톨비,
        decimal 수작업비,
        decimal 대기료,
        decimal 할증,
        decimal 제시운임,
        decimal 승인프로모션보전액)
    {
        var minimum = Math.Max(0m, 차량기본액)
            + Math.Max(0m, 공차거리Km) * Math.Max(0m, 공차Km단가)
            + Math.Max(0m, 적재거리Km) * Math.Max(0m, 적재Km단가)
            + Math.Max(0m, 예상업무시간분) / 60m * Math.Max(0m, 시간당단가)
            + Math.Max(0m, 톨비)
            + Math.Max(0m, 수작업비)
            + Math.Max(0m, 대기료)
            + Math.Max(0m, 할증);
        minimum = decimal.Round(minimum, 0, MidpointRounding.AwayFromZero);
        var funded = Math.Max(0m, 제시운임) + Math.Max(0m, 승인프로모션보전액);
        return new 화물최소지급계산결과(minimum, Math.Max(0m, minimum - funded), funded >= minimum);
    }

    public bool 대기기사우선(decimal 연속기사점수, decimal 최고대기기사점수, decimal 공정성격차점수 = 10m)
        => 최고대기기사점수 - 연속기사점수 > Math.Max(0m, 공정성격차점수);
}

public sealed record 화물시간약속계산결과(DateTime 목표도착시각Utc, DateTime 최종도착한계시각Utc, int 잠금완충분);
public sealed record 화물최소지급계산결과(decimal 기사최소지급액, decimal 운임부족금액, bool 추천가능);
