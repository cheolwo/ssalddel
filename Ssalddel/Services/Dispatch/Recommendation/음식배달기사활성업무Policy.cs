namespace 살뜰.Services.Dispatch.Recommendation;

/// <summary>
/// 한 기사가 동시에 책임질 수 있는 음식 배달 업무 수를 판정한다.
/// 화면의 버튼 상태가 아니라 서버 수락 트랜잭션이 이 정책을 최종 적용한다.
/// </summary>
public static class 음식배달기사활성업무Policy
{
    public const int MaxActiveDeliveries = 3;
    public const string LimitExceededErrorCode = "FoodDeliveryActiveWorkLimitExceeded";

    public static bool 수락가능한가(int currentActiveDeliveries, int requestedDeliveries)
        => currentActiveDeliveries >= 0
           && requestedDeliveries > 0
           && currentActiveDeliveries + requestedDeliveries <= MaxActiveDeliveries;

    public static string 초과안내(int currentActiveDeliveries, int requestedDeliveries)
        => $"현재 진행 중인 음식 배달 {currentActiveDeliveries}건과 새 수락 {requestedDeliveries}건을 합치면 "
           + $"동시 수행 상한 {MaxActiveDeliveries}건을 초과합니다.";
}
