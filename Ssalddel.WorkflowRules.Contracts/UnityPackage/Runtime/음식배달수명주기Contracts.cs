using System;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class 음식배달상태원천코드
    {
        public const string OperationalServer = "OperationalServer";
        public const string SimulationCore = "SimulationCore";
    }

    /// <summary>
    /// 운영 서버와 Simulation Core가 Unity에 같은 뜻으로 제공하는 읽기 전용 음식 배달 상태 사본입니다.
    /// 이 계약은 주문·배차 상태를 변경하지 않습니다.
    /// </summary>
    public sealed class 음식배달수명주기Snapshot
    {
        public string SourceCode { get; set; } = string.Empty;
        public string SourceRevision { get; set; } = string.Empty;
        public string OrderStableId { get; set; } = string.Empty;
        public long OrderRevision { get; set; }
        public string OrderStateCode { get; set; } = string.Empty;
        public string DispatchStateCode { get; set; } = string.Empty;
        public string RestaurantStableId { get; set; } = string.Empty;
        public string OrdererStableId { get; set; } = string.Empty;
        public string DriverStableId { get; set; } = string.Empty;
        public DateTime? AcceptedAtUtc { get; set; }
        public DateTime? ReadyForPickupAtUtc { get; set; }
        public DateTime? DispatchRequestedAtUtc { get; set; }
        public DateTime? PickedUpAtUtc { get; set; }
        public DateTime? DeliveredAtUtc { get; set; }
        public DateTime? ReceiptConfirmedAtUtc { get; set; }
        public string[] SourceRefs { get; set; } = Array.Empty<string>();
    }
}
