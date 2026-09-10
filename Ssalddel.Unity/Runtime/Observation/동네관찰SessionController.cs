using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Observation
{
    /// <summary>기존 Session에 대한 입력 경계. 구현은 Runtime Adapter가 소유한다.</summary>
    public interface I동네관찰SessionPort
    {
        Task<경영SimulationSessionSnapshot> 조회Async();
        Task 진행Async(long revision);
        Task 정책Async(SimulationNpcPolicyChangeRequest request);
        Task 저장Async(long revision);
        Task 재시작Async();
    }

    /// <summary>한 번에 하나의 명령과 저장. View 수명·패널 상태는 Simulation 시간이 아니다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "동네 관찰 입력·조회·저장 순서 조정", Boundary = "기존 Session 권위 유지. UI와 저장 성공은 E 승격 근거가 아니다")]
    public sealed class 동네관찰SessionController : IDisposable
    {
        private readonly I동네관찰SessionPort port;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private double elapsed;
        private bool disposed;
        private bool timedOperation;
        public 경영SimulationSessionSnapshot State { get; private set; }
        public bool Playing { get; private set; }
        public bool Busy { get; private set; }
        public bool CommandPending { get; private set; }
        public bool SaveBlocked { get; private set; }
        public string Message { get; private set; } = "정지 · 재생을 눌러 시작하세요";
        public double LastSaveMs { get; private set; }
        public double PeakSaveMs { get; private set; }
        public double LastOperationMs { get; private set; }
        public int SaveCount { get; private set; }
        public event Action? Changed;

        public 동네관찰SessionController(I동네관찰SessionPort port, 경영SimulationSessionSnapshot initial)
        { this.port = port ?? throw new ArgumentNullException(nameof(port)); State = initial ?? throw new ArgumentNullException(nameof(initial)); }

        public void 재생전환()
        {
            if (disposed || SaveBlocked || State.IsCompleted) return;
            Playing = !Playing; elapsed = 0; Notify();
        }
        public void 정지() { Playing = false; elapsed = 0; Notify(); }
        public Task<bool> 시간진행Async(double deltaSeconds)
        {
            if (disposed || !Playing || SaveBlocked || State.IsCompleted || CommandPending) { elapsed = 0; return Task.FromResult(false); }
            // 저장 대기 시간도 현재 한 주기에 포함하되 밀린 여러 Tick을 쌓지는 않는다.
            // 정책/수동 저장, 앱 정지 시간은 따라잡지 않는다.
            if (Busy && !timedOperation) { elapsed = 0; return Task.FromResult(false); }
            elapsed = Math.Min(1, elapsed + Math.Max(0, Math.Min(deltaSeconds, 1)));
            if (Busy) return Task.FromResult(false);
            if (elapsed < 1) return Task.FromResult(false);
            elapsed = 0;
            return Execute(async () => await port.진행Async(State.Revision), false, false, true);
        }
        public Task<bool> 한초진행Async() { 정지(); return Execute(async () => await port.진행Async(State.Revision), false, false); }
        public Task<bool> 저장Async() => Execute(null, true, false);
        public Task<bool> 정책적용Async(SimulationNpcPolicyChangeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Execute(async () => {
                request.ExpectedRevision = State.Revision;
                request.CommandId = "command:observer-policy:" + Guid.NewGuid().ToString("N");
                await port.정책Async(request);
            }, false, false);
        }
        public Task<bool> 재시작Async(bool confirmed) => !confirmed || !State.IsCompleted
            ? Task.FromResult(false) : Execute(() => port.재시작Async(), false, true);

        private async Task<bool> Execute(Func<Task>? command, bool recovery, bool restart, bool timed = false)
        {
            if (disposed || (!recovery && SaveBlocked) || (!recovery && !restart && State.IsCompleted)) return false;
            if (timed) { if (CommandPending || !await gate.WaitAsync(0)) return false; }
            else
            {
                // 명시적 요청 한 건은 진행 중 저장 뒤로 넘긴다. 반복 클릭은 더 쌓지 않는다.
                if (CommandPending) return false;
                CommandPending = true; elapsed = 0; Notify();
                await gate.WaitAsync();
            }
            var operation = Stopwatch.StartNew();
            try
            {
                // 대기하는 동안 종료·저장 실패가 발생했을 수 있다.
                if (disposed || (!recovery && SaveBlocked) || (!recovery && !restart && State.IsCompleted)) return false;
                Busy = true; timedOperation = timed; elapsed = 0;
                if (restart) Playing = false;
                Notify();
                if (command != null) await command();
                State = await port.조회Async();
                var save = Stopwatch.StartNew();
                try { await port.저장Async(State.Revision); }
                finally { LastSaveMs = save.Elapsed.TotalMilliseconds; PeakSaveMs = Math.Max(PeakSaveMs, LastSaveMs); }
                SaveCount++; SaveBlocked = false;
                if (State.IsCompleted) Playing = false;
                Message = restart ? "새 표본 준비 · 재생을 눌러 시작하세요" : "저장 완료";
                return true;
            }
            catch (Exception)
            {
                // 외부 오류 본문에는 경로/자격 증명이 있을 수 있어 UI에 그대로 노출하지 않는다.
                Playing = false; SaveBlocked = true;
                Message = "진행 또는 저장 실패 · 정지됨. 저장을 다시 시도하세요";
                return false;
            }
            finally
            {
                LastOperationMs = operation.Elapsed.TotalMilliseconds;
                Busy = false; timedOperation = false;
                if (!timed) CommandPending = false;
                if (!timed || !Playing || SaveBlocked) elapsed = 0;
                gate.Release(); Notify();
            }
        }
        private void Notify() { if (!disposed) Changed?.Invoke(); }
        public void Dispose() { disposed = true; Playing = false; Changed = null; elapsed = 0; }
    }
}
