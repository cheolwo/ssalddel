using System;
using System.Linq;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "가상 마트 주문의 예약·피킹·포장·인계 시각", Boundary = "운영 주문과 분리된 합성 사본")]
    public sealed class 가상마트주문Snapshot
    {
        public string OrderId { get; set; } = "";
        public string DestinationId { get; set; } = "facility:synthetic:a";
        public string Stage { get; set; } = "Pending";
        public string WaitReason { get; set; } = "";
        public int AcceptedTick { get; set; }
        public int? PackedTick { get; set; }
        public int? ReadyTick { get; set; }
        public int? PickedUpTick { get; set; }
        public int? DeliveredTick { get; set; }
        public int? ReceivedTick { get; set; }
        public int PickedUnits { get; set; }
        public 가상마트주문Snapshot Copy() => (가상마트주문Snapshot)MemberwiseClone();
    }

    /// <summary>각 주문은 두 적재대 상품을 한 개씩 요구한다. 운영 재고와 무관한 r3 전용 사본.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "마트 작업자와 재고의 조회 사본", Boundary = "표현은 사본을 읽으며 재고 권위를 갖지 않는다")]
    public sealed class 가상마트Snapshot
    {
        public int[] Stock { get; set; } = new[] { 2, 2 };
        public int[] Reserved { get; set; } = new[] { 0, 0 };
        public string WorkerStage { get; set; } = "Idle";
        public string WorkerOrderId { get; set; } = "";
        public double X { get; set; } = 0;
        public double Z { get; set; } = -14;
        public int Shelf { get; set; }
        public int WorkTicks { get; set; }
        public 가상마트주문Snapshot[] Orders { get; set; } = Array.Empty<가상마트주문Snapshot>();
        public 가상마트Snapshot Copy() => new 가상마트Snapshot {
            Stock = Stock.ToArray(), Reserved = Reserved.ToArray(), WorkerStage = WorkerStage,
            WorkerOrderId = WorkerOrderId, X = X, Z = Z, Shelf = Shelf, WorkTicks = WorkTicks,
            Orders = Orders.Select(x => x.Copy()).ToArray() };
    }
}
