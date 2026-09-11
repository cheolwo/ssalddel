# 운영 화물 기사와 Unity NPC 관찰 역할 경계 r1

> 문서 분류: `시스템 / 운영 기능 이관 / SupportingSlice`. 과거 경로와 기획 ID는 호환을 위해 유지하며, 현행 소유 맥락은 [`PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001`](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)이다. 화물 배차의 공통 지표를 확장할 때는 [운영 배차 공통 코어](../../공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)에서 별도로 문답한다.

- 기획 ID: `PLAN-SYSTEM-MYEONMOK-OBSERVER`
- 판본: `freight-driver-role-boundary.r1`
- 상태: `Approved / ServerAcceptanceGateImplemented / DriverWorkspaceImplemented / UnityObservationLabelImplemented / RuntimeEvidencePending`

## 확정

`DriverApp`은 실제 화물 기사가 추천을 검토하고 수락한 뒤 상차지 도착, 상차·인수증 증빙, 하차지 도착, 하차 증빙, 예외 신고를 수행하는 운영 앱이다. Unity의 `SimulationWorldShell`은 가상 화물 NPC와 Simulation 상태 사본을 관찰하는 화면이며 실제 기사 API, 실제 GPS, 상·하차 확정, 정산을 호출하지 않는다.

기사의 활성 운송 건수에는 고정 업무 상한을 두지 않는다. 추가 수락은 최신 서버 원장으로 다음 조건을 모두 다시 계산할 때만 허용한다.

1. 차량과 화물의 단건 적합성
2. 단독·혼적 금지·민감 화물과 기존 화물의 호환성
3. 권장 경로 각 구간의 합산 중량·부피·팔레트 한도
4. 기존 운송과 새 운송의 상·하차 시간창 전체 완수 가능성
5. 현재 위치·좌표·시간창·화물 제원 근거의 존재

진행 중 운송이 있는데 근거가 빠진 경우 서버는 추정하지 않고 추가 수락을 차단한다. 첫 단독 운송에서 차량 제원이 없으면 경고를 표시하고 기사 본인이 명시적으로 확인해야 한다. `기사당최대추천건수`는 동시에 잠글 추천 노출 수만 제한하며 수락한 운송의 업무 상한으로 사용하지 않는다.

수락은 기사별 실행 Gate와 `Serializable` 트랜잭션 안에서 추천 라운드, 배차 가능 상태, 차량·일정·적재 조건을 재검증한다. 클라이언트의 평가값은 확정 근거가 아니다. 같은 기사·의뢰의 이미 확정된 수락 재시도는 새 Event를 만들지 않고 현재 결과를 반환한다.

안정 오류 코드는 다음과 같다.

- `FreightVehicleCompatibilityFailed`
- `FreightScheduleInfeasible`
- `FreightScheduleEvidenceMissing`
- `FreightRecommendationStale`
- `FreightWarningAcknowledgementRequired`

`GET api/v1/driver/transports/workspace`는 모든 활성 운송, 다음 현장 행동 하나, 정렬된 상·하차 정차점, 일정·적재 재검증 상태와 규칙 판본을 반환한다. 호환 `/current`는 최근 수정 건이 아니라 다음 현장 행동 우선순위가 가장 높은 운송을 반환한다.

Unity는 기존 `ISimulationLogisticsRuntime`, `물류이동Presenter`, `법정동화물운송View`를 유지하고 `가상 화물 NPC 관찰 · 실제 화물 운송 없음`을 표시한다. 새 Scene, Map Manager, 운영 기사 Controller 결속은 만들지 않는다.

## 구현 상한과 남은 검증

- 서버와 DriverApp 소스·집중 시험·Windows build까지 구현한다.
- Unity는 기존 Presenter 표시와 EditMode 시험까지만 대상이며 Scene/Prefab을 저장하지 않는다.
- 실제 다중 host DB 경쟁, 운영 기사 운행, 운영 결제·정산, Unity Play Mode·Game View는 별도 증거가 필요하다.
- 공공데이터 기반 면목동 디오라마의 공간 승인과 화물 업무 경계는 서로 대신하지 않는다.
