using System;
using System.Collections.Generic;

namespace Ssalddel.Simulation.Application
{
    public enum 관찰세계제어방식
    {
        자율 = 0,
        직접 = 1
    }

    /// <summary>서버가 소유할 체크포인트 자료. 기존 세션 저장에 자동 삽입하지 않는다.</summary>
    public sealed class 관찰세계진행Snapshot
    {
        public string SchemaVersion { get; set; } = "observer-world-progress.v1";
        public string WorldId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public DateTimeOffset SettledAtUtc { get; set; }
        public bool Connected { get; set; }
        public DateTimeOffset? DisconnectedAtUtc { get; set; }
        public 관찰세계제어방식 Control { get; set; }
    }

    /// <summary>
    /// [StartedAtUtc, EndedAtUtc)의 실행 허용 계약. 실제 작업 성공·재고 변화는 실행기가 판정한다.
    /// 출력된 구간에서는 세계가 진행되며 개인 효과는 별도 허용 값으로 구분한다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "관찰 세계의 시간 구간별 분신·개인 NPC 실행 허용을 고정한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E1세션권위계약,
        Boundary = "시간 허용 계약이며 실제 Tick·개인 자원 소비·성장·저장을 실행하지 않는다.")]
    public sealed class 관찰세계실행구간
    {
        public DateTimeOffset StartedAtUtc { get; }
        public DateTimeOffset EndedAtUtc { get; }
        public TimeSpan Elapsed => EndedAtUtc - StartedAtUtc;
        public bool PlayerActivityAllowed { get; }
        public bool PlayerAutonomyAllowed { get; }
        public bool PersonalNpcWorkAllowed { get; }

        internal 관찰세계실행구간(DateTimeOffset start, DateTimeOffset end,
            bool player, bool autonomy, bool personalNpc)
        {
            StartedAtUtc = start.ToUniversalTime();
            EndedAtUtc = end.ToUniversalTime();
            PlayerActivityAllowed = player;
            PlayerAutonomyAllowed = autonomy;
            PersonalNpcWorkAllowed = personalNpc;
        }
    }

    /// <summary>
    /// 진행 구간의 허용량이지 작업 성공이나 지급 영수증이 아니다.
    /// 소비자는 실제 결과와 Next를 같은 트랜잭션으로 저장해야 한다.
    /// </summary>
    public sealed class 관찰세계진행구간
    {
        public 관찰세계진행Snapshot Next { get; }
        public TimeSpan WorldElapsed { get; }
        public TimeSpan PlayerActiveElapsed { get; }
        public TimeSpan PersonalNpcElapsed { get; }
        public bool PlayerAutonomyAllowed { get; }
        public bool PersonalWorkPaused { get; }
        public string ReasonCode { get; }
        public IReadOnlyList<관찰세계실행구간> ExecutionWindows { get; }

        internal 관찰세계진행구간(관찰세계진행Snapshot next, TimeSpan world,
            TimeSpan player, TimeSpan personalNpc, bool autonomy, bool paused, string reason,
            IReadOnlyList<관찰세계실행구간> executionWindows)
        {
            Next = next;
            WorldElapsed = world;
            PlayerActiveElapsed = player;
            PersonalNpcElapsed = personalNpc;
            PlayerAutonomyAllowed = autonomy;
            PersonalWorkPaused = paused;
            ReasonCode = reason;
            ExecutionWindows = executionWindows;
        }
    }

    /// <summary>
    /// 신뢰할 수 있는 서버 시각만 입력한다. Tick, 자원, 성장, 운영 DB를 직접 변경하지 않는다.
    /// 연결 수/임대 만료 판정은 호출자의 책임이며 화면 입력 유무로 접속을 판단하지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "세계·접속 분신·개인 NPC의 시간 인정 구간과 상한 규칙을 계산한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E1세션권위계약,
        Boundary = "허용량 계산일 뿐 실제 Tick·성장·자원 소비·체크포인트 저장을 실행하지 않는다.")]
    public static class 관찰세계진행Policy
    {
        public static readonly TimeSpan 개인누적상한 = TimeSpan.FromHours(24);

        public static 관찰세계진행구간 정산(관찰세계진행Snapshot state,
            string ownerId, DateTimeOffset serverNow)
        {
            검증(state, ownerId, serverNow);
            var next = 복사(state);
            next.SettledAtUtc = serverNow.ToUniversalTime();
            if (!state.Enabled)
                return new 관찰세계진행구간(next, TimeSpan.Zero, TimeSpan.Zero,
                    TimeSpan.Zero, false, true, "ObserverWorldDisabled", Array.Empty<관찰세계실행구간>());

            var elapsed = serverNow - state.SettledAtUtc;
            if (state.Connected)
                return new 관찰세계진행구간(next, elapsed, elapsed, elapsed,
                    state.Control == 관찰세계제어방식.자율, false, string.Empty,
                    실행구간분할(state, serverNow, elapsed));

            // 날짜 상한 덧셈 오버플로를 피하고 이미 정산한 구간은 다시 인정하지 않는다.
            var previousOffline = state.SettledAtUtc - state.DisconnectedAtUtc!.Value;
            var remaining = previousOffline < 개인누적상한
                ? 개인누적상한 - previousOffline : TimeSpan.Zero;
            var allowed = elapsed < remaining ? elapsed : remaining;
            var capped = serverNow - state.DisconnectedAtUtc.Value >= 개인누적상한;
            return new 관찰세계진행구간(next, elapsed, TimeSpan.Zero, allowed,
                false, capped, capped ? "PersonalOfflineAccrualLimitReached" : "PlayerOffline",
                실행구간분할(state, serverNow, allowed));
        }

        private static IReadOnlyList<관찰세계실행구간> 실행구간분할(
            관찰세계진행Snapshot state, DateTimeOffset until, TimeSpan personalNpc)
        {
            var start = state.SettledAtUtc;
            if (start == until) return Array.Empty<관찰세계실행구간>();
            if (state.Connected)
                return Array.AsReadOnly(new[] { new 관찰세계실행구간(start, until, true,
                    state.Control == 관찰세계제어방식.자율, true) });

            // 허용 시간은 검증된 경과 시간 이내이므로 날짜 상한을 직접 더하지 않는다.
            var personalUntil = start.ToUniversalTime() + personalNpc;
            if (personalNpc == TimeSpan.Zero)
                return Array.AsReadOnly(new[] { new 관찰세계실행구간(start, until, false, false, false) });
            var active = new 관찰세계실행구간(start, personalUntil, false, false, true);
            if (personalUntil == until) return Array.AsReadOnly(new[] { active });
            return Array.AsReadOnly(new[] { active,
                new 관찰세계실행구간(personalUntil, until, false, false, false) });
        }

        /// <summary>상태 변경 전 구간부터 정산한다. 반복 종료는 종료 시각을 갱신하지 않는다.</summary>
        public static 관찰세계진행구간 접속변경(관찰세계진행Snapshot state,
            string ownerId, DateTimeOffset serverNow, bool connected)
        {
            var result = 정산(state, ownerId, serverNow);
            if (state.Connected != connected)
            {
                result.Next.Connected = connected;
                result.Next.DisconnectedAtUtc = connected ? null : serverNow.ToUniversalTime();
                // 재접속은 관찰 기본으로 돌아간다. 종료 전 직접 조작 명령을 재개하지 않는다.
                result.Next.Control = 관찰세계제어방식.자율;
            }
            return result;
        }

        /// <summary>제어권 교체 전 실행 구간을 먼저 반환한다. 실제 실행 잠금은 소비자가 소유한다.</summary>
        public static 관찰세계진행구간 제어변경(관찰세계진행Snapshot state,
            string ownerId, DateTimeOffset serverNow, 관찰세계제어방식 control)
        {
            if (!Enum.IsDefined(typeof(관찰세계제어방식), control))
                throw new InvalidOperationException("ObserverWorldControlInvalid");
            var result = 정산(state, ownerId, serverNow);
            if (!state.Enabled || !state.Connected)
                throw new InvalidOperationException("ObserverWorldControlRequiresConnection");
            result.Next.Control = control;
            return result;
        }

        private static void 검증(관찰세계진행Snapshot state, string ownerId, DateTimeOffset now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.SchemaVersion != "observer-world-progress.v1")
                throw new InvalidOperationException("ObserverWorldSchemaUnsupported");
            if (string.IsNullOrWhiteSpace(state.WorldId) || string.IsNullOrWhiteSpace(state.OwnerId))
                throw new InvalidOperationException("ObserverWorldIdentityMissing");
            if (!string.Equals(state.OwnerId, ownerId, StringComparison.Ordinal))
                throw new InvalidOperationException("ObserverWorldOwnerMismatch");
            if (!Enum.IsDefined(typeof(관찰세계제어방식), state.Control))
                throw new InvalidOperationException("ObserverWorldControlInvalid");
            if (now < state.SettledAtUtc)
                throw new InvalidOperationException("ObserverWorldClockRegressed");
            if (state.Connected == state.DisconnectedAtUtc.HasValue
                || (state.DisconnectedAtUtc.HasValue && state.DisconnectedAtUtc > state.SettledAtUtc))
                throw new InvalidOperationException("ObserverWorldPresenceInvalid");
        }

        private static 관찰세계진행Snapshot 복사(관찰세계진행Snapshot state)
            => new 관찰세계진행Snapshot
            {
                SchemaVersion = state.SchemaVersion,
                WorldId = state.WorldId,
                OwnerId = state.OwnerId,
                Enabled = state.Enabled,
                SettledAtUtc = state.SettledAtUtc,
                Connected = state.Connected,
                DisconnectedAtUtc = state.DisconnectedAtUtc,
                Control = state.Control
            };
    }
}
