using System;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Application
{
    /// <summary>프레임 시간을 Tick 요청 여부로 바꾼다. WorldTick이나 주문 상태의 권위는 없다.</summary>
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.SimulationSessionLifecycle, SsalddelCodeLayer.Application,
        "재생 중 경과 시간으로 단일 Tick 요청 시점을 준비한다.",
        StepKey = "application.observer-tick-request", FlowOrder = 15,
        ExecutionStage = SsalddelCodeExecutionStage.Preview, Effects = SsalddelCodeEffect.None,
        Boundary = "요청 준비만 하며 Runtime 호출·권위 Tick 진행·앱 종료 시간 따라잡기를 하지 않는다.")]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "재생 상태에서 1초당 최대 한 Tick 요청을 준비하고 정지·명령 대기 누적을 버린다.",
        Boundary = "Runtime 포트를 호출하지 않으며 종료·정지 시간 따라잡기를 하지 않는다.")]
    public sealed class 관찰시간누적기
    {
        private double remainingSeconds;

        public bool Tick요청(double elapsedSeconds, bool playing, bool commandInFlight = false)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!playing || commandInFlight)
            {
                remainingSeconds = 0;
                return false;
            }
            // 오래 멈춘 프레임도 한 Tick만 요청한다. 과거 시간을 큐로 쌓지 않는다.
            remainingSeconds += Math.Min(elapsedSeconds, 1);
            if (remainingSeconds < 1 - 1e-9) return false;
            remainingSeconds = Math.Max(0, remainingSeconds - 1);
            return true;
        }

        public void 초기화() => remainingSeconds = 0;
    }
}
