using System;
using System.Linq;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "기사별 대기 자리·복귀 예약·다음 주문 사본", Boundary = "합성 전용 상태이며 운영 기사 정보가 아니다.")]
    public sealed class 가상배달대기기사Snapshot
    {
        public 가상배달기사Snapshot Courier { get; set; } = new 가상배달기사Snapshot();
        public int Slot { get; set; }
        public int ReturnSlot { get; set; } = -1;
        public int AssignedTick { get; set; }
        public string PendingOrderId { get; set; } = "";
        public string WaitReason { get; set; } = "";
        public 가상배달대기기사Snapshot Copy() => new 가상배달대기기사Snapshot {
            Courier = Courier.Copy(), Slot = Slot, ReturnSlot = ReturnSlot, AssignedTick = AssignedTick,
            PendingOrderId = PendingOrderId, WaitReason = WaitReason };
    }
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "세 기사와 공유 통행 묶음의 조회 사본", Boundary = "복제 조회 결과이며 Unity에 상태 권위를 주지 않는다.")]
    public sealed class 가상배달대기Snapshot
    {
        public 가상마트Snapshot? Mart { get; set; }
        public 가상주문흐름Snapshot? OrderFlow { get; set; }
        public 가상동네생활Snapshot? NeighborhoodLife { get; set; }
        public int Batch { get; set; }
        public string TrafficOwner { get; set; } = "";
        public string Decision { get; set; } = "";
        public 가상배달대기기사Snapshot[] Drivers { get; set; } = Array.Empty<가상배달대기기사Snapshot>();
        public 가상배달대기Snapshot Copy() => new 가상배달대기Snapshot { Batch = Batch,
            TrafficOwner = TrafficOwner, Decision = Decision, Drivers = Drivers.Select(x => x.Copy()).ToArray(), Mart = Mart?.Copy(), OrderFlow = OrderFlow?.Copy(), NeighborhoodLife = NeighborhoodLife?.Copy() };
    }
}
