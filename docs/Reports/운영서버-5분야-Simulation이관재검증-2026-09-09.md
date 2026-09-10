# 운영 서버 다섯 분야의 Simulation 이관 재검증

## 결론

**다섯 분야 모두 대응 코드와 출처 연결이 있지만, 전체 API 이관 완료로 판정할 수 없다.** 운영·Simulation·Unity의 대표 흐름을 대조한 기존 시험 157개에 이어 출처·차이 전용 시험 21개를 통과했다. 두 묶음은 목적이 다르며 단순 합계로 제품 완성도를 나타내지 않는다. 이는 운영 API 전체 기능·예외·권한의 일대일 이관이나 실제 서버→Unity 연결 성공을 뜻하지 않는다.

이번 범위는 주문, 배송/운송, 창고 관리, 음식점 관리, 배차 관리의 대표 API와 실제 소비 코드 검토다. 출처 메타데이터를 9단계로 확대하고, 공통 상태·전이와 운영 전용 제외 효과를 비교했다. 결제·정산·개인정보 등은 운영 전용 제외와 단순 미구현을 계속 구분해야 한다. 모든 Controller 메서드에 대한 완전한 기능 대응표는 아직 없다.

## 분야별 대조

| 분야 | 운영 원천/API | 실제 Simulation 대응 | 확인 수준과 남은 범위 |
| --- | --- | --- | --- |
| 주문 | `음식주문Controller`, `api/v1/food-orders`: 등록·목록·상세·수령 확인. 등록/수령 Handler와 UseCase | `Simulation음식배달.cs`, `가상주문흐름.cs`: 세션 주문·수락 후 대기·재시도·수령 상태 | 대표 음식 주문 흐름 존재/시험 통과. 운영 메뉴 검증·현재 사용자 권한·운영 Event/원장과 동일 구현은 아님. 같이주문·마트 등 주문 전 범위 이관은 이번 검증 아님. |
| 배송/운송 | `기사운송진행Controller`, `api/v1/driver/transports`: 상차 도착/완료·하차 도착·인수·문제/예외 신고. `음식배달기사업무Controller`, `api/v1/driver/food-deliveries`: 제안 수락/거절·픽업/전달·위치·경로 | `SimulationFreightTransport.cs`, `가상배달대기.cs`: 운송 상태 이력·Tick 이동·별도 인수 Confirm·Save/Replay | 정상 흐름과 차단/재생 검사 통과. 운영 GPS·기사 앱 근무·문제 신고·묶음 제안·정산 전부의 가상 대응은 미확인. 운송 API와 가상 정책은 별도 구현이다. |
| 창고 | `창고작업Controller`, `api/v1/warehouse-operations`: 창고/사용자·입고·검수·적치·포장·피킹·출고 인계·재위탁. `OutboundBatchEngine.CreatePlan` | `SimulationWarehousePutAway.cs`, NPC 검수 작업, `가상동네보충.cs`: 검수/적치·유한 합성 재고 예약/피킹/포장/보충 인계 | 공통 `창고출고배분Policy`의 실제 호출을 양쪽에서 확인. 단 가상 표본은 2종 물품/고정 보충량 등 별도 입력을 사용한다. 창고 관리/사용자 CRUD·재위탁 전체까지 공유한 것은 아님. |
| 음식점 | 음식 주문 API의 `restaurant/inbox`, `restaurant-acceptance`, `restaurant-progress`; 음식점 탐색·리뷰·운영자 접근·가격 정책 API | `Simulation음식점응답.cs`, `Simulation음식점조리.cs`: 담당 권한·수락/거절·조리 슬롯/순서·조리 완료 후 픽업 대기 | 수락/조리와 중간 저장 재진입 확인. 조리 완료만으로 배송을 확정하지 않음. 메뉴/리뷰/운영자 계정/가격 정책까지의 이관 근거는 이번 확인 범위에 없음. |
| 배차 | `api/v1/dispatch/wait`, `api/v1/driver/dispatch-actions`, `api/v1/admin/dispatch-plans`; `음식배달배차업무정책`, 화물 추천/기사 대기 점수 | `SimulationFreightDispatch.cs`, `가상배달대기.cs`: 후보 평가·용량/위치 노후 거부·선택 Confirm·배정/재탐색 | 픽업 평가·정렬, 화물 대기/추천 점수 등 공통 규칙 사용 존재. 운영 배차 큐·제안 만료·취소·동시 수락·알림/Outbox 전체를 가상 엔진에 이식한 것은 아님. |

