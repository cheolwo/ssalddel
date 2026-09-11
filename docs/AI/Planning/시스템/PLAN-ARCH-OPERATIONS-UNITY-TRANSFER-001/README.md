# 운영 서버 0.0~3.5에서 Mirror Unity로의 이관

- 음식점 후속 승인: [NPC 자동 수락·조리 대기열·도로 거리 배차 r3](restaurant-npc-operation.r3.md). 기획 승인 완료, WI·개발 작업 명세 결속 미완료.

- 기획 ID: `PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001`
- 기획 분야: 시스템·운영 기능 이관
- 기획 판본: `operations-unity-transfer.r3`
- 상태: `ApprovedForHandoff / AppObservationProfilesSeparated`
- 상위 기획: `PLAN-GAME-COMMON-PURPOSE-001`
- 관련 하위 기획: `PLAN-GRAPH-HUB-LOGISTICS-CIRCULATION-001`, `PLAN-PRESENTATION-E4-POOL-001`
- 관련 결정: 운영·Simulation·Unity 권위 분리, Farm·Hub·City 독립 영역 우선
- 관련 WI·PlayableLoop: `WI-001`, `WI-002`, `playable-loop:hub-inbound-putaway.v1`
- 플레이 순서: 독립 영역별. Hub 창고 입고·검수·적치를 첫 기술 표본으로 삼고, 도심 창고 출고·오토바이 배송은 다음 독립 표본 후보로 둔다. 어느 쪽도 게임의 필수 첫 방문 순서로 고정하지 않는다.
- Graph Map 영향: Hub 입고·검수·적치 노드와 내부 작업 엣지의 기존 안정 ID를 재사용한다.
- 다음 인계 상태: `ReadyForGraphMap`; Goal 자동 활성화·Unity Scene 변경·Evidence 자동 승격은 금지한다.

## 1. 목적

운영 서버가 0.0 커뮤니티·공공데이터부터 3.5 마트·도심 물류까지 보유한 업무 기능을 빠짐없이 조사하고, Mirror에서 플레이어가 실제로 다룰 부분과 서버에 남길 부분을 같은 대장으로 관리한다.

전수 조사 모수는 Unity 객체 수가 아니다. 페이지·API·저장 개체를 그대로 H1로 복제하지 않고, 서버의 권위 있는 업무 상태를 플레이어 과업과 공간으로 번역한다.

이 이관의 대표 표현 목표는 현실에서 볼 수 있는 창고 작업자·운송 기사·배달원과 화물의 흐름을 Unity 세계 안에서 알아볼 수 있게 만드는 것이다. 플레이어가 창고 안의 입고·검수·적치·피킹·포장·상차를 보고, 도심 물류 거점에서 오토바이와 차량이 출발·배송·복귀하는 모습을 보며, 필요할 때 직접 참여하거나 NPC에게 맡길 수 있어야 한다.

이를 위해 서버 기능은 화면을 그대로 옮기지 않고 다음 세 층으로 번역한다.

1. **업무 사실:** 권위 상태, 담당, 수량, 기한, 경로, 결과와 이력
2. **플레이 선택:** 관찰, 직접 수행, 위임, 보류, 경로·우선순위 변경
3. **세계 표현:** 작업자 행동, 화물 위치, 창고 점유, 차량 이동, 대기·지연·복귀

세 층은 같은 판본을 참조하지만 서로를 대신하지 않는다. NPC와 차량이 움직이는 장면만으로 입고·출고·배송 완료를 만들지 않고, 서버 상태만 존재한다고 실제 Unity 표현이 성립했다고 보지도 않는다.

## 2. 확정된 이관 등급

모든 운영 기능은 다음 기본 등급 하나를 가진다.

| 등급 | 의미 | Unity 기본 표현 |
| --- | --- | --- |
| `PlayableAction` | 플레이어가 선택하고 명시적으로 확인할 수 있는 업무 | H1 상호작용·선택 UI. 운영 변경은 서버 Command 뒤 재조회 |
| `ReadOnlyContext` | 상태·근거·이력·가격처럼 읽는 것이 중심인 정보 | World Object에서 여는 Panel 또는 선택형 상세 |
| `AmbientSimulation` | 차량·NPC·재고 흐름처럼 세계의 움직임을 설명하는 가상 상태 | 권위 상태 사본을 읽는 환경 표현. 운영 저장 금지 |
| `ServerOnly` | 금융·급여·개인정보·관리자·내부 Run·게임 내 실행이 승인되지 않은 외부 효과 | World Object로 만들지 않고 필요하면 권한 있는 Web으로 인계 |

