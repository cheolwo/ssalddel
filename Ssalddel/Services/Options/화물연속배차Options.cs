namespace 살뜰.Services.Options;

public sealed class 화물연속배차Options
{
    public const string SectionName = "FreightContinuity";

    public bool Enabled { get; set; }
    public bool ShadowMode { get; set; } = true;
    public bool 운임정책활성 { get; set; }
    public string 운임정책Revision { get; set; } = "freight-payout-floor.unapproved";
    public string 시간약속Revision { get; set; } = "freight-time-commitment.r1";
    public int 탐색주기초 { get; set; } = 30;
    public int 이동중예약초 { get; set; } = 300;
    public int 일반예약초 { get; set; } = 180;
    public int 도착임박예약초 { get; set; } = 120;
    public decimal 도착임박거리Km { get; set; } = 10m;
    public decimal 연속픽업최대거리Km { get; set; } = 5m;
    public decimal 공정성격차점수 { get; set; } = 10m;
    public int 기본픽업서비스분 { get; set; } = 15;
    public int 기본하차서비스분 { get; set; } = 15;
    public int 최소완충분 { get; set; } = 15;
    public decimal 불확실성완충비율 { get; set; } = 0.2m;
    public decimal 주의남은완충비율 { get; set; } = 0.5m;
    public int 동일위험재알림악화분 { get; set; } = 10;
    public int 무알림Eta변경분 { get; set; } = 5;
    public decimal 차량기본액 { get; set; }
    public decimal 공차Km단가 { get; set; }
    public decimal 적재Km단가 { get; set; }
    public decimal 시간당단가 { get; set; }
}
