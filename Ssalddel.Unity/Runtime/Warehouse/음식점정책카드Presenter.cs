using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Warehouse
{
    /// <summary>음식점 설정 카드의 데이터 연결. Scene/UI 오브젝트가 아닌 권위 조회·변경 어댑터다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "음식점 조리 정책의 권위 조회·변경 포트와 카드 표시 상태를 연결한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2Unity권위Client,
        Boundary = "실제 Unity 카드 화면·Scene·입력 완주나 E 승격을 증명하지 않는다.")]
    public sealed class 음식점정책카드Presenter
    {
        private readonly ISimulationNpcPolicyRuntime runtime;
        private readonly string sessionId;
        private readonly string facilityId;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private 경영SimulationSessionSnapshot? state;
        private SimulationNpcWorkPolicySnapshot? selectedPolicy;
        public string 안내 { get; private set; } = "조회 전";
        public const int 새설정기본자리수 = 1;
        public bool 조회됨 => state != null;
        public bool 편집가능 => selectedPolicy != null;
        public 음식점정책카드Presenter(ISimulationNpcPolicyRuntime runtime, string sessionId, string facilityId)
        { this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); this.sessionId = sessionId; this.facilityId = facilityId; }
        private SimulationNpcWorkPolicySnapshot 정책 => selectedPolicy ?? throw new InvalidOperationException(안내);
        public int? 조리자리수 => selectedPolicy?.CookingSlots;
        public int? 조리시간 => selectedPolicy?.WorkDurationTicks;
        public bool 자동조리배정 => selectedPolicy?.AutomationEnabled ?? false;
        public int? 현재Tick => state?.CurrentTick;
        public bool 진행가능 => state != null && state.CurrentTick < state.DurationTicks;
        public 음식점주문표시Model[] 주문목록 => state == null ? Array.Empty<음식점주문표시Model>() : state.FoodDeliveries
            .Where(value => value.RestaurantFacilityStableId == facilityId).OrderBy(value => value.AcceptedTick)
            .ThenBy(value => value.FoodOrderStableId, StringComparer.Ordinal)
            .Select(value => new 음식점주문표시Model(value, state.Tasks.FirstOrDefault(task => task.TaskStableId == value.TaskStableId))).ToArray();
        public string 설정상태 => selectedPolicy == null ? 안내 : !selectedPolicy.CookingSlots.HasValue
            ? "미설정·조리 대기" : !selectedPolicy.AutomationEnabled ? "새 조리 배정 중지" : "조리 설정 적용됨";
        public long 판본 => state?.Revision ?? throw new InvalidOperationException("먼저 조회해야 합니다.");
        public string[] 주문상태 => state == null ? Array.Empty<string>() : state.FoodDeliveries
            .Where(value => value.RestaurantFacilityStableId == facilityId).OrderBy(value => value.AcceptedTick)
            .ThenBy(value => value.FoodOrderStableId, StringComparer.Ordinal)
            .Select(value => value.FoodOrderStableId + " · " +
                (state.Tasks.Any(task => task.TaskStableId == value.TaskStableId && task.TaskTypeCode == "FoodOrderCookingQueued")
                    ? "조리 대기" : value.StateCode)).ToArray();
        private async Task 재조회(CancellationToken token)
        {
            state = null;
            selectedPolicy = null;
            안내 = "조회 중";
            try
            {
                var result = await runtime.GetPolicySessionAsync(sessionId, token);
                var candidates = result.NpcWorkPolicies.Where(value => value.FacilityStableId == facilityId
                    && value.ActionCode == SimulationNpcActionCodes.RestaurantCooking).ToArray();
                state = result;
                if (candidates.Length != 1)
                {
                    안내 = candidates.Length == 0 ? "음식점 조리 정책 없음·편집 불가" : "음식점 조리 정책 중복·편집 불가";
                    return;
                }
                selectedPolicy = candidates[0];
                안내 = "최신 상태 조회 완료";
            }
            catch
            {
                state = null;
                selectedPolicy = null;
                안내 = "조회 실패·새로고침 필요";
                throw;
            }
        }
        public async Task 조회Async(CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try { await 재조회(token); } finally { gate.Release(); }
        }
        public async Task 적용Async(string commandId, long expectedRevision, int slots, int durationTicks, bool enabled,
            CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try
            {
                var policy = 정책;
                try
                {
                    await runtime.UpdateNpcPolicyAsync(sessionId, new SimulationNpcPolicyChangeRequest
                    {
                        CommandId = commandId, ExpectedRevision = expectedRevision, PolicyStableId = policy.PolicyStableId,
                        CookingSlots = slots, CookingDurationTicks = durationTicks, AutomationEnabled = enabled,
                        Priority = policy.Priority, PreferredActorStableId = policy.PreferredActorStableId,
                        AutoDelegationEnabled = policy.AutoDelegationEnabled,
                    }, token);
                }
                catch
                {
                    try
                    {
                        await 재조회(token);
                        if (편집가능) 안내 = "설정 적용 실패·최신 상태를 확인하고 다시 편집하세요.";
                    }
                    catch { /* 조회 실패 시 편집을 차단하고 원래 명령 오류를 보존한다. */ }
                    throw;
                }
                await 재조회(token);
            }
            finally { gate.Release(); }
        }
    }

    /// <summary>업무 계약과 분리된 읽기 전용 화면 행. View가 문자열에서 업무 상태를 역추론하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "같은 권위 상태 사본의 음식 주문·작업을 한국어 표시 행으로 해석한다.",
        Boundary = "표시 모델은 상태 전이·조리 완료를 판정하지 않는다.")]
    public sealed class 음식점주문표시Model
    {
        public string 주문식별자 { get; }
        public string 권위상태코드 { get; }
        public string 표시상태 { get; }
        public int? 조리시작Tick { get; }
        public int? 조리완료예정Tick { get; }
        internal 음식점주문표시Model(Simulation음식배달Snapshot order, SimulationTaskSnapshot? task)
        {
            주문식별자 = order.FoodOrderStableId;
            권위상태코드 = order.StateCode;
            표시상태 = order.StateCode == "주문대기"
                ? string.IsNullOrEmpty(order.RestaurantResponseDecisionStableId) ? "접수 대기"
                    : task?.TaskTypeCode == "FoodOrderCookingQueued" ? "조리 대기"
                    : task?.TaskTypeCode == "FoodPreparationOnly" ? "조리 배정·시작 대기" : "조리 상태 확인 필요"
                : order.StateCode == "픽업대기" ? "픽업 준비" : order.StateCode;
            if (task?.TaskTypeCode == "FoodPreparationOnly")
            {
                조리시작Tick = task.ScheduledStartTick;
                조리완료예정Tick = task.ExpectedEndTick;
            }
        }
    }
}
