# 농장 울타리 경계 구간 H1

@spatial-knowledge h1-stock:farm-fence-edge
@hierarchy H1
@state ExploratoryInventory
@wi WI-NATURE-20
@wi WI-FARM-DEFENSE-MOBILIZE
@wi WI-FARM-DEFENSE-RESOLVE
@gameplay FenceDamageState
@gameplay FenceRepairState
@gameplay FarmDefenseBottleneck
@role FarmFenceBoundary
@role FarmDefenseBottleneck
@capability Spatial.FarmFenceBoundary
@capability Spatial.FarmFenceRepairAnchor
@capability Spatial.FarmDefenseBottleneck
@capability Spatial.FarmIngressGap
@capacity FenceSegmentCount
@capacity IngressWidth
@connector FarmBoundaryIngress
@connector PatrolRouteConnector
@connector ThreatBreachConnector
@grammar farm:헛간 작업마당

`h1-stock:farm-fence-edge`는 농장 경계의 손상·수리·돌파 상태와 통과 가능한 출입 틈을 한 구간에서 판독하기 위한 재사용 H1이다.

- 손상 울타리 수리와 농장 방어가 같은 경계를 공유한다.
- 순찰·위협 진입·플레이어 귀환은 서로 다른 연결 역할로 유지한다.
- Blender 시안은 표현 후보일 뿐 실제 Unity 배치나 권위 상태가 아니다.
