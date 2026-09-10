using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WorkflowRules
{
    /// <summary>후보 조회·예약·저장 없이 기존 출고 엔진의 배분 순서를 계산한다.</summary>
    public static class 창고출고배분Policy
    {
        public static IReadOnlyList<(int LineIndex, T Candidate, int Quantity)> 판정<T>(
            IReadOnlyList<int> quantities, IReadOnlyList<IReadOnlyList<T>> candidates,
            Func<T, long> stockId, Func<T, long> warehouseId,
            Func<T, int> available, Func<T, decimal> score, Func<T, bool> serviceArea,
            IReadOnlyList<T>? allCandidates = null)
        {
            if (quantities == null || candidates == null || quantities.Count != candidates.Count)
                throw new ArgumentException("출고 라인과 후보 목록이 일치해야 합니다.");
            var all = allCandidates ?? candidates.SelectMany(value => value).ToArray();
            Dictionary<long, int> Remaining() => all.GroupBy(stockId)
                .ToDictionary(group => group.Key, group => group.Min(available));
            var best = new List<(int LineIndex, T Candidate, int Quantity)>();
            decimal? bestScore = null;
            if (quantities.Count > 1)
            {
                foreach (var warehouse in all.Select(warehouseId).Distinct())
                {
                    var remaining = Remaining();
                    var plan = new List<(int LineIndex, T Candidate, int Quantity)>();
                    for (var i = 0; i < quantities.Count; i++)
                    {
                        var eligible = candidates[i].Where(value => warehouseId(value) == warehouse
                            && remaining[stockId(value)] >= quantities[i])
                            .OrderByDescending(score).Take(1).ToArray();
                        if (eligible.Length == 0) break;
                        var selected = eligible[0];
                        remaining[stockId(selected)] -= quantities[i];
                        plan.Add((i, selected, quantities[i]));
                    }
                    if (plan.Count != quantities.Count) continue;
                    var total = plan.Sum(value => score(value.Candidate))
                        + (plan.Any(value => serviceArea(value.Candidate)) ? 100m : 0m);
                    // 동점은 기존 엔진처럼 입력 후보에서 먼저 발견한 창고를 유지한다.
                    if (!bestScore.HasValue || total > bestScore.Value)
                    {
                        bestScore = total;
                        best = plan;
                    }
                }
            }
            if (best.Count > 0) return best;
            var stock = Remaining();
            for (var i = 0; i < quantities.Count; i++)
            {
                var needed = quantities[i];
                foreach (var candidate in candidates[i])
                {
                    if (needed <= 0) break;
                    var quantity = Math.Min(stock[stockId(candidate)], needed);
                    if (quantity <= 0) continue;
                    best.Add((i, candidate, quantity));
                    stock[stockId(candidate)] -= quantity;
                    needed -= quantity;
                }
            }
            return best;
        }
    }
}
