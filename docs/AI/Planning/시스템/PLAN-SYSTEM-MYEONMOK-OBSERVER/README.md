# 면목동 실제 지도 기반 배달 관찰 r1

- 기획 ID: `PLAN-SYSTEM-MYEONMOK-OBSERVER`
- 분야·판본: 시스템 / `myeonmok-observer.r1`
- 상태: `ApprovedPlanningBaseline / ReferenceMapPrepared / FiveElementObjectCatalogImplemented / SpatialReviewPending`
- 승인 근거: 2026-09-06 면목동 실제 지리와 가상 NPC 배달 관찰 제안 뒤 사용자의 `Implement the proposed plan` 요청.
- 상위: [관찰 중심 개인 세계](../PLAN-SYSTEM-OBSERVER-WORLD/README.md)
- 관련: [운영 이관](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md), [음식점 NPC 운영 r3](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/restaurant-npc-operation.r3.md)
- 구현·자료 인계: [기반 구현 명세](implementation.md)
- 공간 정리: [중랑구 색인·면목동 500m 타일 참고 지도](regional-reference-map-result.r1.md)
- 표현 방향: [공공데이터 기반 아이소메트릭 3D 디오라마](public-data-isometric-diorama-direction.r1.md)
- 객체·업무 결속: [면목동 오행 업무 객체 대장 r1](myeonmok-five-element-game-object-catalog.r1.md)

## 확정

면목역 주변 약 500m × 500m의 실제 도로·건물 윤곽을 바탕으로 가상의 음식점 1곳, 목적지 2곳, 배달 NPC 1명의 운영을 관찰한다. 실존 업소·주민·주문·기사 위치를 재현하는 것이 아니다. 정확한 중심·범위·출입구·정차점은 실제 원본과 공간 검토로 결속하며 임의 좌표를 면목동이라고 표시하지 않는다.

사용자는 관찰·일시정지·목표와 정책 설정을 주로 사용한다. 이 표본만 앱 실행 중 자동 시간을 사용하고 앱 종료 뒤에는 진행하지 않는다. 상위 기획의 향후 서버 지속 세계·24시간 개인 작업 상한을 삭제하거나 기존 프로필에 자동 시간을 켜지 않는다. 별도 AreaSet·프로필·저장 슬롯으로 기존 평창 공간과 격리하되 공식 Scene은 `SimulationWorldShell`을 유지한다.

가상 주문 → NPC 수락 → 조리와 배차 대기 병행 → 도로 경로로 음식점 도착 → 조리 완료와 기사 도착이 모두 성립한 뒤 픽업 → 목적지 이동 → 별도 수령 확인 → 대기점 복귀 순서다. NPC 한 명은 주문 하나만 맡는다. 후보 배차는 통행 가능한 최단 도로 거리와 안정 ID로 결정한다. 막힌 길은 대기 사유를 남기며 순간이동·시간만으로 픽업/배달 완료 처리하지 않는다.

첫 구현은 도로 이동과 짧은 도보 접근을 분리한다. 교차하는 선만 보고 교차로를 생성하거나 건물 중심에서 출입구·길을 추정하지 않는다. 높이·지형·도로 폭의 미확인을 0이나 평지 확정으로 바꾸지 않는다. 단순 외형을 먼저 사용하고 Blender 고도화는 경로·배치 검증 이후로 둔다. Farm·Hub에서 받은 화물이 없어도 독립 City 표본으로 동작해야 한다.

## 미정·진행 관문

- Unity의 장기 표현은 Graph Map과 배치 Map을 거친 공공데이터 기반 아이소메트릭 3D 디오라마로 정했다. 현재는 방향만 기록했으며 Unity 코드·Scene 적용은 보류한다.
- 사가정역 기준 OSM 건물·도로와 중랑구·면목동 비공개 검토 사본은 판본·hash에 결속했다. 다만 면목동 전체 행정경계, 상가 좌표의 명시적 측지 기준, 출입구·통행 가능성은 아직 확정되지 않았다.
- 공간·접근·도로 연결 연구가 `Accepted`로 재결속되기 전 실제 Scene 배치·NPC 이동 실행을 시작하지 않는다. 이번 기획 승인으로 연구 결과·E 증거를 승인했다고 해석하지 않는다.
- 기존 주문/조리 WI는 유지한다. 객체 원형 26개와 WI Profile 11개의 비권위 결속은 준비했지만, 실제 Scene 객체·Goal/E7·저장/재생·실행 시간 결속은 후속 통합 작업이다. 공통 파일의 기존 담당·수정 범위도 다시 확인한다.
- 합성 시험은 `SyntheticFixture`로 표시하고 실제 면목동 자료나 공간 승인으로 승격하지 않는다.

다음 입력 하나: 면목동 행정경계와 출입구·도로 통행 검토 자료를 확보해 `PlacementReviewOnly` 타일 후보 중 실제 World 배치로 승격할 작은 구역 하나를 고른다.
