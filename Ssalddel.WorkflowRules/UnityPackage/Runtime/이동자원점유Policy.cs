using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WorkflowRules
{
    public sealed class 이동자원진입요청
    {
        public string 기사Id { get; }
        public long 대기시작Tick { get; }
        public IReadOnlyList<string> 자원Ids { get; }
        public 이동자원진입요청(string driverId, long waitingTick, params string[] resources)
        {
            if (string.IsNullOrWhiteSpace(driverId) || waitingTick < 0 || resources == null || resources.Length == 0
                || resources.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("OccupancyRequestInvalid");
            기사Id = driverId; 대기시작Tick = waitingTick;
            자원Ids = Array.AsReadOnly(resources.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class 이동자원진입판정
    {
        public string 기사Id { get; }
        public bool 진입가능 { get; }
        public string 사유 { get; }
        internal 이동자원진입판정(string id, bool allowed, string reason)
        { 기사Id = id; 진입가능 = allowed; 사유 = reason; }
    }

    public sealed class 이동자원점유결과
    {
        public IReadOnlyDictionary<string, string> 점유후보 { get; }
        public IReadOnlyList<이동자원진입판정> 판정목록 { get; }
        internal 이동자원점유결과(Dictionary<string, string> owners, List<이동자원진입판정> decisions)
        {
            점유후보 = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(owners);
            판정목록 = decisions.AsReadOnly();
        }
    }

    /// <summary>
    /// 방향 중립 도로 구간/출입구 ID를 묶어 원자적 진입 후보를 계산한다.
    /// 기존 점유 해제·대기 위치·저장·권위 확정은 호출자 책임이다. 물리 충돌 검증이 아니다.
    /// </summary>
    public static class 이동자원점유Policy
    {
        public static 이동자원점유결과 판정(IReadOnlyDictionary<string, string> occupied,
            IEnumerable<이동자원진입요청> requests)
        {
            if (occupied == null) throw new ArgumentNullException(nameof(occupied));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (occupied.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
                throw new ArgumentException("OccupancyStateInvalid", nameof(occupied));
            var owners = occupied.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var sorted = requests.OrderBy(x => x.대기시작Tick).ThenBy(x => x.기사Id, StringComparer.Ordinal).ToArray();
            if (sorted.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
                throw new ArgumentException("DuplicateDriverRequest", nameof(requests));
            var pending = new HashSet<string>(StringComparer.Ordinal);
            var decisions = new List<이동자원진입판정>();
            foreach (var request in sorted)
            {
                var held = owners.Where(x => x.Value == request.기사Id).Select(x => x.Key).ToArray();
                // 이미 가진 자원은 반복 요청해도 동일하다. 부분 점유 상태에서 추가 획득하며 기다리지 않는다.
                var reason = held.Length > 0 && request.자원Ids.Any(x => !held.Contains(x, StringComparer.Ordinal))
                    ? "ReleaseBeforeNewReservation"
                    : request.자원Ids.Any(x => owners.TryGetValue(x, out var owner) && owner != request.기사Id)
                        ? "ResourceOccupied"
                        : held.Length == 0 && request.자원Ids.Any(pending.Contains) ? "EarlierWaiter" : "";
                if (reason.Length == 0)
                    foreach (var resource in request.자원Ids) owners[resource] = request.기사Id;
                else if (held.Length == 0)
                    foreach (var resource in request.자원Ids) pending.Add(resource);
                decisions.Add(new 이동자원진입판정(request.기사Id, reason.Length == 0, reason));
            }
            return new 이동자원점유결과(owners, decisions);
        }
    }
}
