# 운영 배차 공통 코어 구현 준비 점검 r1

- 기준 기획: [운영 배차 공통 코어 r2](README.md)
- 점검일: 2026-09-10
- 상태: `AuditComplete / BackendWorkflowIntegrationPartial / ClientIntegrationDeferred`
- 점검 범위: 공통 업무 의미, 운영 모바일 앱, Simulation 공통 Runtime, Unity 관찰 경계, 역할별 수락·거절·중단·보호
- 검증 상한: 현재 작업트리의 소스·프로젝트 참조·집중 단위 시험. 실제 모바일 조작, 실제 서버 연결, 운영 DB·Redis, Unity Play Mode·Game View는 이번 점검에서 검증하지 않음

## 권위와 소비자 경계

| 층 | 의도한 책임 | 현재 확인 | 판정 |
| --- | --- | --- | --- |
| 운영 공통 계약 | 주문자·음식점·기사·플랫폼이 같은 배차 사건·사유·책임 코드를 사용 | 추가형 공통 DTO와 코드, 오늘·어제·그제 지표 계약과 기사·음식점 본인 지표 API를 구현. 공통 추천 시작·기사 거절, 음식 배달 수락·완료, 음식점 응답, 수락 전 주문자 취소를 자동 기록 | `BackendImplementedIntegrationPartial` |
| 운영 서버 | 인증·권한·최신 원장 검증 뒤 실제 주문·배차·배송 상태 확정 | Controller→UseCase/Command→서버 정책 구조와 기사 동시 3건 관문 존재 | `Partial` |
| 모바일 앱 | 실제 사용자의 명시적 업무 입력과 서버 재조회 | `OrdererApp`, `RestaurantDeskApp`, `FDriverApp`, `DriverApp`이 운영 계약·클라이언트 인프라를 참조하고 Simulation Runtime을 직접 참조하지 않음 | `ConfirmedByStaticAndTests` |
| Simulation Core | 가상 세션·Tick·Save/Replay의 권위 | `BusinessWorkflowRuntime`이 기존 Local/Remote Simulation Runtime을 묶고 `SimulationSession`만 허용 | `ConfirmedByStaticAndTests` |
| Unity | Simulation 또는 허용된 읽기 사본의 관찰 표현 | Runtime descriptor가 `AutonomousNpcWorld`, `ObservationPresentationOnly=true`, `AllowsOperationalDriverActions=false`를 강제 | `ConfirmedByStaticAndTests` |
| 운영–Simulation 연결 | 승인된 운영 자료의 읽기 전용 파생·동결 사본 | 일반 경계는 문서·Runtime에 있으나 새 배차 지표 투영은 아직 없음 | `Deferred` |

현재 `Ssalddel.BusinessWorkflow`의 공통성은 모바일과 Unity가 함께 호출하는 운영 코어라는 뜻이 아니다. Local/Remote Simulation이 같은 가상 업무 의미를 소비하는 facade다. 운영 모바일의 공통 의미는 `Ssalddel.Contracts`와 운영 서버 정책·원장에 두고, Simulation에는 별도 adapter나 동결 사본으로 전달해야 한다.

## 입장별 배려 구현 점검

| 입장 | 이미 확인된 배려 | 부족한 부분 | 판정 |
| --- | --- | --- | --- |
| 음식 배달 기사 | 인증된 수락·거절·픽업·완료 API, 서버의 동시 3건 안전 상한, 선택형 거절 사유 코드, 신규 배차 ON/OFF 분리, 유효 제안·수락·거절·완료 자동 원장과 오늘·어제·그제 지표 | 만료의 연결 오류 비귀속 판정, 보호 중단, 이의 제기, 시간당 정책 확정과 앱 화면 연결 없음 | `PartialReducedGap` |
| 화물 기사 | 거절·수락 취소 사유, 차량·혼적·구간 용량·시간창 재검증, 예외 신고 | 음식 배달과 공유할 사유·책임 코드 및 일별 지표 계약 없음 | `PartialReusableEvidence` |
| 음식점 | 주문 수락·거절을 유효 주문 사건과 같은 트랜잭션에 기록하고, 재고·영업시간·조리 용량 부적합 및 서비스 장애를 거절률에서 보호. 본인 단기 지표 API 제공 | 이의 제기와 운영자 정정, 최소 표본·감점 상한 없음 | `PartialReducedGap` |
| 주문자 | 주문 생성·목록·상세·수령 확인에 더해 음식점 수락 전 멱등 취소와 주문자 책임·음식점 제안 철회를 같은 트랜잭션에 기록 | 수락 이후 조리비·환불·기사 보상 취소 정책 없음 | `PartialReducedGap` |
| 플랫폼 운영자 | 후보 판단 감사와 개인정보 제거, 서버 권위 배차·동시 안전 관문 존재 | 역할 공통 활동 원장, 원인별 취소 반환, 운영자 정정·감사, 최소 표본·감점 상한, 시간당 소프트 목표 설정 없음 | `PartialHighGap` |
| Simulation·Unity 사용자 | 실제 기사 행위를 금지한 가상 세션·관찰 전용 경계 존재 | 운영 완료율과 분리된 게임 회복 계약, 배차 지표의 비권위 관찰 투영은 미정 | `BoundaryReadyFeatureDeferred` |

