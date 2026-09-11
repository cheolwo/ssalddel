using System.Globalization;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Domain,
    "운영 배차 사건을 운영 시장의 오늘·어제·그제 지표로 계산한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 30,
    StepKey = "domain.operational-dispatch-short-window",
    DependsOnStepKeys = ["contract.operational-dispatch-activity"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "순수 계산만 수행하며 영속 저장, Redis 갱신, 배차 순위 변경 또는 책임 확정을 수행하지 않는다.")]
public sealed class 운영배차단기지표Calculator
{
    public 운영배차단기지표Dto 계산(
        IEnumerable<운영배차활동사건Dto> 사건목록,
        DateTimeOffset 생성시각Utc,
        TimeZoneInfo 운영시장시간대)
    {
        ArgumentNullException.ThrowIfNull(사건목록);
        ArgumentNullException.ThrowIfNull(운영시장시간대);

        var 기준시각Utc = 생성시각Utc.ToUniversalTime();
        var 오늘날짜 = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(기준시각Utc, 운영시장시간대).DateTime);
        var 날짜별 = new Dictionary<DateOnly, 누적지표>
        {
            [오늘날짜] = new(),
            [오늘날짜.AddDays(-1)] = new(),
            [오늘날짜.AddDays(-2)] = new()
        };

        var 유효사건 = 중복제거(사건목록)
            .Select(사건 =>
            {
                검증(사건);
                return 사건;
            })
            .Where(사건 => 사건.발생시각Utc.ToUniversalTime() <= 기준시각Utc)
            .ToArray();

        foreach (var 제안 in 유효사건.Where(유효제안인가))
        {
            var 운영날짜 = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(제안.발생시각Utc, 운영시장시간대).DateTime);
            if (!날짜별.TryGetValue(운영날짜, out var 지표))
            {
                continue;
            }

