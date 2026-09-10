using Ssalddel.Unity.Presentation;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Samples.FoodDelivery
{
    /// <summary>
    /// 한 배달기사 Prefab 인스턴스의 표현 컴포넌트입니다.
    /// Mongo 문서마다 MonoBehaviour 타입을 만들지 않고 같은 타입의 여러 인스턴스가 서로 다른 StableId를 표시합니다.
    /// </summary>
    public sealed class 음식배달ActorView : MonoBehaviour
    {
        [SerializeField] private string actorStableId = string.Empty;
        [SerializeField] private Transform[] routePoints = new Transform[0];
        [SerializeField] private float moveSpeed = 2f;

        private int targetIndex;
        public string ActorStableId => actorStableId;
        public string BoundOrderStableId { get; private set; } = string.Empty;
        public long BoundRevision { get; private set; } = -1;

        public bool TryApply(음식배달수명주기Snapshot snapshot)
        {
            var presentation = new 음식배달수명주기표현(snapshot);
            if (presentation.Revision < BoundRevision) return false;
            BoundOrderStableId = presentation.OrderStableId;
            BoundRevision = presentation.Revision;
            targetIndex = Mathf.Clamp(presentation.StepIndex, 0, Mathf.Max(0, routePoints.Length - 1));
            return true;
        }

        private void Update()
        {
            if (routePoints.Length == 0 || routePoints[targetIndex] == null) return;
            transform.position = Vector3.MoveTowards(
                transform.position,
                routePoints[targetIndex].position,
                Mathf.Max(0, moveSpeed) * Time.deltaTime);
        }
    }
}
