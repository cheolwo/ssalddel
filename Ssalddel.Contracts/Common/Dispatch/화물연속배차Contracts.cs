namespace Ssalddel.Contracts.Common.Dispatch;

public static class 화물연속배차예약상태Code
{
    public const string 보유 = "Held";
    public const string 수락 = "Accepted";
    public const string 만료 = "Expired";
    public const string 반환 = "Released";
    public const string 무효 = "Invalidated";
}

public static class 화물시간위험Code
{
    public const string 정상 = "Normal";
    public const string 주의 = "Watch";
    public const string 위험 = "Critical";
    public const string 근거부족 = "EvidenceMissing";
}

public static class 화물조건재동의상태Code
{
    public const string 대기 = "Pending";
    public const string 동의 = "Accepted";
    public const string 거절 = "Rejected";
}

public sealed class 화물연속배차의사변경요청
{
    public Guid ClientRequestId { get; set; }
    public bool Enabled { get; set; }
}

public sealed class 화물연속배차상태Dto
{
    public const string CurrentRuleRevision = "freight-continuity.r1";

    public string RuleRevision { get; set; } = CurrentRuleRevision;
    public string 기사Id { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset? 변경시각Utc { get; set; }
    public 화물다음콜예약Dto? 보유중다음콜 { get; set; }
    public 화물연속배차지표Dto 지표 { get; set; } = new();
}

public sealed class 화물운송시간약속Dto
{
    public string 의뢰Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTimeOffset 목표도착시각Utc { get; set; }
    public DateTimeOffset 최종도착한계시각Utc { get; set; }
    public int 잠금완충분 { get; set; }
    public int 픽업서비스분 { get; set; }
    public int 하차서비스분 { get; set; }
    public int 기준경로분 { get; set; }
    public int 보수경로분 { get; set; }
    public string 위험Code { get; set; } = 화물시간위험Code.정상;
    public int 남은완충분 { get; set; }
    public string 시간약속Revision { get; set; } = string.Empty;
}

public sealed class 화물다음콜예약Dto
{
    public string ReservationId { get; set; } = string.Empty;
    public string 의뢰Id { get; set; } = string.Empty;
    public string 상태Code { get; set; } = 화물연속배차예약상태Code.보유;
    public long Revision { get; set; }
    public int RecommendationRound { get; set; }
    public DateTimeOffset 보유시각Utc { get; set; }
    public DateTimeOffset 만료시각Utc { get; set; }
    public DateTimeOffset 음성알림예정시각Utc { get; set; }
    public int 보유초 { get; set; }
    public decimal? 추천점수 { get; set; }
    public decimal? 기사최소지급액 { get; set; }
    public decimal? 운임부족금액 { get; set; }
    public string 반환사유Code { get; set; } = string.Empty;
}

public sealed class 화물연속배차지표Dto
{
    public int? 무급공백분 { get; set; }
    public int? 활동시간분 { get; set; }
    public decimal? 예상순수익 { get; set; }
    public decimal? 활동시간당예상순수익 { get; set; }
    public decimal? 참고시간당완료건수 { get; set; }
}

public sealed class 화물경로위험Dto
{
    public string 위험Code { get; set; } = 화물시간위험Code.근거부족;
    public string 의뢰Id { get; set; } = string.Empty;
    public DateTimeOffset? 예상도착시각Utc { get; set; }
    public DateTimeOffset? 보수예상도착시각Utc { get; set; }
    public int? 남은완충분 { get; set; }
    public bool 보유콜자동반환필요 { get; set; }
    public string 사유Code { get; set; } = string.Empty;
}

public sealed class 화물최소지급판정Dto
{
    public decimal 기사최소지급액 { get; set; }
    public decimal 운임부족금액 { get; set; }
    public bool 추천가능 { get; set; }
    public bool 승인프로모션적용 { get; set; }
    public string 운임정책Revision { get; set; } = string.Empty;
}