## 핵심 코드 차이

1. `FDriverApp`의 음식 배달 거절은 본문 없이 `POST .../reject`를 호출하고 서버도 제안 ID만 받는다. 기사에게 사유권을 준다는 기획과 다르다.
2. `기사운행시작/종료`는 Shift 생명주기를 표현하지만 신규 배차 수신 의사와 서버의 실제 배차 가능 상태를 두 값으로 제공하지 않는다.
3. `RedisDriverRejectedRequestStore`는 기사·의뢰 ID 집합과 TTL을 가진 재추천 방지 자료다. 날짜·유효성·사유·책임을 가진 비율 원장이 아니다.
4. 음식점 거절은 사유를 받고 서버 진행 변경 뒤 재조회하므로 재사용 가능한 선행 사례다.
5. 화물 기사 API는 거절·수락 취소 사유를 이미 받지만 음식 배달·음식점·주문자와 안정 코드가 통일되지 않았다.
6. 기존 동시 3건 상한은 안전 정책으로 유지해야 하며 시간당 3~5건 소프트 균형 목표로 바꾸면 안 된다.

## 리팩터링 묶음

### R0. 충돌과 기준선 확인

- 현재 배차·Driver·Runtime 관련 dirty 파일의 소유 작업과 완료 상태 확인
- 같은 파일을 동시에 수정하지 않도록 정확 쓰기 경로 확정
- 이 단계는 코드를 이동하거나 동작을 바꾸지 않음

### R1. 운영 배차 공통 의미

- `Ssalddel.Contracts`에 역할 공통 배차 사건·사유·책임·유효성·의사/실효 상태 계약
- 서버나 저장소에 의존하지 않는 오늘·어제·그제 순수 계산
- 동시 안전 상한과 시간당 소프트 목표를 다른 타입으로 표현
- 기존 공개 DTO·JSON을 깨지 않는 추가형 호환

### R2. 운영 원장과 판정 투영

- 영속 활동 원장을 권위로 두고 Redis는 재구성 가능한 판정 투영으로 제한
- 중복 사건 멱등성, 서버 시각, 운영 시장 날짜, Shift·제안·주문·주체·사유 기록
- 유효 제안 분모와 책임별 완료·중단 집계

### R3. 역할별 운영 API·모바일 연결

- 기사 거절 사유와 신규 배차 의사 ON/OFF
- 음식점 유효 주문·거절 사유 코드
- 주문자 취소·중단 사유와 진행 단계별 허용 조건
- 플랫폼 정정·이의 제기 감사
- 각 변경 뒤 같은 원장 재조회. 화면 조작은 최소화

### R4. 배차 후보와 공정성 적용

- 예상 도착시간, 유효 후보 기회, 시간당 편중을 설명 가능한 입력으로 조립
- 최소 표본·감점 상한·예외 배차를 먼저 확정한 뒤 실제 순위에 적용
- 운영자가 끌 수 있는 선택형 정책과 감사 기록

### R5. Simulation·Unity 관찰

- 운영 원장을 직접 쓰지 않는 읽기 전용 또는 동결 배차 정책 사본
- Simulation 회복은 운영 완료율과 분리
- Unity는 결과·이유·가상 NPC 생활만 표현하고 운영 Command를 호출하지 않음
- 코드·EditMode와 실제 Play Mode·Game View를 별도 검증