등급은 서버 실행 권한을 부여하지 않는다. `PlayableAction`도 기존 권한·revision·Preview·Confirm·canonical 재조회를 통과해야 한다.

## 3. 앱과 생활 관찰 프로필 분리

운영 앱의 존재와 Unity에서 관찰할 수 있는 생활은 같은 목록으로 취급하지 않는다. 운영 앱은 실제 사용자·업무 원장·명시적 Command의 진입점이고, 생활 관찰 프로필은 `SimulationSession` 상태 사본을 같은 장면에서 읽어 표현하는 구성이다. 앱 하나가 여러 관찰 프로필과 연결되거나 여러 앱이 하나의 생활 프로필을 설명할 수 있으므로 관계는 다대다로 둔다.

첫 프로필은 `observation-profile:synthetic-neighborhood-food-life.v1`이다. `SimulationWorldShell`의 City 합성 동네에서 주민 주문, 음식점 수락·조리, 음식 배달 기사 배정·이동·픽업·전달, 주민 수령, 기사 복귀를 자율 생활로 묶는다. 기본 카메라는 전체 개요이며 사용자가 고른 주체·시설만 추적한다. 사건 발생만으로 카메라를 자동 전환하지 않는다.

| 운영 앱 | 관찰 관계 | 첫 생활 프로필에서 읽는 의미 |
| --- | --- | --- |
| `OrdererApp` | `SimulationAnalog` | 가상 주민의 음식 주문과 수령 |
| `RestaurantDeskApp` | `SimulationAnalog` | 가상 음식점의 접수·수락·조리 |
| `FoodDeliveryDriverApp` (`FDriverApp`) | `SimulationAnalog` | 가상 배달 기사의 배정·이동·픽업·전달·복귀 |

`SimulationAnalog`는 운영 앱·계정·주문·기사 위치를 Unity에 연결한다는 뜻이 아니다. 첫 프로필은 `allowsOperationalActions=false`, `observationPresentationOnly=true`이며 실제 주문, 실제 배차, 운영 DB 변경, 정산 효과를 만들지 않는다. 기존 Hub 입고 첫 표본과 전수 이관 등급은 그대로 유지한다.

세부 범위와 검증 절차는 [같은 장면 자율 음식 생활 관찰 r1](../PLAN-SYSTEM-MYEONMOK-OBSERVER/same-scene-food-life-observation.r1.md)에 결속한다.

운영 기사 앱과 Unity NPC의 권위 경계는 [음식 배달 기사 경계 r2](../PLAN-SYSTEM-MYEONMOK-OBSERVER/driver-role-boundary.r2.md)와 [화물 기사 경계 r1](../PLAN-SYSTEM-MYEONMOK-OBSERVER/freight-driver-role-boundary.r1.md)을 지원 자료로 사용한다. 두 문서는 과거 면목동 경로에 남아 있지만 소유 의미는 운영 기능 이관이며, 거리·거절률·완료율·시간당 균형 같은 운영 배차 정책은 별도 [운영 배차 공통 코어](../../공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)가 소유한다.

## 4. H1·H2 번역 규칙

### H1

H1은 플레이어가 식별하고 접근할 수 있는 **과업·작업 공간·상호작용 지점**이다. DB 행, 이력 행, Outbox, 수집 Run, 관리자 설정은 H1이 아니다.

한 H1은 여러 페이지·UseCase·저장 개체를 소비할 수 있고, 같은 서버 기능도 관점에 따라 여러 H1에서 읽을 수 있다. 관계는 다대다로 관리한다.

### H2

H2는 둘 이상의 H1을 시작·선택·결과·회복 또는 귀환으로 연결하는 독립 업무 블록이다. Farm·Hub·City의 H2는 다른 영역의 화물을 필수 시작 상태로 요구하지 않는다. 영역 간 운송은 양쪽 독립 폐루프가 준비된 뒤 별도 통합 엣지로 연다.

| 서버 업무 | 재사용 H1 | 재사용 H2 |
| --- | --- | --- |
| 창고 입고·검수·적치 | `h1-stock:hub-receiving-storage` | `h2-candidate:hub-inbound-storage`, `h2-candidate:hub-internal-warehouse` |
| 창고 피킹·포장·출고 준비 | `h1-stock:hub-outbound-staging`, `h1-stock:hub-temporary-staging` | `h2-candidate:hub-fulfillment` |
| 운송 상하차·차량 대기 | `h1-stock:hub-vehicle-yard`, `h1-stock:hub-town-corridor` | `h2-candidate:hub-outbound-vehicle`, `h2-candidate:hub-town-corridor` |
| 마트 주문·수령 | `h1-stock:town-market-display`, `h1-stock:town-order-packing`, `h1-stock:town-resident-pickup` | `h2-candidate:market-life-commerce`, `h2-candidate:town-order-fulfillment` |

