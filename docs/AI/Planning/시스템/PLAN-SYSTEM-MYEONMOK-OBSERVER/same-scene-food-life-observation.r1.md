# [기획 · 시스템 · PLAN-SYSTEM-MYEONMOK-OBSERVER · same-scene-food-life-observation.r1]

> 문서 분류: `시스템 / 관찰 세계 / SupportingSlice`. 과거 경로와 기획 ID는 호환을 위해 유지하며, 합성 생활·카메라·표현의 현행 소유 맥락은 [`PLAN-SYSTEM-OBSERVER-WORLD`](../PLAN-SYSTEM-OBSERVER-WORLD/README.md)다. 운영 배차 원장과 지표는 [운영 배차 공통 코어](../../공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)가 소유한다.

- 상태: `Approved / Implemented / FocusedAutomatedValidationPassed / ActualUnityPlayAndGameViewObserved`
- 승인 근거: 2026-09-10 사용자가 운영 앱과 Unity 관찰 가능 생활을 구분하고, 여러 작업이 같은 장면에서 자연스럽게 살아가도록 확장하는 계획을 요청한 뒤 `Implement the proposed plan`으로 구현을 승인했다.
- 상위 기획: [면목동 실제 지도 기반 배달 관찰 r1](README.md)
- 관련 기획: [운영 서버에서 Mirror Unity로의 이관 r3](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md), [자율 음식 배달 반복 관찰 r6](autonomous-food-world.r6.md)
- 관찰 프로필: `observation-profile:synthetic-neighborhood-food-life.v1`
- 공식 Scene: `SimulationWorldShell`
- 영역·자료: `City` / 기존 합성 동네. 실제 면목동 GIS 배치가 아니다.

## 확정

운영 앱과 Unity 생활 관찰 프로필을 별도 목록으로 관리하고 다대다로 연결한다. 첫 연결은 `OrdererApp`, `RestaurantDeskApp`, `FoodDeliveryDriverApp`의 업무 의미를 하나의 가상 음식 생활 프로필로 묶는 `SimulationAnalog`다. 실제 앱 원장이나 실제 사용자를 Unity에 복제하지 않는다.

한 장면에서 다음 흐름을 NPC가 자율적으로 이어 간다.

1. 주민 NPC가 음식 주문을 만든다.
2. 음식점 NPC가 주문을 확인하고 수락한다.
3. 음식점 NPC가 조리하고 픽업 대기로 전환한다.
4. 음식 배달 NPC가 배정되고 음식점까지 이동한다.
5. 조리와 도착 조건이 모두 충족되면 픽업한다.
6. 주문자 주택으로 이동해 전달한다.
7. 주민 NPC가 수령을 확인한다.
8. 음식 배달 NPC가 대기점으로 복귀한다.

기존 `WI-CITY-RESTAURANT-ACCEPT`, `WI-CITY-RESTAURANT-COOK`, `WI-CITY-SYNTHETIC-ASSIGN`, `WI-CITY-SYNTHETIC-MOVE`, `WI-CITY-SYNTHETIC-PICKUP`, `WI-CITY-SYNTHETIC-DELIVER`, `WI-CITY-SYNTHETIC-RECEIVE`, `WI-CITY-SYNTHETIC-RETURN`을 재사용한다. 새 WI·Goal·공식 Scene을 만들지 않는다.

Simulation Core가 주문과 생활 상태의 권위다. Unity Presenter와 휴대폰 화면은 같은 revision의 사본만 읽으며 `allowsOperationalActions=false`, `observationPresentationOnly=true`를 유지한다. 실제 음식 주문·운영 음식점 수락·실제 기사 배차나 위치·정산·운영 DB 쓰기는 범위 밖이다. 기존 마트·창고·화물 생활은 같은 합성 동네에서 계속 움직일 수 있지만 이번 완료 판정에는 넣지 않는다.

카메라는 전체 개요를 기본으로 한다. 사용자가 휴대폰에서 주문자·음식점·배달 기사를 고른 뒤 명시적으로 관찰할 때만 주체 또는 시설을 추적한다. 사건에 따른 자동 카메라 전환은 하지 않으며 대상이 사라지면 전체 개요로 안전하게 돌아간다.

