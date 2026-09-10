using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WorkflowRules
{
    /// <summary>상태 조회는 호출자가 수행한다. 권역 단계: 동일0, 인접1, 제한 확장2.</summary>
    public sealed class 음식배달기사후보
    {
        public string 기사Id { get; }
        public int 권역단계 { get; }
        public decimal 거리Km { get; }
        public decimal 소요분 { get; }
        public decimal 대기보정 { get; }
        public decimal 허용반경Km { get; }
        public bool 가용 { get; }
        public bool 위치유효 { get; }
        public bool 경로존재 { get; }
        public 음식배달기사후보(string id, int area, decimal distance, decimal minutes,
            decimal aging, decimal radius, bool available = true, bool fresh = true, bool route = true)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("DriverIdRequired", nameof(id));
            if (area < 0 || area > 2 || distance < 0 || minutes < 0 || radius < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            기사Id = id; 권역단계 = area; 거리Km = distance; 소요분 = minutes;
            대기보정 = aging; 허용반경Km = radius; 가용 = available; 위치유효 = fresh; 경로존재 = route;
        }
    }

    public sealed class 음식배달후보평가
    {
        public 음식배달기사후보 후보 { get; }
        public 음식배달픽업평가? 평가 { get; }
        public string 제외사유 { get; }
        public bool 적격 => 제외사유.Length == 0;
        internal 음식배달후보평가(음식배달기사후보 candidate, 음식배달픽업평가? score, string reason)
        { 후보 = candidate; 평가 = score; 제외사유 = reason; }
    }

    public sealed class 음식배달후보선정결과
    {
        public IReadOnlyList<음식배달후보평가> 평가목록 { get; }
        public string? 선택기사Id { get; }
        internal 음식배달후보선정결과(IEnumerable<음식배달후보평가> items, string? selected)
        { 평가목록 = Array.AsReadOnly(items.ToArray()); 선택기사Id = selected; }
    }

    /// <summary>추천만 반환한다. 확정·점유·운영 알림·DB 쓰기를 수행하지 않는다.</summary>
    public static class 음식배달후보선정Policy
    {
        public static 음식배달후보선정결과 판정(IEnumerable<음식배달기사후보> candidates,
            DateTime now, DateTime? readyAt, decimal searchRadiusKm,
            int minimumSameArea = 3, decimal earlyMinutes = 3, decimal lateMinutes = 5)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (searchRadiusKm < 0 || minimumSameArea < 1) throw new ArgumentOutOfRangeException(nameof(searchRadiusKm));
            var items = candidates.OrderBy(x => x.기사Id, StringComparer.Ordinal).ToArray();
            if (items.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("DuplicateDriverId", nameof(candidates));
            string Excluded(음식배달기사후보 x) => !x.가용 ? "DriverUnavailable" : !x.위치유효 ? "LocationStale"
                : !x.경로존재 ? "NoTraversableRoute"
                : x.거리Km > Math.Min(searchRadiusKm, x.허용반경Km) ? "ApproachRadiusExceeded" : "";
            var eligible = items.Where(x => Excluded(x).Length == 0).ToArray();
            var same = eligible.Count(x => x.권역단계 == 0);
            var maximumArea = same >= minimumSameArea ? 0 : 1;
            if (!eligible.Any(x => x.권역단계 <= maximumArea)) maximumArea = 2;
            var results = items.Select(x =>
            {
                var reason = Excluded(x);
                if (reason.Length == 0 && x.권역단계 > maximumArea) reason = "SearchAreaNotExpanded";
                return new 음식배달후보평가(x, reason.Length == 0
                    ? 음식배달픽업평가Policy.예상소요시간판정(x.거리Km, x.대기보정, readyAt, now,
                        x.소요분, earlyMinutes, lateMinutes) : null, reason);
            }).ToArray();
            var best = 음식배달픽업평가Policy.정렬(results.Where(x => x.적격), x => x.평가!,
                x => x.후보.권역단계, x => x.후보.거리Km, x => x.후보.기사Id).FirstOrDefault();
            return new 음식배달후보선정결과(results, best?.후보.기사Id);
        }
    }
}
