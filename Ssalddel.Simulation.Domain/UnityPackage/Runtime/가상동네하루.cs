using System;
using System.Linq;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private readonly bool neighborhoodDayEnabled;
        private static bool 하루생활대상(가상생활NpcSnapshot actor) => actor.Role == "Courier"
            || actor.ActorId == 가상동네생활기준.RestaurantActorId || actor.ActorId == 가상동네생활기준.MartWorkerId;

        private static void 하루주거배정(가상동네생활Snapshot life)
        {
            foreach (var resident in life.Actors.Where(x => x.Role == "Resident"))
            {
                resident.HomeFacilityId = "facility:synthetic:" + resident.ActorId.Substring(resident.ActorId.Length - 1);
                resident.HomeX = resident.WorkX; resident.HomeZ = resident.WorkZ;
                resident.DayHistory = new[] { "0 · 집에서 하루 시작" };
            }
            var index = 0;
            foreach (var worker in life.Actors.Where(하루생활대상).OrderBy(x => x.ActorId, StringComparer.Ordinal))
            {
                var home = index % 2 == 0 ? "a" : "b";
                worker.HomeFacilityId = "facility:synthetic:" + home;
                // 기존 주택 앞 공간의 서로 다른 생활 지점. 실내나 가족 관계를 새로 만들지 않는다.
                worker.HomeX = (home == "a" ? 30 : 10) + 1 + index / 2 * .8;
                worker.HomeZ = 24;
                worker.DayHistory = new[] { "0 · 일터에서 하루 시작" };
                worker.NextAction = "업무와 휴식 · 저녁에 신규 배정 마감";
                index++;
            }
        }

        private bool 하루퇴근진행(가상생활NpcSnapshot actor)
        {
            if (!Life.DayEnabled || !하루생활대상(actor) || CurrentTick < 1050) return false;
            if (actor.Stage == "Working" || actor.Stage == "Finishing")
            {
                actor.Stage = "Finishing";
                actor.Reason = "오늘 신규 배정 마감 · 맡은 업무를 끝낸 뒤 귀가";
                actor.NextAction = "업무 인계와 복귀가 끝나면 도보 귀가";
                if (LifeBusy(actor)) return true;
            }
            if (new[] { "Finishing", "Resting", "ReturningToRest", "ReturningToWork" }.Contains(actor.Stage))
            { actor.Stage = "GoingHome"; actor.HomeRouteStep = 0; }
            if (actor.Stage == "GoingHome")
            {
                actor.Reason = "기존 출입구와 도로를 따라 도보 귀가 · 차량은 주차 유지";
                actor.NextAction = "주택 앞 식사 후 휴식";
                if (하루귀가이동(actor))
                {
                    actor.Stage = "Eating"; actor.EatingUntilTick = CurrentTick + 30;
                    actor.Reason = "퇴근 후 식사 · 생활 표현, 주문/재고 추가 소비 없음";
                }
            }
            else if (actor.Stage == "Eating")
            {
                actor.Reason = "주택 앞 식사 · 생활 표현, 주문/재고 추가 소비 없음";
                actor.NextAction = "식사를 마치고 휴식";
                if (CurrentTick >= actor.EatingUntilTick) actor.Stage = "HomeRest";
            }
            if (actor.Stage == "HomeRest" || actor.Stage == "Sleeping")
            {
                actor.Stage = CurrentTick >= 1500 ? "Sleeping" : "HomeRest";
                actor.Reason = actor.Stage == "Sleeping" ? "밤 · 주택에서 수면 표시" : "주택 앞에서 하루를 마치고 휴식";
                actor.NextAction = "하루 종료 · 자동 재시작 없음";
                actor.TaskId = "";
            }
            return true;
        }

        // 업무 통로 → 기존 정차점 → 기존 도로 → 기존 주택 출입구. 새 길을 추론하지 않는다.
        private (double x, double z)[] 하루귀가경로(가상생활NpcSnapshot actor)
        {
            var home = actor.HomeFacilityId.EndsWith(":a", StringComparison.Ordinal) ? 30d : 10d;
            var points = new List<(double x, double z)> { (actor.WorkX, actor.WorkZ) };
            if (actor.ActorId == 가상동네생활기준.MartWorkerId)
                points.AddRange(가상동네배치기준.경로("mart-exit").Skip(1).Concat(new[] {
                    가상동네배치기준.기준점("junction-20"), 가상동네배치기준.기준점("home-junction-20"), (home,20d) }));
            else if (actor.Role == "RestaurantOperator")
                points.AddRange(가상동네배치기준.경로("restaurant-exit").Skip(1).Concat(new[] {
                    가상동네배치기준.기준점("junction-20"), 가상동네배치기준.기준점("home-junction-20"), (home,20d) }));
            else
            {
                var driver = waitingFleet!.Drivers.Single(x => x.Courier.ActorStableId == actor.ActorId);
                points.AddRange(FromHome(home, driver.Slot).Reverse().Skip(1));
            }
            points.AddRange(new[] { (home,22.5d), (home,24d), (actor.HomeX,actor.HomeZ) });
            return points.ToArray();
        }

        private bool 하루귀가이동(가상생활NpcSnapshot actor)
        {
            var route = 하루귀가경로(actor);
            if (actor.HomeRouteStep >= route.Length) return true;
            var target = route[actor.HomeRouteStep];
            var prior = actor.HomeRouteStep == 0 ? (actor.X,actor.Z) : route[actor.HomeRouteStep - 1];
            string[] resources;
            if (actor.HomeRouteStep == 0) resources = new[] { "life:work-access:" + actor.ActorId };
            else if (actor.HomeRouteStep >= route.Length - 2) resources = new[] { "entrance:" + actor.HomeFacilityId };
            else if (actor.ActorId == 가상동네생활기준.MartWorkerId && actor.HomeRouteStep <= 3)
                resources = new[] { "entrance:mart" };
            else if (actor.Role == "RestaurantOperator" && actor.HomeRouteStep == 1)
                resources = new[] { "entrance:restaurant" };
            else resources = LifeRouteResources(new[] { prior, target });
            if (!LifeReserve(actor.ActorId, resources))
            { actor.Reason = "귀가 통로 점유 · 해제 후 이동 재개"; return false; }
            if (!MoveLifeActor(actor, target.x, target.z)) return false;
            foreach (var resource in resources) Life.ResourceOwners.Remove(resource);
            Life.WaitingSince.Remove(actor.ActorId);
            actor.HomeRouteStep++;
            return actor.HomeRouteStep == route.Length;
        }

        private void 하루변화기록(가상생활NpcSnapshot actor, string previous)
        {
            if (previous == actor.Stage) return;
            actor.DayHistory = actor.DayHistory.Concat(new[] { CurrentTick + " · " + actor.Stage }).Reverse().Take(16).Reverse().ToArray();
            // 종전 휴식/복귀 분기에서 이미 기록한 전이는 중복 발행하지 않는다.
            if (actor.Stage != "Working" && actor.Stage != "Resting")
                RecordLife(actor.ActorId, "LIFE-SHIFT", actor.Stage, actor.HomeFacilityId);
        }

        private void 주민하루진행(가상생활NpcSnapshot actor, bool pending, bool approaching)
        {
            var receipt = foodDeliveries.Values.Where(x => x.OrdererStableId == actor.ActorId && x.ReceivedTick.HasValue)
                .Select(x => x.ReceivedTick!.Value).DefaultIfEmpty(-1).Max();
            if (receipt > actor.LastMealReceiptTick)
            {
                actor.LastMealReceiptTick = receipt; actor.EatingUntilTick = CurrentTick + 30;
                actor.DayHistory = actor.DayHistory.Concat(new[] { CurrentTick + " · 수령한 음식으로 식사" }).Reverse().Take(16).Reverse().ToArray();
                RecordLife(actor.ActorId, "LIFE-REST", "Eating", actor.HomeFacilityId);
            }
            if (approaching) { actor.NextAction = "출입구에서 주문 수령"; return; }
            actor.Stage = CurrentTick < actor.EatingUntilTick ? "Eating" : CurrentTick >= 1500 && !pending ? "Sleeping" : "AtHome";
            actor.Reason = actor.Stage == "Eating" ? "수령 기록에 따른 식사 · 이중 재고 차감 없음"
                : pending ? "미완료 주문 대기 · 수령은 생략하지 않음" : actor.Stage == "Sleeping" ? "밤 · 주택에서 수면 표시" : "집에서 생활";
            actor.NextAction = pending ? "주문 수령" : CurrentTick >= 1050 ? "휴식 · 오늘 신규 주문 마감" : "다음 주문 검토";
        }
    }
}
