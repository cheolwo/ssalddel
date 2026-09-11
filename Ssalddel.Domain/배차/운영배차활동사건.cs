namespace 살뜰.도메인.배차;

public sealed class 운영배차활동사건
{
    public string 사건StableId { get; set; } = string.Empty;
    public DateTime 발생시각Utc { get; set; }
    public string 운영시장시간대Id { get; set; } = string.Empty;
    public string? ShiftId { get; set; }
    public string? 제안Id { get; set; }
    public string? 주문Id { get; set; }
    public string? 업무시도Id { get; set; }
    public string 주체Id { get; set; } = string.Empty;
    public string 주체역할Code { get; set; } = string.Empty;
    public string 사건유형Code { get; set; } = string.Empty;
    public string 제안유효성Code { get; set; } = string.Empty;
    public string 책임Code { get; set; } = string.Empty;
    public string 사유Code { get; set; } = string.Empty;
    public string 상태값Code { get; set; } = string.Empty;
    public int 지표단위수 { get; set; }
    public DateTime 기록시각Utc { get; set; }
}