커뮤니티·공공데이터·공동구매·음식점처럼 현행 H 정의가 충분하지 않은 기능은 기존 H를 억지로 재사용하지 않고 `HMappingRequired`로 남긴다.

### 물류 풍경을 구성할 H 후보

기존 안정 ID를 먼저 재사용하되, 다음 역할이 비어 있으면 Graph Map과 H 대장에서 새 후보 필요성을 검토한다.

| 규모 | 표현 역할 | 대표 내용 |
| --- | --- | --- |
| H1 | 입고·검수 작업 지점 | 하차, 등록, 이상 확인, 임시 대기 |
| H1 | 보관·피킹 작업 지점 | 랙·팔레트·재고 위치, 피킹, 이동 |
| H1 | 포장·상차·출차 지점 | 포장, 배송 묶음, 차량 배정, 출발 확인 |
| H1 | 도심 배송·복귀 지점 | 오토바이·소형 차량 대기, 배송 출발, 반품·회수 |
| H1 | 작업자 안전·휴식 지점 | 교대, 휴식, 보호 장비, 사고·과로 예방 |
| H2 | 창고 내부 순환 | 입고→검수→적치→다음 작업 선택 |
| H2 | 출고 순환 | 지시→피킹→포장→상차→출차 |
| H2 | 마지막 구간 배송 순환 | 출발→이동→인수 또는 실패→복귀·재배송 |

H1 하나를 건물 하나나 Prefab 하나와 동일시하지 않는다. 작업 지점 하나가 여러 소품·Actor·Animation을 조합할 수 있고, 같은 창고 건물 안에 여러 H1이 함께 존재할 수 있다.

## 5. 전수 이관 대장

기계 판독 대장은 다음을 각각 독립 배열로 보존한다.

- 0.0~3.5 페이지 기능 규칙 전체
- EF Core `DbSet` 전체와 MongoDB collection 전체
- 현행 Unity 대표 Page-to-World 경로와 표현 구현 근거
- 페이지별 canonical 업무 기능, 이관 등급, 표시 방식, H1/H2·WI·PlayableLoop 후보
- 권한·개인정보·외부 효과와 가장 이른 Evidence 재개 단계
- 판본·파일 hash와 대장 불일치 진단

페이지 별칭은 canonical 기능 ID로 묶는다. 다만 자동 정규화로 의미를 확정하지 않으며, 검증된 별칭 규칙만 같은 ID를 공유한다.

## 6. 첫 독립 표본: Hub 입고·검수·적치

### 지금·여기·나·너·이렇게

- 지금: Hub 내부 입고 상태 사본이 조회되었고 검수·적치 작업을 고를 수 있는 시점
- 여기: `area-set:sim:pyeongchang:logistics-hub.v1` 안의 입고·검수·보관 작업 공간
- 나: 창고 관리자 관점 또는 독립 Hub Simulation의 허용된 행위 주체
- 너: 입고 화물, 검수 상태, 보관 위치와 이를 처리하는 NPC 작업
- 이렇게: `WI-001`로 화물을 검수하고 `WI-002`로 적치한 뒤 `HubWorkChoiceAvailable`로 돌아간다.

### 기존 계약 재사용

- 서버 계약: `WarehouseWorldSnapshotRoutes.AuthorizedSnapshot`
- 서버 조회: `창고WorldSnapshot조회UseCase`
- Unity 소비: `WarehouseWorldApiRepository`, `WarehouseWorldInterpreter`, `WarehousePresenter`
- H1: `h1-stock:hub-receiving-storage`
- H2: `h2-candidate:hub-inbound-storage`, 상위 조립 후보 `h2-candidate:hub-internal-warehouse`

새 Entity별 API나 별도 공식 Scene을 만들지 않는다. 첫 구현은 권한 있는 Snapshot 조회·해석·표현 준비를 재사용한다.

### 현재 Evidence와 차단

- 서버 Snapshot 계약과 Unity 읽기·해석·표현 코드: 존재
- PlayableLoop 기획 관문: 이 판본으로 승인 가능
- H2 세부 연결구·내부 도달 가능성: 미검증
- 실제 Prefab·World 배치·활성 Renderer/Collider/Bounds·입력: 미검증
- 운영 Command 연결과 canonical 재조회: 조회 표본 뒤 별도 WI에서 검증
- 현행 Presentation은 E1이며 이 이관 대장의 목표 상한은 E4 준비다. E5는 실제 배치 증거 전까지 금지한다.

## 7. 영역별 후속 순서