## 핵심 발견

1. **출처 표기는 9단계로 확대됐지만 여전히 전체 이관 검사는 아니다.** 주문, 음식점 응답/조리, 음식 배차, 화물 배차/운송, 창고 적치/출고, Unity 음식 상태 표시를 `food-workflow-lineage`에서 조회한다. `SharedRuleCall`은 양쪽의 실제 공통 규칙 호출만 사용하고, 창고 적치처럼 의미만 대응하는 구현은 `SemanticAdaptation`으로 남겼다.
2. 기존 [업무흐름규칙Catalog](../../Ssalddel.WorkflowRules/UnityPackage/Runtime/업무흐름규칙Catalog.cs)에는 `SourceCapabilityKey`, `SourceContractRevision`, `RuleRevision`, `SourceStableIds`, 제외 운영 효과가 이미 있다. 이를 새 메타데이터와 연결해야 하며 별도 병렬 대장을 만들 필요는 없다. 이 문자열 자체가 현행 운영 코드와 자동 동등성을 보장하지는 않는다.
3. 배송 상태 규칙은 [운영 기사 전이 Policy](../../Ssalddel/Application/Driver/Transport/Services/기사운송상태전이Service.cs)와 공통 Catalog에 각각 선언돼 있다. 대표 상차→하차→인수 순서는 대응하지만, 운영은 사용자 명령/UTC이고 가상은 Tick에서 상차 도착과 완료를 같은 시점에 기록하기도 한다. 그대로 같은 실행기로 취급하면 안 된다. 관련 시험이 각각 통과한 것과 모든 전이 쌍의 동등성 검사는 다르다.
4. 창고는 이름뿐인 대응이 아니다. [운영 OutboundBatchEngine](../../Ssalddel/Services/LogisticsProcessing/Warehouse/OutboundBatchEngine.cs)과 [가상 보충](../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상동네보충.cs)이 같은 [창고출고배분Policy](../../Ssalddel.WorkflowRules/UnityPackage/Runtime/창고출고배분Policy.cs)를 호출한다. 기존 운영 시험은 150 seed의 동결 참조 구현 비교도 포함한다. 그 150회는 test case 150개로 중복 합산하지 않는다.
5. [기존 이관 대장](../AI/generated/operational-unity-transfer-catalog.md)의 `MappedCandidate`는 후보 분류이지 실제 이관 완료가 아니다. 파일 경로/페이지 관계만으로 전체 API·Unity 실행 증거로 승격하지 않았다.

## 이번 실행한 검증

산출물 폴더: `artifacts/local/validation/five-domain-transfer-audit-20260909/`.

| 시험 묶음 | 결과 | 범위 |
| --- | --- | --- |
| `five-domain-server.trx` | 34/34, 실패0 | 음식 주문 Controller, 음식점 수락/진행 Handler, 기사 운송 전이, 창고 출고 배분, 음식 배차 정책, 기사 배차 수락 Handler |
| `five-domain-simulation.trx` | 90/90, 실패0 | 음식 주문/배달, 가상 주문 흐름, 화물 배차/운송, NPC 검수/적치, 음식점 조리 인계, 공통 규칙, 가상 동네 하루, 출처 메타데이터 |
| `five-domain-unity-library.trx` | 33/33, 실패0 | 음식 배달 관찰·음식점/창고 정책 카드·창고 World·역할 NPC 운송 데이터 흐름 |

