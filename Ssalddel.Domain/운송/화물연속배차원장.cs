namespace 살뜰.도메인.운송;

public sealed class 화물연속배차상태
{
    public long Id { get; set; }
    public string 기사Id { get; set; } = string.Empty;
    public bool 활성여부 { get; set; }
    public long Revision { get; set; }
    public string 마지막ClientRequestId { get; set; } = string.Empty;
    public DateTime? 마지막유상운송완료시각Utc { get; set; }
    public DateTime? 다음유상픽업시각Utc { get; set; }
    public DateTime 변경시각Utc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class 화물운송시간약속
{
    public long Id { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
    public string 기사Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime 목표도착시각Utc { get; set; }
    public DateTime 최종도착한계시각Utc { get; set; }
    public DateTime? 화주최종요청시각Utc { get; set; }
    public int 잠금완충분 { get; set; }
    public int 픽업서비스분 { get; set; }
    public int 하차서비스분 { get; set; }
    public int 기준경로분 { get; set; }
    public int 보수경로분 { get; set; }
    public string 시간약속Revision { get; set; } = string.Empty;
    public DateTime 잠금시각Utc { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class 화물다음콜예약
{
    public long Id { get; set; }
    public string ReservationId { get; set; } = string.Empty;
    public string 기사Id { get; set; } = string.Empty;
    public string? 활성예약기사Key { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
    public string 현재운송의뢰Id { get; set; } = string.Empty;
    public string 상태Code { get; set; } = "Held";
    public long Revision { get; set; }
    public int RecommendationRound { get; set; }
    public DateTime 보유시각Utc { get; set; }
    public DateTime 만료시각Utc { get; set; }
    public DateTime 음성알림예정시각Utc { get; set; }
    public DateTime? 음성알림발송시각Utc { get; set; }
    public decimal? 추천점수 { get; set; }
    public decimal? 기사최소지급액 { get; set; }
    public decimal? 운임부족금액 { get; set; }
    public string 반환사유Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
