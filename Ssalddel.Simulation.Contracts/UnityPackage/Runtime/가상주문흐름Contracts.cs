using System;
using System.Linq;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "NPC 주문 주기와 원장 참조·응답·배차 연결 사본", Boundary = "주문 본문을 복제하지 않으며 운영 DB와 무관하다")]
    public sealed class 가상주문흐름Snapshot
    {
        public 가상주문자Snapshot[] Orderers { get; set; } = new[] {
            new 가상주문자Snapshot { Residence = "a", NextTick = 1 },
            new 가상주문자Snapshot { Residence = "b", NextTick = 31 } };
        public 가상주문연결Snapshot[] Entries { get; set; } = Array.Empty<가상주문연결Snapshot>();
        public 가상주문흐름Snapshot Copy() => new 가상주문흐름Snapshot {
            Orderers = Orderers.Select(x => x.Copy()).ToArray(), Entries = Entries.Select(x => x.Copy()).ToArray() };
    }
    public sealed class 가상주문자Snapshot
    {
        public string Residence { get; set; } = "";
        public int NextTick { get; set; }
        public int Sequence { get; set; }
        public 가상주문자Snapshot Copy() => (가상주문자Snapshot)MemberwiseClone();
    }
    public sealed class 가상주문연결Snapshot
    {
        public string OrderId { get; set; } = "";
        public string SourceKind { get; set; } = "Restaurant";
        public int SubmittedTick { get; set; }
        public int? ResponseTick { get; set; }
        public string ResponseCode { get; set; } = "Pending";
        public int? QueuedTick { get; set; }
        public string DriverId { get; set; } = "";
        public string State { get; set; } = "Inbox";
        public string WaitReason { get; set; } = "음식점 확인 대기";
        public 가상주문연결Snapshot Copy() => (가상주문연결Snapshot)MemberwiseClone();
    }
}
