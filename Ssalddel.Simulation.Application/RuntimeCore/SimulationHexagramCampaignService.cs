using System;
using System.Collections.Concurrent;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "이야기 재시도 번호와 저장본 복원 허용 여부를 관리하는 계약을 정의한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E1저장재생계약,
        Boundary = "계약만으로 영속 저장·서버 동시성·저장 재생 성공을 증명하지 않는다.")]
    public interface ISimulationHexagramCampaignAttemptStore
    {
        int RegisterEntry(string sessionStableId, string hexagramStableId,
            string entrySaveStableId);
        int NextAttempt(string sessionStableId, string hexagramStableId);
        void RegisterSave(string saveStableId, string sessionStableId,
            string hexagramStableId, int attemptOrdinal);
        void EnsureRestoreAllowed(string saveStableId,
            SimulationHexagramCampaignStateSnapshot? packageState);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "프로세스 내 이야기 재시도 번호와 저장본의 유효성을 기록·검사한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세션실행,
        Boundary = "메모리 저장이며 프로세스 재시작·분산 Host의 내구성을 보장하지 않는다.")]
    public sealed class InMemorySimulationHexagramCampaignAttemptStore
        : ISimulationHexagramCampaignAttemptStore
    {
        private readonly ConcurrentDictionary<string, int> attempts = new(
            StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SaveAttempt> saves = new(
            StringComparer.Ordinal);

        public int RegisterEntry(string sessionStableId, string hexagramStableId,
            string entrySaveStableId)
        {
            var key = Key(sessionStableId, hexagramStableId);
            var attempt = attempts.AddOrUpdate(key, 1,
                (_, current) => Math.Max(1, current));
            saves[entrySaveStableId] = new SaveAttempt(key, 1);
            return attempt;
        }

        public int NextAttempt(string sessionStableId, string hexagramStableId)
            => attempts.AddOrUpdate(Key(sessionStableId, hexagramStableId), 2,
                (_, current) => checked(current + 1));

        public void RegisterSave(string saveStableId, string sessionStableId,
            string hexagramStableId, int attemptOrdinal)
        {
            if (string.IsNullOrWhiteSpace(hexagramStableId)
                || attemptOrdinal <= 0) return;
            var key = Key(sessionStableId, hexagramStableId);
            attempts.AddOrUpdate(key, attemptOrdinal,
                (_, current) => Math.Max(current, attemptOrdinal));
            saves[saveStableId] = new SaveAttempt(key, attemptOrdinal);
        }

        public void EnsureRestoreAllowed(string saveStableId,
            SimulationHexagramCampaignStateSnapshot? packageState)
        {
            if (packageState == null
                || string.IsNullOrWhiteSpace(packageState.HexagramStableId)
                || packageState.AttemptOrdinal <= 0) return;
            var key = saves.TryGetValue(saveStableId, out var recorded)
                ? recorded.Key
                : Key(string.Empty, packageState.HexagramStableId);
            if (attempts.TryGetValue(key, out var current)
                && packageState.AttemptOrdinal < current)
                throw new SimulationConflictException(
                    "HexagramCampaignSaveAttemptInvalidated");
        }

        private static string Key(string sessionStableId, string hexagramStableId)
            => (sessionStableId ?? string.Empty).Trim() + "|"
                + (hexagramStableId ?? string.Empty).Trim();

        // Unity의 .NET Standard 소비 경로는 IsExternalInit을 제공하지 않는다.
        // 값 비교나 with 복사를 사용하지 않는 내부 저장 항목만 불변 클래스로 둔다.
        private sealed class SaveAttempt
        {
            public string Key { get; }
            public int AttemptOrdinal { get; }

            public SaveAttempt(string key, int attemptOrdinal)
            {
                Key = key;
                AttemptOrdinal = attemptOrdinal;
            }
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "이야기 진입·단계 진행·재시도와 권위 세션 저장·복원을 조율한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행,
        Boundary = "실제 플레이 완주·Hosted 연결·E 승격은 별도 검증한다.")]
    public sealed class SimulationHexagramCampaignService
    {
        private readonly 경영SimulationSessionAccessor sessions;
        private readonly 경영SimulationSession생명주기Service lifecycle;
        private readonly ISimulationHexagramCampaignAttemptStore attempts;

        public SimulationHexagramCampaignService(
            경영SimulationSessionAccessor sessions,
            경영SimulationSession생명주기Service lifecycle,
            ISimulationHexagramCampaignAttemptStore attempts)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        }

        public SimulationHexagramCampaignStateSnapshot Get(string sessionStableId)
            => sessions.Require(sessionStableId).GetHexagramCampaignState();

        public SimulationHexagramCampaignStateSnapshot Enter(
            string sessionStableId, SimulationHexagramCampaignEnterRequest request)
        {
            var entrySaveStableId = "simulation-save:campaign-entry:"
                + sessionStableId.Trim() + ":" + request.HexagramStableId.Trim();
            var state = sessions.Require(sessionStableId)
                .BeginHexagramCampaign(request, entrySaveStableId);
            var package = lifecycle.Save(sessionStableId,
                new SimulationSessionSaveRequest
                {
                    SaveStableId = entrySaveStableId,
                    ExpectedRevision = state.EntryWorldRevision,
                });
            attempts.RegisterEntry(sessionStableId, state.HexagramStableId,
                package.SaveStableId);
            return state;
        }

        public SimulationHexagramCampaignStateSnapshot CompleteLine(
            string sessionStableId,
            SimulationHexagramCampaignLineCompleteRequest request)
            => sessions.Require(sessionStableId).CompleteHexagramLine(request);

        public SimulationHexagramCampaignStateSnapshot RecordSetback(
            string sessionStableId,
            SimulationHexagramCampaignSetbackRequest request)
            => sessions.Require(sessionStableId).RecordHexagramSetback(request);

        public SimulationHexagramCampaignStateSnapshot Fail(
            string sessionStableId,
            SimulationHexagramCampaignFailureRequest request)
        {
            var current = sessions.Require(sessionStableId);
            var entrySaveStableId = current.ValidateHexagramCampaignFailure(request);
            var before = current.GetHexagramCampaignState();
            var nextAttempt = attempts.NextAttempt(sessionStableId,
                before.HexagramStableId);
            lifecycle.RestoreForCampaignRetry(new SimulationSessionRestoreRequest
            {
                SaveStableId = entrySaveStableId,
            }, request.ExpectedRevision);
            return sessions.Require(sessionStableId)
                .RestartHexagramCampaign(request, nextAttempt);
        }

        public SimulationHexagramCampaignStateSnapshot Complete(
            string sessionStableId,
            SimulationHexagramCampaignCompleteRequest request)
            => sessions.Require(sessionStableId)
                .CompleteHexagramCampaign(request);
    }
}
