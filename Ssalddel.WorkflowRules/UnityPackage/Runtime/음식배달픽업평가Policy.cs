using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WorkflowRules
{
    public sealed class 음식배달픽업평가
    {
        public int 단계 { get; }
        public decimal 차이분 { get; }
        public string 설명 { get; }
        public decimal 추천점수 { get; }
        public 음식배달픽업평가(int stage, decimal gap, string description, decimal score)
        { 단계 = stage; 차이분 = gap; 설명 = description; 추천점수 = score; }
    }

    /// <summary>운영 시계/경로 조회를 호출하지 않는 음식배달 전용 픽업창 판정.</summary>
    public static class 음식배달픽업평가Policy
    {
        public static 음식배달픽업평가 판정(decimal distanceKm, decimal agingScore,
            DateTime? readyAt, DateTime now, decimal speedKmH, decimal earlyMinutes, decimal lateMinutes)
            => Evaluate(distanceKm, agingScore, readyAt, now,
                readyAt.HasValue ? distanceKm / Math.Max(1m, speedKmH) * 60m : 0m, earlyMinutes, lateMinutes);

        /// <summary>차량·보행 경로를 합산한 예상 소요분을 사용한다. 외부 시계나 경로를 조회하지 않는다.</summary>
        public static 음식배달픽업평가 예상소요시간판정(decimal distanceKm, decimal agingScore,
            DateTime? readyAt, DateTime now, decimal travelMinutes, decimal earlyMinutes, decimal lateMinutes)
        {
            if (distanceKm < 0 || travelMinutes < 0) throw new ArgumentOutOfRangeException(nameof(travelMinutes));
            return Evaluate(distanceKm, agingScore, readyAt, now, travelMinutes, earlyMinutes, lateMinutes);
        }

        private static 음식배달픽업평가 Evaluate(decimal distanceKm, decimal agingScore,
            DateTime? readyAt, DateTime now, decimal travelMinutes, decimal earlyMinutes, decimal lateMinutes)
        {
            int stage = 0;
            decimal gap = 0;
            var description = "조리예정시각 미확정";
            if (readyAt.HasValue)
            {
                var arrival = now.AddMinutes((double)travelMinutes);
                var start = readyAt.Value.AddMinutes(-(double)Math.Max(0m, earlyMinutes));
                var end = readyAt.Value.AddMinutes((double)Math.Max(0m, lateMinutes));
                if (arrival < start)
                {
                    stage = 1;
                    gap = Math.Max(0m, (decimal)(start - arrival).TotalMinutes);
                    description = $"조기 도착 대기 약 {gap:0}분";
                }
                else if (arrival > end)
                {
                    stage = 2;
                    gap = Math.Max(0m, (decimal)(arrival - end).TotalMinutes);
                    description = $"픽업창 지연 약 {gap:0}분";
                }
                else
                {
                    gap = Math.Abs((decimal)(arrival - readyAt.Value).TotalMinutes);
                    description = "적정 픽업창";
                }
            }
            return new 음식배달픽업평가(stage, gap, description,
                Math.Max(0m, 100m - distanceKm * 12m) + agingScore - (stage * 100m + gap));
        }

        public static IOrderedEnumerable<T> 정렬<T>(IEnumerable<T> candidates,
            Func<T, 음식배달픽업평가> evaluation, Func<T, int> areaStage,
            Func<T, decimal> distance, Func<T, string> driverId)
            => candidates.OrderBy(value => evaluation(value).단계)
                .ThenBy(value => evaluation(value).차이분).ThenBy(areaStage)
                .ThenByDescending(value => evaluation(value).추천점수)
                .ThenBy(distance).ThenBy(driverId, StringComparer.Ordinal);
    }
}