R1의 추가형 계약·순수 계산, R2의 MySQL 원장과 Memory/Redis 판정 투영, R3 중 기사 본인 ON/OFF·상태·지표 API, 공통 추천 시작·거절, 음식 배달 수락·완료, 음식점 유효 주문·수락·거절·본인 지표, 음식점 수락 전 주문자 취소까지 현 스레드에서 구현했다. 수락 이후 취소·보상, 운영자 API와 보호 중단·정정, R4~R5는 계속 후속 범위다.

## 4차 음식점·주문자 결속 결과

- 서버 검증을 통과해 새로 저장된 음식 주문을 음식점의 유효 제안으로 기록한다. 음식점 수락·거절 사건도 각 주문 변경과 같은 DB 트랜잭션에 저장하고 재시도 때 중복하지 않는다.
- 음식점 거절은 일반 판단, 재고 부족, 영업시간 외, 조리 용량 부족, 서비스 장애의 안정 코드를 사용한다. 조건 부적합과 보호 사유는 사후 확인 시 원래 유효 제안도 거절률 분모에서 철회한다.
- 동일 주문의 음식점과 기사 사건은 주체 역할·ID까지 포함한 별도 cohort다. 음식점 수락은 기사 시간당 균형 점유나 기사 수락 후 완료율을 만들지 않는다.
- 주문자 취소는 `주문대기 → 취소`만 허용한 `food-delivery.v3` 규칙을 사용한다. 주문자 본인·요청 ID·예상 revision을 확인하고 주문자 책임 취소와 음식점 보호 제안 철회를 함께 기록한다.
- 음식점 수락 이후 취소는 조리비·환불·기사 보상 정책이 미정이므로 충돌 응답으로 닫아 둔다.

## 3차 업무 흐름 결속 결과

- 추천 라운드가 실제 시작될 때 유효 제안 사건을 배차 상태·판단 감사와 같은 DB 저장에 넣는다. 기사 거절도 추천 상태 전환과 같은 저장에 공통 사유 코드를 남긴다.
- 음식 배달 단건·묶음 수락은 주문별 수락 사건을 기존 `Serializable` 트랜잭션에 포함한다. 고객 전달 완료 사건도 주문·운송 종료와 같은 트랜잭션에 포함한다.
- 허용 거절 사유는 거리 부담, 시간 제약, 수행 용량 부족, 개인 사정, 기타, 미입력이다. 자유문장은 원장 코드로 받지 않으며 사유가 없어도 기존 API 호출은 `NotProvided`로 계속 동작한다.
- 단기 지표에 수락률을 추가하고 거절률과 같은 유효 제안 분모를 사용한다. 업무 트랜잭션이 직접 사건을 추가한 뒤에도 조회는 영속 원장을 다시 읽어 오래된 당일 투영을 반환하지 않는다.
- 추천 만료는 아직 무응답으로 기록하지 않는다. ON 의사·서버 실효 상태·전달 성공을 결속하기 전에는 기사 책임으로 자동 귀속하지 않는 보호 경계다.

## 1차 구현 결과

- `Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs`: 기사 수신 의사와 서버 실효 상태, 역할·사건·유효성·책임 코드, 활동 사건과 일별 지표 계약
- `Ssalddel.Domain/배차/운영배차수신상태Policy.cs`: 연결 오류·서버 일시정지가 기사 ON/OFF 의사를 덮어쓰지 못하게 하는 무효과 상태 정책
- `Ssalddel.Domain/배차/운영배차단기지표Calculator.cs`: `Asia/Seoul` 달력의 오늘·어제·그제, 유효 제안 분모, 수락 날짜 cohort의 완료·중단·취소·균형 반환을 계산하는 무효과 계산기
- `Ssalddel/Services/Dispatch/Common/운영배차공통Ports.cs`: 영속 원장과 재구성 가능한 판정 투영을 분리한 포트
- 같은 Stable ID의 동일 사건은 한 번만 세고 내용이 다르면 거부한다. 수락이 어제이고 완료가 오늘이어도 수락 후 완료율은 어제 수락 cohort에 귀속한다.
- 계산과 포트는 배차 순위를 바꾸거나 책임을 확정하지 않는다. 미정 정책은 기본 비활성 상태로 남겼다.

## 2차 백엔드 구현 결과

