using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Warehouse
{
    public sealed class 창고정책편집
    {
        public string PolicyStableId { get; }
        internal object Owner { get; }
        public long ExpectedRevision { get; }
        public bool AutomationEnabled { get; set; }
        public int Priority { get; set; }
        public string PreferredActorStableId { get; set; }
        public bool AutoDelegationEnabled { get; set; }
        internal 창고정책편집(SimulationNpcWorkPolicySnapshot policy, long revision, object owner)
        {
            Owner = owner;
            PolicyStableId = policy.PolicyStableId; ExpectedRevision = revision;
            AutomationEnabled = policy.AutomationEnabled; Priority = policy.Priority;
            PreferredActorStableId = policy.PreferredActorStableId;
            AutoDelegationEnabled = policy.AutoDelegationEnabled;
        }
    }

    /// <summary>카드 입력은 초안만 변경한다. 적용 후 권위 재조회로만 화면을 갱신한다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "창고 정책 편집 초안과 권위 조회·변경·재조회 포트를 연결한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2Unity권위Client,
        Boundary = "Unity 화면이나 창고 업무 완료를 대신하지 않고 운영 원장을 직접 변경하지 않는다.")]
    public sealed class 창고정책카드Presenter
    {
        private readonly ISimulationNpcPolicyRuntime runtime;
        private readonly string sessionId;
        private readonly string facilityId;
        private readonly bool includeOutbound;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private 경영SimulationSessionSnapshot? snapshot;
        public string 마지막안내 { get; private set; } = "조회 전";
        public bool 조회됨 => snapshot != null;

        public 창고정책카드Presenter(ISimulationNpcPolicyRuntime runtime, string sessionId, string facilityId,
            bool includeOutbound = false)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.sessionId = sessionId; this.facilityId = facilityId;
            this.includeOutbound = includeOutbound;
        }

        private SimulationNpcWorkPolicySnapshot[] 정책들()
            => snapshot == null ? Array.Empty<SimulationNpcWorkPolicySnapshot>()
                : snapshot.NpcWorkPolicies.Where(policy => policy.FacilityStableId == facilityId
                    && (policy.ActionCode == SimulationNpcActionCodes.WarehouseInboundInspection
                        || policy.ActionCode == SimulationNpcActionCodes.WarehouseStorageMove
                        || (includeOutbound && policy.ActionCode == SimulationNpcActionCodes.WarehouseOutboundFlow)))
                    .OrderBy(policy => policy.ActionCode, StringComparer.Ordinal).ToArray();

        public string[] 카드목록 => 정책들().Select(policy => policy.PolicyStableId).ToArray();
        public string 제목(string policyId)
        {
            var action = 정책들().Single(policy => policy.PolicyStableId == policyId).ActionCode;
            return action == SimulationNpcActionCodes.WarehouseInboundInspection ? "입고 검수"
                : action == SimulationNpcActionCodes.WarehouseOutboundFlow ? "출고·피킹·포장" : "창고 적치";
        }
        public 창고정책편집 편집시작(string policyId)
            => new 창고정책편집(정책들().Single(policy => policy.PolicyStableId == policyId), snapshot!.Revision, this);

        public string[] 작업상태 => snapshot == null ? Array.Empty<string>()
            : snapshot.NpcTaskAssignments.Where(value => value.FacilityStableId == facilityId)
                .Select(value => value.TaskStableId + " · " + value.ActorStableId + " · "
                    + 단계표시(value.PhaseCode) + (value.BlockReasonCodes.Length == 0 ? ""
                        : " · " + string.Join(", ", value.BlockReasonCodes))).ToArray();
        public string[] 재고상태 => snapshot == null ? Array.Empty<string>()
            : snapshot.NpcFacilityInventories.Where(value => value.FacilityStableId == facilityId)
                .Select(value => value.InventoryStableId + " · " + value.Quantity + " " + value.UnitCode
                    + " · " + 재고표시(value.StateCode)).ToArray();

        public async Task 조회Async(CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try { await 재조회Async(token); }
            finally { gate.Release(); }
        }

        public async Task 적용Async(창고정책편집 draft, string commandId, CancellationToken token = default)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (!ReferenceEquals(draft.Owner, this))
                throw new InvalidOperationException("다른 창고 화면에서 만든 초안은 적용할 수 없습니다.");
            await gate.WaitAsync(token);
            try
            {
                if (!정책들().Any(policy => policy.PolicyStableId == draft.PolicyStableId))
                    throw new InvalidOperationException("현재 창고 화면에서 선택한 업무 정책만 변경할 수 있습니다.");
                var request = new SimulationNpcPolicyChangeRequest
                {
                    CommandId = commandId, ExpectedRevision = draft.ExpectedRevision,
                    PolicyStableId = draft.PolicyStableId, AutomationEnabled = draft.AutomationEnabled,
                    Priority = draft.Priority, PreferredActorStableId = draft.PreferredActorStableId,
                    AutoDelegationEnabled = draft.AutoDelegationEnabled,
                };
                try { await runtime.UpdateNpcPolicyAsync(sessionId, request, token); }
                catch
                {
                    snapshot = null;
                    try { await 재조회Async(token); } catch { snapshot = null; }
                    마지막안내 = "적용을 확인하지 못했습니다. 최신 상태를 확인하고 다시 선택하세요.";
                    throw;
                }
                await 재조회Async(token);
                마지막안내 = "정책 적용 후 최신 상태를 조회했습니다. 진행 중 작업은 기존 규칙을 따릅니다.";
            }
            finally { gate.Release(); }
        }

        private async Task 재조회Async(CancellationToken token)
        {
            snapshot = null;
            마지막안내 = "조회 중";
            try { snapshot = await runtime.GetPolicySessionAsync(sessionId, token); }
            catch
            {
                마지막안내 = "최신 상태를 조회하지 못했습니다. 조회 후 다시 확인하세요.";
                throw;
            }
            마지막안내 = "최신 정책";
        }
        private static string 단계표시(string code)
        {
            switch (code)
            {
                case "Candidate": return "후보";
                case "Scheduled": return "대기";
                case "Navigating": return "이동 중";
                case "Working": return "작업 중";
                case "Completed": return "완료";
                case "Blocked": return "차단";
                case "Cancelled": return "취소";
                default: return code;
            }
        }
        private static string 재고표시(string code)
        {
            switch (code)
            {
                case "PendingInspection": return "검수 대기";
                case "StorageEligible": return "적치 가능";
                case "PutAwayCompleted": return "적치 완료";
                case "OutboundRequested": return "출고 요청";
                case "Picked": return "피킹 완료";
                case "OutboundReady": return "포장 완료·출고 대기";
                default: return code;
            }
        }
    }
}