1. Hub 창고 입고·검수·적치
2. Hub 출고 준비와 내부 작업 반환
3. 도심 물류 거점의 출고·오토바이 배송·인수/실패·복귀 독립 폐루프
4. 운송·배차 독립 폐루프
5. 음식점 주문·조리·픽업 독립 폐루프
6. 마트 주문·피킹·수령 독립 폐루프
7. 커뮤니티·공공데이터·공동구매의 선택형 조회·협의
8. 양쪽 영역이 독립 준비된 뒤 창고→운송, 음식점→배달, 마트→도시 배송 통합

이 순서는 제품 출시 판본의 선후행 의존성을 뜻하지 않는다.

## 8. E4에서 E5로 가는 관문

다음 항목이 같은 판본으로 결속된 H1만 E5 실행 후보가 된다.

1. Logic E5 상태 사본 또는 명시적인 읽기 전용 권위 Projection
2. 플레이어 판독 순간과 `VisualKey`
3. 동결된 주·대체·fallback 자산 후보
4. Graph Map 레벨 1~3과 배치 맵의 시야·통행·접근·간격 제약
5. `InteractionAnchor`, 입력, 결과, 취소·해제·귀환 조건
6. active Renderer·Collider·Bounds와 동일 revision의 실제 World 관측
7. 운영 변경이면 Preview→Confirm→server Command→canonical 재조회

정적 Fixture, 컴파일, 단위 시험, 보조 Scene 또는 이미지 후보만으로 E5를 선언하지 않는다.

## 9. 상위 목적 정렬

- 나의 막힘: 방대한 운영 기능을 그대로 World Object로 옮기면 무엇을 해야 하는지 판독하기 어렵다.
- 회복 행동과 대가: 플레이어 과업 단위로 선별 번역하되 정밀 관리·민감 업무는 Web에 남긴다.
- 기여 자원: 기존 0.0~3.5 기능, 권위 Snapshot, WI, H1/H2, Graph Map, Synty 표현 후보
- 상대의 실제 필요: 창고·운송·음식점·마트·공동체 각 영역이 독립적으로 읽히고 작동해야 한다.
- 권위 결과: 서버 또는 Simulation Core가 판정하고 Unity는 같은 revision을 표현한다.
- 환류: 성공 결과를 재조회해 다음 과업과 H1 상태를 갱신한다.
- 보조 층: 공공데이터 근거, 상세 이력, Web 인계, 환경 NPC·차량 표현

## 10. 확정·미정

### 확정

- 네 등급 선별 이관
- 영역별 독립 폐루프 우선
- 첫 기술 표본은 Hub 입고·검수·적치
- 다음 독립 표본 후보는 도심 창고 출고·오토바이 배송·인수/실패·복귀
- H1은 과업 공간, H2는 독립 업무 블록
- Unity는 운영 상태를 직접 쓰지 않음

### 미정 또는 후속 검증

- 서버 기능별 최종 H 신규 생성 여부
- 음식점 전용 H1/H2의 정확한 공간 구성
- Hub H2 연결구·동선·자산과 실제 E5 판본
- 도심 배송 표본의 정확 WI·H1/H2·경로·오토바이 및 Actor 표현 후보
- 운영 Command를 게임 내에서 허용할 개별 WI와 권한

## 11. 구현된 관리 도구

- 음식점 첫 구현: [접수→조리→픽업 대기 전용 처리](restaurant-processing.md). 별도 고객 주문 대기함·수락/거절·배차 연결과 실제 UI는 미완료.

- 첫 표본 구현: [창고 입고 관찰·정책 카드 r1](warehouse-policy-cards.md) — 기존 LocalRuntime과 설정 카드 로직 연결, 실제 Unity UI/배치는 미검증.

- 정책 원본: `eng/execution-ledgers/operational-unity-transfer-policy.json`
- 생성·조회: `eng/execution-ledgers/manage-operational-unity-transfer-catalog.ps1`
- 기계 대장: `docs/AI/generated/operational-unity-transfer-catalog.json`
- 사람이 읽는 대장: `docs/AI/generated/operational-unity-transfer-catalog.md`
- 구조 회귀: `eng/tests/operational-unity-transfer-catalog.ps1`
- 첫 표본 상세 기획: `docs/Architecture/PlayableLoops/Hub입고검수적치.md`

대장은 현재 코드에서 페이지 기능, EF Core `DbSet`, MongoDB `GetCollection` 사용 지점과 Unity 대표 경로를 다시 읽는다. H 대응은 검토 후보이며 중앙 H 대장의 선언 수와 실제 안정 ID 수가 다르면 수정하지 않고 진단으로 반환한다.
