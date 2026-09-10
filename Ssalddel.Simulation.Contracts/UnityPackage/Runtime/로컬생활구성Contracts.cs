using System;
using System.Linq;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>기존 합성 동네의 명시적 실행 입력. 운영 권한이나 현실 관측의 자동 승인이 아니다.</summary>
    public sealed class 로컬생활구성
    {
        public string Revision { get; set; } = "offline-life.r1";
        public string SourceBundleSha256 { get; set; } = "";
        public 로컬생활객체결속[] Actors { get; set; } = Array.Empty<로컬생활객체결속>();
        public 로컬생활시설결속[] Facilities { get; set; } = Array.Empty<로컬생활시설결속>();
        public 로컬생활구성 Copy() => new 로컬생활구성 { Revision = Revision,
            SourceBundleSha256 = SourceBundleSha256,
            Actors = Actors.Select(x => x.Copy()).ToArray(), Facilities = Facilities.Select(x => x.Copy()).ToArray() };
    }

    public sealed class 로컬생활객체결속
    {
        // SlotKey는 기존 동네의 업무 위치이지 객체의 고유 ID가 아니다.
        public string SlotKey { get; set; } = "";
        public string ActorStableId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public string VisualKey { get; set; } = "local-life:actor";
        public 로컬생활객체결속 Copy() => (로컬생활객체결속)MemberwiseClone();
    }

    public sealed class 로컬생활시설결속
    {
        public string SlotKey { get; set; } = "";
        public string FacilityStableId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public 로컬생활시설결속 Copy() => (로컬생활시설결속)MemberwiseClone();
    }
}
