# 오행 업무 오프라인 생활 구성 r1

상태: Approved. 2026-09-09 사용자가 제시 계획에 `Implement the proposed plan.`으로 승인했다. DB도 없는 완전 오프라인 실행, 화면·로컬 기록·Mongo 보고 보관, 기존 작은 합성 동네를 선택했다.

## 재사용·범위

기존 `neighborhood-life.r1.md`의 Required 공간 기준과 `autonomous-food-world.r6.md`의 반복 업무를 재사용한다. 대표 WI는 `WI-CITY-SYNTHETIC-ASSIGN`, 작업 명세는 `eng/execution-ledgers/work-orders/neighborhood-life-assign.e7-work-order.json`이다. 새 Goal/WI/E 승격은 없다. 본 부록은 같은 WI의 시작 자료·표현 수명·보고 인계를 소유하며 다른 22개 생활 WI의 기존 규칙을 보존한다.

지금: 기존 1800 Tick, 종료 시간 따라잡기 없음. 여기: 기존 합성 동네와 승인된 통로. 나: 관찰자. 너: 주민·음식점·기사·마트·창고·화물 기사. 이렇게: 검증된 로컬 묶음으로 시작/중지/저장/이어하기. 결과: 같은 세션의 업무·행위 기록과 표현. 다음 선택: 명시적 재실행 또는 보고 사본 보관. 운영 서버·실제 주문/재고/Save 초기화 없음.

## 정확 구현 소유

- Contracts `UnityPackage/Runtime/로컬생활구성Contracts.cs`, 기존 세션 생성 계약의 선택적 구성 필드.
- Domain 기존 합성 생활 partial·Save/Replay: 입력 결속과 기존 규칙 재사용. 기존 null 구성의 행동·저장 호환 유지.
- `Ssalddel.Simulation.Application/RuntimeCore/Bootstrap/로컬생활시작자료.cs`와 `로컬생활보고.cs`: 묶음·파일 읽기·보고 투영. `Ssalddel.Unity/Runtime/Warehouse/로컬생활구성표본.cs`는 내보내기 도구의 명시 표본 준비만 담당하며 Unity 실행 실패의 대체 입력으로 쓰지 않는다.
- 기존 `eng/Ssalddel.PublicDataPortalImport`: local-life 하위 명령. 기존 로컬 Mongo 설정·수집 도구 재사용, 새 HTTP host 없음.
- Unity `Assets/Ssalddel/Bootstrap/오프라인생활Controller.cs`, `Infrastructure/Simulation/SimulationWorldLocalRuntimeScope.cs`, 기존 관찰 Controller의 설치 제외 분기, 전용 Editor 메뉴·시험. Scene/원본 Prefab 저장 없음.
- 전용 .NET/Unity 시험·`docs/Reports/오프라인생활-구성과보고-2026-09-09.md`·CURRENT_WORK. 상세 결과는 Git 제외 `artifacts/local/offline-life-r1/`.

## 검증·실패 경계

E7→E1 검토: 실제 화면은 별도, 생성/수명/보고→오프라인 저장→동일 상태 사본→자료 유효성→원문/역할/규칙. E1→E7 검증: hash/참조·입력 불변→Core 회귀→JSON 재조회→객체 수명→Unity 시험→실제 화면. 실패면 해당 책임으로 반환하며 판정 완화 없음.

처음에는 기존 고정 합성 지형만 허용한다. 건물/도로 좌표를 바꾸는 일반 지도 실행기나 미검토 경로 탐색은 아니다. 고유 ID 변경과 가용 대기점 내 기사 수 변경은 명시 구성으로 지원한다. 다른 원형/지형은 구체적으로 거부한다. Mongo 구성은 실행 권위를 갖지 않으며 시작 후 DB 재조회 없음. 원본 편집 권위는 유지한다. 보고 저장 실패는 로컬 기록을 보존하고 재반입 가능, 로컬 저장 실패는 진행 정지다.

이번 완료 판정은 원본→Mongo 검증 보관→JSON 내보내기→오프라인 실행→로컬 저장/보고→Mongo 중복 없는 재보관을 구분한다. 자료·단위 시험을 실제 Game View 증거로 승격하지 않는다. commit/push 없음.