## 구현 범위

- 운영-Unity 이관 정책과 생성 대장에 관찰 프로필·앱 연결을 추가한다.
- 음식 주문별로 주문자·음식점·배달 기사·현재 단계·대기 사유·완료 여부를 읽기 전용 투영한다.
- Unity 휴대폰의 동네 화면에서 음식 생활 흐름을 주문 상세로 탐색하고, 주문자·음식점·기사 관찰은 사용자의 추가 선택으로만 실행한다.
- `SimulationWorldShell.unity`와 운영 앱 API·DB·Command는 수정하지 않는다.

## 검증 상한

- 자동 시험: 정책 참조·중복·권위 경계, 기존 Hub 첫 표본 보존, Presenter의 원본 불변과 음식 생활 연결, 기존 자율 생활 회귀.
- Unity 정적·컴파일·EditMode: 별도 Unity 작업 트리에 반영된 공유 소스와 휴대폰 표현을 확인한다.
- 실제 장면: 프로필 5에서 `SimulationWorldShell`을 한 번 재생하고 추가 주문·정책 조작 없이 음식 생활 한 주기와 기사 복귀를 최대 5분 동안 관찰한다. 전체 개요, 사용자가 고른 추적, 완료·복귀 화면과 Console을 별도 증거로 남긴다.
- 자동 시험이나 과거 캡처를 현재 Play Mode·Game View 증거로 대신하지 않는다. 실행 환경을 사용할 수 없으면 코드·시험 완료와 실제 화면 미검증을 분리해 기록한다.

## 미정

- 실제 면목동 공간 자료의 배치 승격은 기존 공간 검토 관문을 유지한다.
- 추가 앱과 생활 프로필은 첫 프로필의 안정성 확인 뒤 별도 승인 범위로 확장한다.

## 구현·검증 결과

- 이관 정책 v2와 생성 대장에 첫 생활 프로필, 세 앱의 `SimulationAnalog` 연결, 운영 행위 금지 검사를 추가했다. 기존 Hub 첫 표본은 바꾸지 않았다.
- Presenter는 음식 주문별 주문자·음식점·기사·단계·대기 사유·현재 시설·완료/거절을 같은 사본에서 파생하며 원본 상태를 변경하지 않는다.
- Unity 휴대폰은 생활 카드에서 주문 상세로만 이동하고, 주문자·음식점·기사를 다시 고른 뒤 `이 NPC 따라가기`를 눌러야 카메라가 이동한다. 추적 대상이 사라질 때는 전체 개요로 돌아간다.
- 자동 검증: 이관 대장 34건, .NET 관찰 25건, 합성 동네 10건, 한 기사 전체 흐름 1건, Unity 휴대폰 EditMode 9건, Unity 생활 EditMode 3건 통과.
- 실제 Unity: `SimulationWorldShell`에서 격리 저장을 쓰는 프로필 5를 한 번 재생했다. 추가 주문·정책 조작 없이 Tick 50에 첫 주문 `수령확인`, 배정 기사 `Idle`, 기사 주문 ID 빈 값을 확인했다. 전체 개요·명시적 기사 추적·완료 주문 Game View를 캡처했고 Console 오류는 0건이었다.
- 입력은 Unity Pipeline을 통해 실제 휴대폰 `Button.onClick`을 호출했다. 실제 마우스 수동 클릭 시험으로 확대해 표현하지 않는다. Scene은 저장하지 않았고 프로필 환경 변수는 검증 뒤 기본값으로 복구했다.
- 문서 범위 표준 `Fast`와 이번 변경 파일의 `git diff --check`는 통과했다. 코드 범위 표준 `Fast`·`Task`는 빌드·시험 전에 기존 E 책임 미분류 8건에서 중단됐으며, 이번 변경의 기능별 직접 시험 통과와 구분한다.

다음 질문 하나: 다음 앱 묶음을 정할 때 관찰 프로필을 직업 중심으로 넓힐지, 장소 중심 생활 묶음으로 넓힐지 하나를 고른다.
