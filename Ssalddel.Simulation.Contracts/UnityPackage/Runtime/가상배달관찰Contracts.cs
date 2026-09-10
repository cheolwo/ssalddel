namespace Ssalddel.Simulation.Contracts
{
    /// <summary>합성 관찰 프로필 전용. 실제 운영 기사·주소가 아니다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "기사 정체성·현재 주문·이동·물품 상태 사본을 정의한다.", Boundary = "합성 프로필 전용이며 실제 기사·주소가 아니다.")]
    public sealed class 가상배달기사Snapshot
    {
        public string ActorStableId { get; set; } = "actor:synthetic-courier:1";
        public string Stage { get; set; } = "Idle";
        public string OrderStableId { get; set; } = string.Empty;
        public int Batch { get; set; }
        public double Distance { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double VehicleX { get; set; }
        public double VehicleZ { get; set; }
        public bool Carrying { get; set; }
        public 가상배달기사Snapshot Copy() => (가상배달기사Snapshot)MemberwiseClone();
    }
}
