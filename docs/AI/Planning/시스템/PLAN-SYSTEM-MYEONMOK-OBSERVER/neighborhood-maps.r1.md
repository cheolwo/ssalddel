# 현재 동네 전체 맵·코드 선행 정리 r1

- `PLAN-SYSTEM-MYEONMOK-OBSERVER` / 사용자 승인 / 구현·검증 중
- 기존 [하루 생활](neighborhood-day.r1.md), 음식 배달·마트·창고 표본을 보존한다. 새 Scene·실제 배치·공공자료 수집·commit/push는 제외한다.
- [그래프 맵](../../../../../eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json)은 주체·시설·주거·업무 관계를, [배치 맵](../../../../../eng/world-seedbeds/placement-map-profiles/synthetic-neighborhood.v1.json)은 기존 위치·크기·작업점·명시 경로를 소유한다.
- 배치 JSON의 기존 좌표를 공통 C# 정의로 생성하고 코드 소비자가 참조한다. 생성 코드의 수동 수정이나 Unity에서의 임의 좌표 보정을 정본에 역수입하지 않는다.
- 정리는 주택 생활 → 음식점 업무 → 마트/보충창고 업무 → 기존 도로·이동 순서의 검증 묶음이다. 전체 연결은 유지하며 독립 Scene이나 새로운 영역으로 나누지 않는다.
- 마트 운영 조회/Simulation 작업점 차이와 바닥 밖 입고점은 알려진 미해결 사항이다. 좌표를 강제 이주하지 않고 실제 배치 준비를 차단한다.
- 자동 검사와 실제 화면은 구별한다. 검사 통과는 참조·코드 일치의 증거이며 실제 E5·보행·겹침 해소 증거가 아니다.

## 결과

관계 노드 19개(시설·도로·주체 및 논리적 배달 대기 거점), 관계 20개, 물리 도형 9개, 기준점 44개, 경로 14개, NPC 9명을 등록했다. 배달 대기 거점의 기존 `facility:synthetic:courier-base`는 마트 주변을 공유하며 별도 건물을 생성하지 않는다. H 등급을 근거 없이 새로 확정하지 않았다.

| 후속 배치 묶음 | 기존 대상 | 이번 준비 | 남은 시각 검증 |
| --- | --- | --- | --- |
| 주택 생활 | 주택 A/B·주민·퇴근한 근로자 | 주거 관계·수령점·귀가 경로·하루 상태 | 출입구 폭·여러 사람의 간격·주택 앞 생활 판독 |
| 음식점 업무 | 음식점 주인·기사 | 업무/출입/정차점과 기존 조리·수령 흐름 연결 | 문턱·내외부 가림 |
| 마트·보충창고 업무 | 작업자·화물 기사·시설 두 곳 | 기존 입고·작업·인계 통로 구분 | 바닥 밖 입고점·운영 조회/생활 작업점 불일치 |
| 도로·이동 | 기존 도로 네 도형·대기점 다섯 곳 | 명시 경로 연결·점유 대기·재개 | 사람/차량 간격·교차로·실제 Collider |

## 정본과 코드 소비

- 배치 맵 JSON → `eng/world-seedbeds/manage-neighborhood-maps.ps1 -Mode Write` → `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/가상동네배치기준.generated.cs` 순서다. Unity 전용 JSON 로더나 새로운 Map Manager는 만들지 않는다.
- `-Mode Check`는 생성 코드와 배치 정의의 불일치를 거절한다. 기존 `GraphMapTooling.ps1`의 경로·JSON·중복·정규화 도구를 공유한다. 다른 스키마를 전제로 하는 북부 전체 맵 생성기는 변경하지 않는다.
- 공통 대기점·생활 주체 작업점·마트/음식점 귀가 경로를 공통 정의에 결속했다. Unity 소비 코드는 주요 건물·도로·대기점의 같은 값을 읽는다. 기존 세부 작업 애니메이션·내부 이동 수식까지 전면 교체한 것은 아니다.
- 기존 좌표와 경로 순서는 유지했다. 향후 r1 좌표를 바꾸려면 저장 재생 호환을 다시 검토하며 이번 코드 정리를 자동 이주 승인으로 사용하지 않는다.
- 그래프 맵과 배치 맵은 상태를 확정하는 엔진이 아니다. 생성 코드는 좌표 상수만 제공하고 업무 원장·배차·재고·이동 상태 권위는 기존 Core가 유지한다.

## 검증·제한

- 맵 정상 검사와 오류 주입 8종(중복 노드/주체, 누락 관계/기준점, 단절 경로, 시설 겹침, 미기록 바닥 밖 작업점, 생성 코드 불일치)이 통과했다. 실행: `pwsh -NoProfile -File eng/tests/neighborhood-maps.ps1`.
- Unity CLI EditMode의 `동네관찰RuntimeAdapterTests` 1/1 통과. 변경한 연결 코드를 포함한 Unity 컴파일 성공이며 실제 Scene 배치·Play Mode·Game View 증거는 아니다.
- 전체 Task 검사는 공용 E 책임 코드 지도의 현행 소스 불일치에서 차단됐다. 다른 작업의 생성물을 덮어쓰지 않았다. 상세: `artifacts/local/validation/neighborhood-maps-task.log`.
- 최종 Core 동네 관련 시험 46/46 통과. 유한 재고·하루 전이·저장/재생·기존 좌표 유지·반환 경로 독립성·미등록 기준점 거절·출입구 점유 대기/해제 후 재개를 확인했다. 상세 `artifacts/local/validation/neighborhood-maps-final-core.log`.
- 마트 입고 위치·운영/생활 좌표 차이·정밀 외형 미검증 때문에 `SceneReady=false`를 유지한다. 알려진 경고를 숨기거나 물체를 자동 이동하지 않았다.

## 후속: 중랑구 색인·면목동 500m 타일 참고 지도

기존 합성 Graph Map과 배치 Map은 그대로 보존하고, 공공·비공개 검토 자료를 섞지 않는 별도 [중랑구·면목동 참고 지도 결과](regional-reference-map-result.r1.md)를 추가했다. 중랑구는 상위 자료 색인, 면목동은 사가정역 기준 500m 타일 36개의 상세 후보로 표현한다.

개별 주소·사업체 행은 비공개 자료 대장에 남기고 추적 산출물에는 자료 판본·hash와 타일별 집계만 둔다. 기존 도형 동네는 `ScenarioOverlayOf`로 연결할 뿐 현실 공간으로 승격하지 않는다. 결과는 `PlacementReviewOnly / SceneReady=false`이며 Unity 기본 Game View·Scene·업무 권위는 변경하지 않는다.

후속 Unity 표현은 [공공데이터 기반 아이소메트릭 3D 디오라마 방향](public-data-isometric-diorama-direction.r1.md)을 따른다. Graph Map이 의미 관계를, 배치 Map이 좌표·외곽·집계와 미배치 사유를 정한 뒤 공개 가능한 읽기 전용 결과만 Unity가 표현한다. 이 방향 기록 자체로 Unity 구현이나 실제 Scene 배치를 시작하지 않는다.