            var correlationKey = 상관관계Key(제안);
            var 사후부적합 = 유효사건.Any(x =>
                x.발생시각Utc >= 제안.발생시각Utc
                && string.Equals(상관관계Key(x), correlationKey, StringComparison.Ordinal)
                && 사후분모제외사건인가(x));
            if (!사후부적합)
            {
                지표.유효제안수 += 제안.지표단위수;
            }
        }

        foreach (var 사건 in 유효사건)
        {
            var 운영날짜 = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(사건.발생시각Utc, 운영시장시간대).DateTime);
            if (!날짜별.TryGetValue(운영날짜, out var 지표))
            {
                continue;
            }

            반영(지표, 사건);
        }

        foreach (var 수락묶음 in 유효사건
                     .Where(x => x.사건유형Code == 운영배차사건유형Code.수락)
                     .GroupBy(상관관계Key, StringComparer.Ordinal))
        {
            var 수락 = 수락묶음
                .OrderBy(x => x.발생시각Utc)
                .ThenBy(x => x.사건StableId, StringComparer.Ordinal)
                .First();
            var 수락날짜 = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(수락.발생시각Utc, 운영시장시간대).DateTime);
            if (!날짜별.TryGetValue(수락날짜, out var 지표))
            {
                continue;
            }

            var 관련사건 = 유효사건
                .Where(x => x.발생시각Utc >= 수락.발생시각Utc
                            && string.Equals(상관관계Key(x), 수락묶음.Key, StringComparison.Ordinal))
                .ToArray();
            반영수락Cohort(지표, 수락, 관련사건);
        }

        return new 운영배차단기지표Dto
        {
            운영시장시간대Id = 운영배차시장시간대Code.대한민국,
            생성시각Utc = 기준시각Utc,
            오늘 = 변환(오늘날짜, 날짜별[오늘날짜]),
            어제 = 변환(오늘날짜.AddDays(-1), 날짜별[오늘날짜.AddDays(-1)]),
            그제 = 변환(오늘날짜.AddDays(-2), 날짜별[오늘날짜.AddDays(-2)])
        };
    }

    public static TimeZoneInfo 대한민국시간대조회()
    {
        foreach (var id in new[] { 운영배차시장시간대Code.대한민국, "Korea Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new TimeZoneNotFoundException("대한민국 운영 시장 시간대를 찾을 수 없습니다.");
    }

    private static IReadOnlyList<운영배차활동사건Dto> 중복제거(
        IEnumerable<운영배차활동사건Dto> 사건목록)
    {
        var 결과 = new List<운영배차활동사건Dto>();
        foreach (var 묶음 in 사건목록.GroupBy(x => x?.사건StableId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(묶음.Key))
            {
                throw new ArgumentException("운영 배차 사건 식별자가 필요합니다.", nameof(사건목록));
            }

            var 첫사건 = 묶음.First()
                ?? throw new ArgumentException("운영 배차 사건은 null일 수 없습니다.", nameof(사건목록));
            var 첫지문 = 지문(첫사건);
            if (묶음.Skip(1).Any(x => x is null || !string.Equals(첫지문, 지문(x), StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"같은 사건 식별자 '{묶음.Key}'에 서로 다른 내용이 있습니다.");
            }

            결과.Add(첫사건);
        }

        return 결과;
    }

    private static void 검증(운영배차활동사건Dto 사건)
    {
        if (사건.지표단위수 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(사건.지표단위수), "지표 단위 수는 1 이상이어야 합니다.");
        }

        if (!string.Equals(
                사건.운영시장시간대Id,
                운영배차시장시간대Code.대한민국,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("지원하지 않는 운영 시장 시간대입니다.", nameof(사건));
        }

        if (string.IsNullOrWhiteSpace(사건.주체Id)
            || string.IsNullOrWhiteSpace(사건.주체역할Code))
        {
            throw new ArgumentException("운영 배차 사건의 주체 ID와 역할이 필요합니다.", nameof(사건));
        }
    }

    private static void 반영(누적지표 지표, 운영배차활동사건Dto 사건)
    {
        var 단위 = 사건.지표단위수;
        var 유효제안 = string.Equals(
            사건.제안유효성Code,
            운영배차제안유효성Code.유효,
            StringComparison.Ordinal);

        switch (사건.사건유형Code)
        {
            case 운영배차사건유형Code.거절 when 유효제안:
                지표.거절수 += 단위;
                break;
            case 운영배차사건유형Code.무응답 when 유효제안:
                지표.무응답수 += 단위;
                break;
        }
    }

    private static void 반영수락Cohort(
        누적지표 지표,
        운영배차활동사건Dto 수락,
        IReadOnlyList<운영배차활동사건Dto> 관련사건)
    {
        var 단위 = 수락.지표단위수;
        지표.수락수 += 단위;
        if (수락.주체역할Code != 운영배차주체역할Code.기사)
        {
            return;
        }

        지표.균형점유수 += 단위;

        var 종결 = 관련사건
            .Where(x => x.사건유형Code is 운영배차사건유형Code.완료
                or 운영배차사건유형Code.중단
                or 운영배차사건유형Code.취소)
            .OrderByDescending(x => x.발생시각Utc)
            .ThenByDescending(x => x.사건StableId, StringComparer.Ordinal)
            .FirstOrDefault();
        var 책임판정 = 관련사건
            .Where(x => x.사건유형Code == 운영배차사건유형Code.책임판정)
            .OrderByDescending(x => x.발생시각Utc)
            .ThenByDescending(x => x.사건StableId, StringComparer.Ordinal)
            .FirstOrDefault();
        var finalResponsibility = 책임판정?.책임Code ?? 종결?.책임Code;
        if (종결?.사건유형Code == 운영배차사건유형Code.완료)
        {
            지표.완료율대상수 += 단위;
            지표.완료수 += 단위;
        }
        else if (종결?.사건유형Code == 운영배차사건유형Code.중단)
        {
            if (finalResponsibility == 운영배차책임Code.기사)
            {
                지표.완료율대상수 += 단위;
                지표.기사책임중단수 += 단위;
            }
            else if (finalResponsibility == 운영배차책임Code.보호대상)
            {
                지표.보호중단수 += 단위;
            }
        }
        else
        {
            // 진행 중 수락은 종결되기 전까지 완료율 분모에 넣지 않는다.
        }
        지표.외부책임취소수 += Math.Min(
            단위,
            관련사건
                .Where(x => x.사건유형Code == 운영배차사건유형Code.취소
                            && 외부책임인가(x.책임Code))
                .Sum(x => x.지표단위수));
        지표.균형반환수 += Math.Min(
            단위,
            관련사건
                .Where(x => x.사건유형Code == 운영배차사건유형Code.균형건수반환
                            && 균형반환대상인가(finalResponsibility))
                .Sum(x => x.지표단위수));
    }

    private static int 종결단위(
        IEnumerable<운영배차활동사건Dto> 사건목록,
        string 사건유형Code,
        int 최대단위,
        string? 책임Code = null)
        => Math.Min(
            최대단위,
            사건목록
                .Where(x => x.사건유형Code == 사건유형Code
                            && (책임Code is null || x.책임Code == 책임Code))
                .Sum(x => x.지표단위수));

    private static string 상관관계Key(운영배차활동사건Dto 사건)
    {
        var subject = $"{사건.주체역할Code.Trim()}:{사건.주체Id.Trim()}";
        if (!string.IsNullOrWhiteSpace(사건.업무시도Id))
        {
            return $"{subject}|attempt:{사건.업무시도Id.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(사건.주문Id))
        {
            return $"{subject}|order:{사건.주문Id.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(사건.제안Id))
        {
            return $"{subject}|offer:{사건.제안Id.Trim()}";
        }

        if (사건.사건유형Code == 운영배차사건유형Code.수락)
        {
            throw new ArgumentException("수락 사건에는 주문 ID 또는 제안 ID가 필요합니다.", nameof(사건));
        }

        return $"{subject}|event:{사건.사건StableId}";
    }

    private static bool 유효제안인가(운영배차활동사건Dto 사건)
        => 사건.사건유형Code == 운영배차사건유형Code.제안전달
           && 사건.제안유효성Code == 운영배차제안유효성Code.유효;

    private static bool 사후분모제외사건인가(운영배차활동사건Dto 사건)
        => 사건.사건유형Code is 운영배차사건유형Code.거절
                or 운영배차사건유형Code.무응답
                or 운영배차사건유형Code.취소
                or 운영배차사건유형Code.중단
           && 사건.제안유효성Code is 운영배차제안유효성Code.조건부적합
                or 운영배차제안유효성Code.전달실패
                or 운영배차제안유효성Code.보호사유;

    private static bool 외부책임인가(string 책임Code)
        => 책임Code is 운영배차책임Code.주문자
            or 운영배차책임Code.음식점
            or 운영배차책임Code.플랫폼;

    private static bool 균형반환대상인가(string? 책임Code)
        => 책임Code == 운영배차책임Code.보호대상
           || (책임Code is not null && 외부책임인가(책임Code));

    private static 운영배차일별지표Dto 변환(DateOnly 날짜, 누적지표 값)
        => new()
        {
            운영시장날짜 = 날짜.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            유효제안수 = 값.유효제안수,
            수락수 = 값.수락수,
            거절수 = 값.거절수,
            무응답수 = 값.무응답수,
            완료수 = 값.완료수,
            기사책임중단수 = 값.기사책임중단수,
            외부책임취소수 = 값.외부책임취소수,
            보호중단수 = 값.보호중단수,
            균형점유수 = 값.균형점유수,
            균형반환수 = 값.균형반환수,
            수락률 = 비율(값.수락수, 값.유효제안수),
            거절률 = 비율(값.거절수, 값.유효제안수),
            수락후완료율 = 비율(값.완료수, 값.완료율대상수)
        };

    private static decimal? 비율(int 분자, int 분모)
        => 분모 == 0 ? null : decimal.Divide(분자, 분모);

    private static string 지문(운영배차활동사건Dto 사건)
        => string.Join('|',
            사건.발생시각Utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            사건.운영시장시간대Id,
            사건.ShiftId,
            사건.제안Id,
            사건.주문Id,
            사건.업무시도Id,
            사건.주체Id,
            사건.주체역할Code,
            사건.사건유형Code,
            사건.제안유효성Code,
            사건.책임Code,
            사건.사유Code,
            사건.상태값Code,
            사건.지표단위수.ToString(CultureInfo.InvariantCulture));

    private sealed class 누적지표
    {
        public int 유효제안수 { get; set; }
        public int 수락수 { get; set; }
        public int 완료율대상수 { get; set; }
        public int 거절수 { get; set; }
        public int 무응답수 { get; set; }
        public int 완료수 { get; set; }
        public int 기사책임중단수 { get; set; }
        public int 외부책임취소수 { get; set; }
        public int 보호중단수 { get; set; }
        public int 균형점유수 { get; set; }
        public int 균형반환수 { get; set; }
    }
}
