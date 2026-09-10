using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Presentation
{
    public readonly struct 배달평면위치
    {
        public double X { get; }
        public double Z { get; }
        public 배달평면위치(double x, double z) { X = x; Z = z; }
    }

    /// <summary>Scene 좌표가 아닌 E4 상대 배치 경로. 명시한 구간만 따라가며 지름길을 만들지 않는다.</summary>
    public sealed class 음식배달관찰경로
    {
        private readonly 배달평면위치[] points;
        private readonly double[] lengths;
        public string StableId { get; }
        public string Revision { get; }
        public double Length { get; }
        public 배달평면위치 Start => points[0];
        public 배달평면위치 End => points[points.Length - 1];

        public 음식배달관찰경로(string stableId, string revision, params 배달평면위치[] route)
        {
            if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(revision))
                throw new ArgumentException("DeliveryRouteIdentityMissing");
            if (route == null || route.Length < 2)
                throw new ArgumentException("DeliveryRouteDisconnected");
            if (route.Any(p => !Finite(p.X) || !Finite(p.Z)))
                throw new ArgumentException("DeliveryRouteCoordinateInvalid");
            StableId = stableId;
            Revision = revision;
            points = route.ToArray();
            lengths = new double[points.Length - 1];
            for (var i = 0; i < lengths.Length; i++)
            {
                var dx = points[i + 1].X - points[i].X;
                var dz = points[i + 1].Z - points[i].Z;
                lengths[i] = Math.Sqrt(dx * dx + dz * dz);
                if (!Finite(lengths[i]) || lengths[i] <= 0)
                    throw new ArgumentException("DeliveryRouteSegmentInvalid");
                Length += lengths[i];
            }
            if (!Finite(Length)) throw new ArgumentException("DeliveryRouteLengthInvalid");
        }

        public 배달평면위치 위치(double progress, bool blocked = false)
        {
            if (!Finite(progress) || progress < 0 || progress > 1)
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (blocked) throw new InvalidOperationException("DeliveryRouteBlocked");
            var remaining = progress * Length;
            for (var i = 0; i < lengths.Length; i++)
            {
                if (remaining <= lengths[i] || i == lengths.Length - 1)
                {
                    var t = Math.Min(1, remaining / lengths[i]);
                    return new 배달평면위치(points[i].X + (points[i + 1].X - points[i].X) * t,
                        points[i].Z + (points[i + 1].Z - points[i].Z) * t);
                }
                remaining -= lengths[i];
            }
            return End;
        }

        public 음식배달관찰경로 역방향(string stableId)
            => new 음식배달관찰경로(stableId, Revision, points.Reverse().ToArray());

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>주행과 현관 접근의 접속점을 검사한다. 결과는 배치 후보이며 실제 통행 증거가 아니다.</summary>
    public sealed class 음식배달관찰동선
    {
        public 음식배달관찰경로 VehicleRoute { get; }
        public 음식배달관찰경로 EntranceRoute { get; }
        public 음식배달관찰동선(음식배달관찰경로 vehicle, 음식배달관찰경로 entrance)
        {
            VehicleRoute = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            EntranceRoute = entrance ?? throw new ArgumentNullException(nameof(entrance));
            if (vehicle.End.X != entrance.Start.X || vehicle.End.Z != entrance.Start.Z)
                throw new ArgumentException("DeliveryStopEntranceDisconnected");
            if (vehicle.Revision != entrance.Revision)
                throw new ArgumentException("DeliveryRouteRevisionMismatch");
        }

        public 배달평면위치 정차위치 => VehicleRoute.End;
        public 배달평면위치 인계접근위치(double progress) => EntranceRoute.위치(progress);
    }

    public static class 음식배달관찰표본
    {
        public const string Revision = "food-delivery-neighborhood.r1";
        public static 음식배달관찰동선 음식점()
            => new 음식배달관찰동선(
                new 음식배달관찰경로("delivery-route:restaurant", Revision,
                    new 배달평면위치(0, 0), new 배달평면위치(10, 0)),
                new 음식배달관찰경로("delivery-entrance:restaurant", Revision,
                    new 배달평면위치(10, 0), new 배달평면위치(10, 4)));

        public static 음식배달관찰동선 주택(string residence)
        {
            if (residence != "A" && residence != "B")
                throw new ArgumentException("DeliveryResidenceUnknown");
            var x = residence == "A" ? 30 : 10;
            return new 음식배달관찰동선(
                new 음식배달관찰경로("delivery-route:residence-" + residence, Revision,
                    new 배달평면위치(10, 0), new 배달평면위치(20, 0),
                    new 배달평면위치(20, 20), new 배달평면위치(x, 20)),
                new 음식배달관찰경로("delivery-entrance:residence-" + residence, Revision,
                    new 배달평면위치(x, 20), new 배달평면위치(x, 24)));
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "음식배달 상태 사본의 표시값 변환 계약",
        Boundary = "읽기 전용 변환 코드; 실제 Unity 객체 결속·입력·이동·화면 증거 아님")]
    [Ssalddel.Contracts.Common.Metadata.SsalddelCodeMetadata(
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeFeatureKeys.FoodWorkflowLineage,
        Ssalddel.Contracts.Common.Metadata.SsalddelCodeLayer.ViewModel,
        "음식배달 상태 사본을 읽기 전용 표시 문구로 변환",
        StepKey = "unity.food-state-projection", FlowOrder = 30,
        ExecutionStage = Ssalddel.Contracts.Common.Metadata.SsalddelCodeExecutionStage.Presentation,
        ReadsFrom = Ssalddel.Contracts.Common.Metadata.SsalddelCodeDataScope.SimulationState,
        SourceCodeRefs = new[] { "Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs" },
        ReuseKind = "ProjectionConsumption",
        Adaptation = "Simulation음식배달Snapshot의 주문 ID/Revision/상태를 읽는다. 경로 이동·MonoBehaviour 생성·운영 서버 연결은 이 클래스의 책임이 아니다.",
        Boundary = "표현값만 생성; 원본 상태 변경·주문/배차/수령 확정 없음")]
    public sealed class 음식배달관찰상태
    {
        public string OrderId { get; }
        public long OrderRevision { get; }
        public string StateCode { get; }
        public string Description { get; }
        public bool ReceiptConfirmed { get; }
        public string BoundaryCode => "PresentationOnlyNotDriverOrRouteAuthority";

        public 음식배달관찰상태(Simulation음식배달Snapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.FoodOrderStableId) || source.Revision < 0)
                throw new ArgumentException("DeliveryObservationIdentityInvalid");
            OrderId = source.FoodOrderStableId;
            OrderRevision = source.Revision;
            StateCode = source.StateCode;
            Description = source.StateCode switch
            {
                "주문대기" => "주문 접수 대기",
                "조리중" => "음식 준비 중",
                "픽업대기" => "음식 픽업 대기",
                "기사배정" => "기사 배정 단계 · 실제 기사 연결 별도",
                "픽업완료" => "픽업 완료 · 전달 진행",
                "전달완료" => "전달 완료 · 수령 확인 대기",
                "수령확인" when source.ReceivedTick.HasValue => "수령 확인 완료 · 복귀 상태 연결 별도",
                "거절" => "주문 거절",
                "취소" => "주문 취소",
                _ => throw new ArgumentException("DeliveryObservationStateUnsupported")
            };
            ReceiptConfirmed = source.StateCode == "수령확인" && source.ReceivedTick.HasValue;
        }
    }
}
