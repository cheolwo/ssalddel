using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    public sealed class 로컬생활보고
    {
        public string Schema { get; set; } = "offline-life-report.r1";
        public string Authority { get; set; } = "LocalSimulationOnly";
        public string BundleSha256 { get; set; } = "";
        public string SessionStableId { get; set; } = "";
        public long Revision { get; set; }
        public int Tick { get; set; }
        public string ReplayHash { get; set; } = "";
        public string ContentSha256 { get; set; } = "";
        public string RecordCoverage { get; set; } = "ExistingLedgerTailWithCheckpoint_NotEntireSessionAudit";
        public string[] RuleSources { get; set; } = new[] { "BusinessWorkflowRuleEngine", "창고출고배분Policy", "LocalSimulationRuntime" };
        public int FoodReceived { get; set; }
        public int MartReceived { get; set; }
        public int ReplenishmentReceived { get; set; }
        public Simulation행위기록LedgerSnapshot? ActionLedger { get; set; }
        public 경영SimulationSessionSnapshot State { get; set; } = new 경영SimulationSessionSnapshot();

        // 기존 원장을 읽는 보고 사본이다. 기록 성공을 다시 업무 성공 명령으로 실행하지 않는다.
        public static 로컬생활보고 Create(SimulationSessionSavePackage package, 로컬생활시작묶음 bundle)
        {
            로컬생활시작자료.Validate(bundle);
            로컬생활시작자료.Require(package.SessionCreateRequest.LocalLife?.SourceBundleSha256 == bundle.ContentSha256,
                "OfflineLifeSaveBundleMismatch");
            var state=package.Snapshot;var fleet=state.WaitingFleet ?? throw new InvalidOperationException("OfflineLifeFleetMissing");
            var r = new 로컬생활보고 {BundleSha256=bundle.ContentSha256,SessionStableId=state.SessionStableId,Revision=state.Revision,
                Tick=state.CurrentTick,ReplayHash=package.ReplayHash,State=로컬생활시작자료.Copy(state),
                ActionLedger=로컬생활시작자료.Copy(package.ActionManifestationLedger),
                FoodReceived=state.FoodDeliveries.Count(x=>x.ReceivedTick.HasValue),MartReceived=fleet.Mart!.Orders.Count(x=>x.ReceivedTick.HasValue),
                ReplenishmentReceived=fleet.NeighborhoodLife!.Depot.Shipments.Count(x=>x.ReceivedTick.HasValue)};
            r.ContentSha256=Hash(r);return r;
        }
        private static string Hash(로컬생활보고 report) {var copy=로컬생활시작자료.Copy(report);copy.ContentSha256="";
            return 로컬생활시작자료.CanonicalHash(로컬생활시작자료.JsonBytes(copy));}
        public static void Validate(로컬생활보고 report)
        {
            로컬생활시작자료.Require(report.Schema=="offline-life-report.r1" && report.Authority=="LocalSimulationOnly"
                && report.ContentSha256==Hash(report) && 로컬생활시작자료.IsHash(report.BundleSha256)
                && report.SessionStableId==report.State.SessionStableId && report.Tick==report.State.CurrentTick
                && report.Revision==report.State.Revision,"OfflineLifeReportInvalid");
            var fleet=report.State.WaitingFleet;
            로컬생활시작자료.Require(fleet?.NeighborhoodLife != null && fleet.Mart != null && report.Tick>=0 && report.Tick<=1800
                && report.FoodReceived==report.State.FoodDeliveries.Count(x=>x.ReceivedTick.HasValue)
                && report.MartReceived==fleet!.Mart!.Orders.Count(x=>x.ReceivedTick.HasValue)
                && report.ReplenishmentReceived==fleet.NeighborhoodLife!.Depot.Shipments.Count(x=>x.ReceivedTick.HasValue),"OfflineLifeReportSummaryMismatch");
        }
    }
}