- `운영배차활동사건` MySQL 테이블, 주체·발생 시각 및 주문·제안 인덱스, 한정 EF 마이그레이션을 추가했다.
- Stable ID의 동일 요청은 한 번만 저장하며 같은 ID의 다른 의미는 거부한다.
- `TransientState:Provider`가 `Memory`면 메모리 투영, `Redis`면 TTL이 있는 Redis 수신 상태·단기 지표 투영을 등록한다.
- Redis 투영이 비었거나 연결에 실패해도 MySQL 원장에서 수신 상태와 지표를 재구성한다. Redis는 책임·운영 결과의 권위가 아니다.
- 기사 인증 주체만 본인의 수신 상태 조회, 명시적 ON/OFF 변경, 오늘·어제·그제 지표 조회 API를 사용할 수 있다. 범용 사건 기록과 서버 실효 상태 변경은 내부 UseCase에만 남겼다.
- 로컬 Redis `localhost:16379`에서 저장·재조회·TTL을 실제 확인하고 시험용 고유 키를 제거했다.
- 마이그레이션 SQL은 운영 배차 테이블 하나만 생성함을 확인했다. 고유한 임시 MySQL 데이터베이스에 DDL을 실제 적용해 테이블 1개와 기본키·보조 인덱스를 확인한 뒤 임시 데이터베이스를 삭제했다. 기존 개발 DB에는 선행 미적용 마이그레이션이 있어 업데이트하지 않았다.

## 개발 시작 체크리스트

- [ ] 공통 코어 기획의 미정 질문이 필요한 수준까지 닫힘
- [x] 사용자가 현 스레드에서 공통 코어 코드 리팩터링·개발을 명시적으로 요청함
- [ ] 묶음 배차 건수 단위 확정
- [ ] 시간당 계산 구간 확정
- [ ] 최소 표본·감점 상한 또는 명시적 비활성 기본값 확정
- [ ] 보호 중단·책임 판정·이의 제기 최소 계약 확정
- [x] 현재 dirty 파일을 확인하고 기존 앱·배차·Unity 수정 파일을 1차 쓰기 범위에서 제외함
- [x] 첫 배치를 새 공통 계약·도메인 계산·서버 포트·집중 시험으로 제한함
- [x] 공통 계약 직렬화·계층 메타데이터·계산 규칙 집중 시험을 확정함
- [x] 실제 운영 활성화와 Simulation·Unity 적용이 별도 관문임을 유지함

## 이번 검증

- `BusinessWorkflowRuntimeCompositionTests`: 12/12 통과
- `FDriverRealtimeAuthRecipientCompositionTests`, `RoleAppRealApiDefaultCompositionTests`, `음식배달기사활성업무PolicyTests`: 합계 23/23 통과
- 운영 배차 계약·정책·계산·EF 원장·복구·API·DI·Redis 실제 연결 및 3차 업무 사건 결속 집중 시험: 합계 32/32 통과
- 4차 음식 주문·음식점·주문자·공통 규칙·원장·Controller 집중 회귀: 169/169 통과
- 결과 파일: `artifacts/local/validation/dispatch-core-audit/dispatch-boundary-simulation.trx`, `dispatch-boundary-operations.trx`
- 백엔드 최종 결과 파일: `artifacts/local/validation/operational-dispatch-core/operational-dispatch-backend-final.trx`
- 업무 흐름 결속 최종 결과 파일: `Ssalddel.Tests/TestResults/operational-dispatch-workflow-final3.trx`
- 음식점·주문자 결속 최종 결과 파일: `Ssalddel.Tests/TestResults/operational-dispatch-food-final4.trx`
- EF 한정 SQL: `artifacts/local/validation/operational-dispatch-core/operational-dispatch-ledger.sql`
- 4차 범위 Fast·Task는 `git diff --check`와 Simulation Unity 코드 지도까지 통과한 뒤, 다른 동시 작업의 E 책임 메타데이터 누락 8건에서 시험 실행 전에 중단됐다. 로그는 `artifacts/local/validation/20260910-203347/evidence-map-check.log`, `artifacts/local/validation/20260910-203418/evidence-map-check.log`다.
- 별도 v3.5 전체 빌드는 경고60·오류0이고 Simulation 전체 시험은 1,895/1,895 통과했다. 서버 전체 시험은 5,039개 중 5,032개 통과했고 기존 WebApp capability 1건·UI CSS 1건·기존 Controller metadata 5건이 실패했다. 새 음식점·주문자 결속 실패는 없다.
- 실제 모바일 조작·운영 서버·DB·Redis·Unity Play Mode·Game View: 미검증
