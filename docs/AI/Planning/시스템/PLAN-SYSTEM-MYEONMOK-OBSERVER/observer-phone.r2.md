# NPC 앱·업무 서버 관찰 r2

상태: Approved / ImplementationInProgress (2026-09-07). 사용자가 음식 배달 3역할·NPC 선택형 관찰·기본 5분 검증을 선택하고 `Implement the proposed plan.`으로 승인했다. 이 판본은 [휴대폰 r1](observer-phone.r1.md)의 검증 기본 시간과 서버 연결 제외 범위를 이번 격리 검증에 한해 대체한다. 기존 로컬 생활·저장·30분 시나리오와 과거 증거는 보존한다.

## 확정 범위

- 실제 실행 검증 기본 300초. 빌드/서버 준비는 별도이며 실패 시 자동 연장하지 않는다. 세계 수명·근무·휴식 규칙은 단축하지 않는다.
- 가상 주문자 1·음식점 1·기사 2와 음식 주문 1건. 정상 로그인과 기존 역할 API로 등록→수락→추천→기사 수락→조리(최소 1분)→픽업→전달→수령 확인한다.
- 별도 Docker Compose 프로젝트·DB·볼륨. `Simulation` 유지, 실제 지급·외부 알림·지도 호출 차단. 기존 운영 서비스의 주소/비밀/DB를 가져오지 않는다. 테스트 전용 좌표 제공자는 명시적 표본이며 실패 fallback이 아니다.
- 기존 자동 배차 스케줄러의 Operational 관문은 변경하지 않는다. 격리 검증 실행기가 자신의 주문 ID만 기존 배차 서비스에 전달한다. 이는 운영 스케줄러 작동 증거가 아니다.
- NPC 실행기는 API를 호출하고 Unity는 재조회한 결과를 읽는다. API/DB 오류를 성공 연출로 숨기지 않으며 기존 Local Simulation과 동시에 같은 주문을 진행하지 않는다.
- Figma `0KhuQLc1MleUBIQnARC21Z / 2269:177`의 같은 주문번호·역할 카드·상태 칩 구성을 기존 휴대폰에 적용. NPC 선택은 관찰 대상 변경이며 업무 사용자 권한 변경이 아니다. 전체 MAUI 앱 재구현은 제외한다.

## 구현/표현 인계

개발은 Hongdal 서버 검증 경계·기존 API 실행·영속 재조회·검증 스크립트·문서를 소유한다. 월드·공간·배치 담당은 별도 Unity 저장소의 opt-in 업무 서버 프로필·Adapter·휴대폰/도형 표현·시험·실제 캡처를 소유한다. canonical `SimulationWorldShell`과 기존 Scene/슬롯을 보존한다.

검증 전용 조회 계약: `GET /verification/food-delivery`, 명시적 시작 `POST /verification/food-delivery/start`, `X-Verification-Key` 인증. 개발용 격리 환경에서만 등록한다. schemaVersion `food-delivery-observer.r1`, runId/revision/status/elapsedSeconds/durationSeconds/orderNo/orderStatus/dispatchStatus/message, actors(id/name/role/state/nextAction/waitReason/lastAction/x/z), events(sequence/actorId/action/result/occurredAtUtc). 정상 업무 DTO를 이 조회 결과에 매핑하고 Simulation 상태 사본으로 위장하지 않는다. 비밀은 ignored 로컬 연결 파일로만 전달한다.

## E7 영향과 검증 상한

기존 `neighborhood-life-accept/cook/assign/move/pickup/deliver/receive`의 업무 의미를 재사용하는 별도 통합 검증이다. 기존 Goal·WI ID·승인 hash·E0·승격 비활성은 덮지 않는다. 이 문서와 연결된 통합 작업 명세가 이번 변경 범위를 소유한다.

Logic E7→E1: 재연결/동일 주문 복구→권한·멱등·DB/Event 시험→정상 업무 API 결과→역할별 업무 입력→영속 상태/기존 정책→독립 표본 주체→관찰 목적. Presentation E7→E1: 실제 5분 입력/판독→끊김/재시도→canonical Scene 연결→Figma 카드 후보→조회 모델→같은 주문/배우 연결→관찰자 선택. E1→E7은 역순으로 조립한다. 실패는 해당 가장 이른 책임으로 반환한다.

표현 후보 `visual:food-delivery-observer.r2`: 기존 uGUI·도형·한국어 글꼴. Figma는 정보/시각 구성의 근거이지 서버 상태가 아니다. 서버 미준비 시 화면은 연결 오류로 남는다. E5는 실제 조회 결과 결속, E6는 전이/재연결, E7는 실제 입력/5분 관찰로 각각 검증하며 자동 승격하지 않는다.

## 완료 판단

권한 위반·중복 요청·배차 경쟁/후보 조건·상태 전이·DB 독립 재조회·재처리 시험과 5분 Unity 관찰을 구분한다. 수행하지 못한 항목과 저장 성능 문제는 결과에 남긴다. 장시간 안정성·실제 영업/운영 준비·유료 외부 효과·마트/창고 확장·commit/push는 포함하지 않는다.