합계 **157/157**, skip0. 프로젝트 컴파일 후 자동 시험을 실행했다. 서버 시험의 Fake/메모리 저장소 및 Simulation 로컬 표본/Save 시험이며, 기존 Docker 운영 원장을 호출하는 live 업무 실행은 하지 않았다. Unity 라이브러리 시험은 Editor/Play/Game View 증거가 아니다. 직전 회차 50개와 겹치므로 누적 207개라고 세지 않는다. 이전 Task의 E 책임 미표기 차단을 이번 부분 회귀로 해소했다고 보고하지 않는다.

## 후속 출처·차이 검증 — 이번 구현

- `food-workflow-lineage`의 필수 단계를 3개에서 9개로 확대했다. 운영 원천 파일, 실제 공유 규칙 파일, 입력·시간·권위 차이를 각 코드 특성에 기록했다.
- 음식 주문은 실제로 읽는 공통 `업무흐름규칙Catalog`를 표시했고, 창고 적치는 직접 호출하지 않는 Catalog 표기를 제거했다. 화물 배차의 단계는 미리보기 전용이 아니라 Confirm을 포함하는 상태 변경 경계로 표시했다.
- `운영업무출처MetadataTests` 8/8: 아홉 단계 종류·상대 경로·권위 분리, 운영/Simulation 양쪽의 공유 정책 실제 호출을 확인했다.
- `업무흐름규칙ParityTests` 13/13: 음식 상태 목록, 음식점 거절/픽업 준비, 화물의 모든 상태 쌍, 창고 상태 정규화와 업무별 운영 전용 제외 효과를 비교했다.
- 산출물: `artifacts/local/validation/five-domain-source-lineage-20260909/{source-lineage,operations-simulation-diff}.trx`.
- 코드 지도 생성과 `--feature food-workflow-lineage` 조회가 통과했다. 현재 출력은 정확히 9단계를 보여 준다.
- `Ssalddel.Simulation.slnx`, `Ssalddel.Unity.slnx` 빌드는 경고0/오류0, `Ssalddel.v3.5.slnx` 빌드는 오류0으로 통과했다. 전체 제품 빌드의 경고60은 기존 `DriverApp` AndroidX 버전 제약 및 nullable 경고이며 이번 파일에서 새로 만든 경고가 아니다.
- 범위 Fast는 diff와 코드 지도까지 통과한 뒤 기존 E 책임 미표기 3개(`가상동네배치기준Tests`, `가상동네하루Tests`, `운영지도AreaViewModel`)에서 중단했다. 이관 검사를 통과시키기 위해 타 작업의 책임 표기를 임의 수정하거나 검사를 완화하지 않았다.

이 검증은 운영 DB·서버 호출이나 Unity Editor 실행이 아니다. 기존 공통 규칙을 양쪽에서 실제 호출하는 경우와 상태 의미만 재구성한 경우를 구분하고, 운영 효과가 Simulation으로 새어 들어오지 않는지를 자동 검사하는 기준선이다.

## 다음 보완 우선순위

1. 운영 API별로 `공통 규칙 / 가상 변형 / 운영 전용 제외 / 미이관 / 미검증`을 더 세밀하게 유지한다. 우선 대표 음식 주문 1건의 수락→배차→픽업→전달→수령과 실패/재시도를 같은 상태 사본 판본으로 닫고, 독립 화물·창고 흐름은 별도 slice로 검증한다.
2. 동일 요청 재시도, 권한/판본 불일치, 동시 배정 및 취소처럼 상태 목록만으로 잡히지 않는 차이를 계약 표본으로 확대한다. UTC↔Tick 등 의도된 차이는 명시하고 동등성 조건을 제한한다.
3. Unity에는 운영 Handler나 DB 객체를 넘기지 않고 Simulation의 `StableId/Revision/StateCode/Tick` 상태 사본과 읽기 전용 표현 투영만 넘긴다. 음식 배달 기사·음식점·주문자, 화물 차량·창고·목적지 객체는 이 사본을 소비하는 표현 계층으로 두고, 실제 이동·중단·복귀·재진입은 별도 Editor/Play/Game View 관문에서 확인한다.

이번 후속은 메타데이터 특성·manifest·자동 시험·생성 코드 지도·문서를 변경했다. 실제 업무 실행 본문·DB·Scene·상태 판정·commit/push 변경0.
