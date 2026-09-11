namespace Ssalddel.Contracts.Food;

public static class 음식조리시간결정출처Code
{
    public const string 음식점명시선택 = "RestaurantExplicit";
    public const string 플랫폼관측평균 = "PlatformObservedAverage";
    public const string 음식점설정 = "RestaurantSetting";
    public const string 시스템기본 = "SystemDefault";
    public const string 즉시픽업 = "ImmediatePickup";
}

public sealed class 음식점조리시간설정항목Dto
{
    public long? 메뉴Id { get; set; }
    public int 시작분 { get; set; }
    public int 종료분 { get; set; }
    public int 조리예상분 { get; set; }
}

public sealed class 음식점조리시간설정응답
{
    public long 음식점Id { get; set; }
    public long Revision { get; set; }
    public IReadOnlyList<음식점조리시간설정항목Dto> 항목 { get; set; } = [];
    public DateTime? 최근변경시각Utc { get; set; }
}

public sealed class 음식점조리시간설정변경요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long? 예상Revision { get; set; }
    public IReadOnlyList<음식점조리시간설정항목Dto> 항목 { get; set; } = [];
}

public sealed class 음식점조리참고응답
{
    public string 주문번호 { get; set; } = string.Empty;
    public int 참고조리분 { get; set; }
    public string 참고결정출처Code { get; set; } = 음식조리시간결정출처Code.시스템기본;
    public int 관측표본수 { get; set; }
    public int 관측기간일수 { get; set; } = 28;
    public string 운영시장시간대Id { get; set; } = "Asia/Seoul";
}

public static class 음식배달시도상태Code
{
    public const string 수락 = "Accepted";
    public const string 가게도착 = "RestaurantArrived";
    public const string 픽업완료 = "PickedUp";
    public const string 중단 = "Interrupted";
    public const string 전달완료 = "Completed";
}

public static class 음식배달중단사유Code
{
    public const string 사고 = "Accident";
    public const string 조리지연 = "RestaurantPreparationDelay";
    public const string 배터리부족 = "BatteryLow";
    public const string 배달수단고장 = "VehicleBreakdown";
    public const string 위험기상 = "UnsafeWeather";
    public const string 개인긴급 = "PersonalUrgency";
    public const string 기타 = "Other";

    private static readonly HashSet<string> 허용목록 = new(StringComparer.Ordinal)
    {
        사고,
        조리지연,
        배터리부족,
        배달수단고장,
        위험기상,
        개인긴급,
        기타
    };

    public static string 정규화(string? value)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || !허용목록.Contains(clean))
        {
            throw new ArgumentException("지원하지 않는 음식 배달 중단 사유입니다.", nameof(value));
        }

        return clean;
    }
}

public sealed class 음식배달가게도착요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long? 예상시도Revision { get; set; }
}

public sealed class 음식배달중단요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long? 예상시도Revision { get; set; }
    public string 사유Code { get; set; } = string.Empty;
    public string? 메모 { get; set; }
}

public static class 음식배달중단검토판정Code
{
    public const string 보호 = "Protected";
    public const string 기사책임 = "DriverResponsible";
    public const string 음식점책임 = "RestaurantResponsible";
    public const string 플랫폼책임 = "PlatformResponsible";
}

public sealed class 음식배달중단검토요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long? 예상Revision { get; set; }
    public string 판정Code { get; set; } = string.Empty;
    public bool 악용확정여부 { get; set; }
    public string 판정사유 { get; set; } = string.Empty;
}
