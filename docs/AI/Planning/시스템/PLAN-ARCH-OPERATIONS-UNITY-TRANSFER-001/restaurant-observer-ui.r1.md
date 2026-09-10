# 음식점 로컬 관찰 UI — 구현 범위 r1

- 승인: 2026-09-06 사용자가 공통 구조+음식점, 설정·주문 상태 우선, 로컬 공통 Core, 수동 1 Tick을 선택하고 제시된 계획에 `Implement the proposed plan.`으로 구현을 요청했다.
- 기준: [NPC 운영 r3](restaurant-npc-operation.r3.md)의 업무 규칙·hash는 변경하지 않는다. 이 문서는 접수·조리 WI의 표현 연결과 실행 표본 범위만 추가한다.
- 기존 Goal: `interaction-goal:restaurant-auto-accept.v1`, `interaction-goal:restaurant-cooking.v1`. 주체는 기존 Ready NPC·권위 상호작용 대상이며 새 WI를 만들지 않는다.
- 직접 결과: 사용자가 샘플 주문을 명시적으로 추가하고, 1 Tick마다 접수·대기·조리·픽업 준비를 읽으며 자리·시간·새 조리 배정 정책을 조정한다. 대가는 기존 자리 점유와 대기 시간이다. 실패 시 조작 차단·최신 재조회 후 다시 선택한다.
- 실행: 같은 공식 `SimulationWorldShell`, 별도 음식점 선택 프로필과 저장 슬롯, Runtime/Session 하나, 명시적 저장. 기존 자연 생존 자동 실행·저장과 분리한다. 최초 주문은 자동 생성하지 않는다.
- 표시: 기존 Unity UI의 텍스트·버튼·입력·스크롤 목록. 건물/도로/Actor 자산·H 배치·Animation·InteractionAnchor 변경 없음. 공간 전문 연구는 이번 비공간 카드 연결에 요구하지 않는다.
- E7→E1 영향: 실제 입력 완주(E7)·동작(E6)·World 결속(E5)은 미검증으로 유지. E4는 텍스트 카드 후보, E3는 화면 모델·모의 세션 시험, E2는 명령/조회와 UI 결속, E1은 r3 사용자 약속이다. 자동 시험으로 단계 승격하지 않는다.
- E1→E7 검증: 기존 계약·명령 재사용 → 화면 모델 시험 → 로컬 세션/저장 회귀 → Unity 컴파일/EditMode 결속. 실제 Play Mode·Game View와 원격 서버는 별도 증거다.
- 쓰기 소유: 이 개발 작업이 음식점 공통 표본/Presenter/시험, 별도 Unity의 음식점 Bootstrap/View/시험 및 로컬 프로필 분기를 맡는다. 다른 기존 변경은 보존한다.
- 제외: 기사 배정·이동, 3D 조리, 새 공식 Scene, 운영 DB/HTTP, 자동 시간 진행, 식재료 비용, 기존 저장 migration, 커밋·푸시.

구조와 사용법은 [Unity 코드 연결 안내](../../../../Architecture/UnityClientLayeredArchitecture.md)를 따른다. 구현 검증 결과는 CURRENT_WORK의 최신 snapshot에 기록한다.
