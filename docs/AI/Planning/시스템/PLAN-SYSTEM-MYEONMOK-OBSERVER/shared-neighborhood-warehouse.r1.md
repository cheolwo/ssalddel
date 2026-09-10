# 같은 동네에 누적하는 마트 창고 업무 r1

기획 ID: `PLAN-SYSTEM-MYEONMOK-OBSERVER` / 판본: `shared-neighborhood-warehouse.r1`

상태: Approved / SharedMartInteriorImplemented / WarehouseReadBindingImplemented / AuthenticatedExecutionPending. 승인: 2026-09-08 사용자의 공통 공간·모드 보존 계획에 대한 `Implement the proposed plan`.

## 기준과 수용 범위

기존 도형 동네·공식 `SimulationWorldShell`·모드·슬롯·고유 식별자를 보존한다. 기능마다 새 공식 공간을 만들지 않고 기존 살뜰마트 내부 배치와 작업자에 기능을 누적한다. 마트와 보충창고는 별개 주체다. 로컬 생활과 서버 관찰은 같은 배치를 사용할 수 있지만 상태 권위와 실행은 분리한다. 기존 공간 좌표가 다른 음식점·주택을 강제 이동하거나 저장을 이주하지 않는다.

지금: 조회 시점과 연결 상태. 여기: 기존 마트 내부 입고·검수·선반·대기 지점. 나: 관찰자. 너: 기존 마트 작업자와 서버 재고/입고 작업. 이렇게: 관찰·선택·조회, 서버에서 확인된 입고/검수/적치 변화를 표현한다. 이안·효사·새 스토리·출고 확장은 이번 범위 밖이다.

## 수직 작업 명세

- 준비된 주체: 기존 `MartWorkerId`, 살뜰마트 배치, 서버 `warehouse:{id}`와 `warehouse-inventory:{id}`. 표현 작업자는 실제 근무자 신원/위치가 아닌 고정된 모의 역할이다. 실제 연결에는 권한 있는 창고 ID와 API 연결이 필요하다.
- 대표 상호작용: 기존 입고·검수·적치 업무 결과의 조회/관찰. 새 상태 권위·Goal·WI 번호는 만들지 않는다. 업무 실행은 기존 창고 UseCase/API만 소유한다.
- 직접 결과: 같은 재고 식별자의 상태·수량·보관 위치를 한 작업자와 기존 선반에 표현한다. 모의 동작이 업무 성공을 만들지 않는다. 작업 소멸만으로 완료를 추론하지 않고 재고의 적재완료/위치를 확인한다.
- 실패·회복·귀환: 연결/권한/계약 오류면 마지막 성공 자료를 유지하고 오류 표시, 재조회로 복구. 완료 후 같은 작업자는 대기한다. 알 수 없는 상태는 확인 필요로 표시한다.
- Logic E1~E4: 기존 업무 계약 재사용, 조회 매핑·주체/영역 범위·중복/역행 응답 검증. E5~E7: 실제 권한 API·원장 재조회·중복 요청 검증이 없으면 미검증.
- Presentation E1~E4: 기존 도형/역할 색·열린 내부·안전한 통로·식별자 결속. 후보는 Cube이며 외부 자산 조사 NotApplicable. E5: 실제 조회와 World 객체 결속; E6: 이동·중단·귀환·기존 모드 회귀; E7: 실제 Game View. 정적 시험으로 E 승격하지 않는다.
- 전문 연구: 새 지형/외부 모델/애니메이션 없음. 기존 승인 배치와 도형을 재사용하며 통로·중복 생성은 이번 집중 시험에서 검증한다.
- 쓰기: Unity 기존 도형 Controller/배경/관찰 휴대폰과 마트 공통 배치·조회 연결·집중 시험, Hongdal 이 문서·PLANNING·CURRENT_WORK. 공유 파일은 직전 재조회하고 최소 변경한다.

## 완료 기준

모드 보존, 공통 마트 배치, 단일 작업자, 안전한 이동 지점, 서버 실패의 명시적 표시, 재조회 결과 일치, 기존 음식 배달 회귀, 실제 화면 전·중·후 확인을 각각 기록한다. 검증용 가짜 응답 시험을 실제 운영 서버 연결로 보고하지 않는다. 원격 운영 DB·실주문·commit·push 없음.

## 결과

부분 구현: 기존 로컬 `BuildMart`와 서버 배경 조립이 같은 `마트내부조립`을 호출한다. 선반·포장대·인계대·작업자에 입고점·검수대를 누적하고, 조회 전 동적 재고·작업자는 숨긴다. 전체 음식점/주택 좌표와 각 모드 크기는 호환을 위해 유지한다. 새 공통 월드 원장이나 Scene은 만들지 않았다.

`마트입고관찰Repository`는 기존 권한 API `api/v1/warehouse-operations/world/zones/warehouse?warehouseId=...`를 GET으로만 호출한다. `마트입고관찰Store`는 범위/중복/시각 검증 뒤 같은 재고를 유지하며 입고 확인·검수 확인·적치 확인·대기를 판독한다. 작업 소멸은 완료로 추론하지 않는다. 표현은 실근무자 위치가 아닌 모의 역할이며 업무 명령을 발행하지 않는다. 실제 보관위치 두 개를 선반과 명시적으로 연결해야 하고 모르는 위치는 배치 연결 필요로 표시한다.

### 연결 방법과 현재 차단

기존 서버 관찰 프로필6에서 `Configure마트입고(IOperationalUnityApiClient, warehouseId, storageLocations)`로 기존 인증 연결을 주입한다. 두 `storageLocations`는 화면의 기존 선반1/2에 해당하는 실제 조회 위치 코드여야 한다. 별도 실행 모드는 만들지 않는다.

격리 시험에서는 `SSALDDEL_WAREHOUSE_OBSERVER_CONNECTION`에 로컬 연결 JSON의 절대 경로를 명시할 수 있다. 필드는 `baseUrl`, `accessToken`, `warehouseId`, `storageLocations`이며 endpoint는 기존 검증용 `http://127.0.0.1:5215/`만 허용한다. 연결 파일은 `artifacts/local/verification/warehouse-observer/` 등 비공개·비추적 경로에만 두고 token 값을 문서/Scene/로그에 기록하지 않는다. 음식 검증용 `accessKey`는 창고 사용자 인증을 대신하지 않는다. GET 이외·다른 창고 경로·redirect·proxy·cookie·표본 fallback을 허용하지 않는다.

실제 창고 권한 계정·입고 한 건을 준비하고 기존 정상 API로 입고 완료/검수/적치를 실행하는 검증 실행기는 이번에 구현되지 않았다. 따라서 **읽기 연결 구현과 전체 업무 자동 실행 완료를 구분한다**. 현재 격리 음식 검증 서버도 실행 중이 아니며, 일반 `hongdal-app-1`의 재시작 상태를 발견했지만 이 작업에서 해당 서버를 변경하거나 대체하지 않았다. 실제 서버 검증은 가장 이른 Logic E5 입력 결속부터 남아 있다.

### 검증

- 서버 기존 `창고WorldSnapshot조회UseCaseTests`, `적재작업UseCaseTests`, `재고현황UseCaseTests`: 11/11 통과. 일반 업무 계약 회귀이며 실제 MySQL/HTTP 실행 증거는 아니다.
- 최종 Unity CLI EditMode: 마트 입고·배경·편집 미리보기·기존 업무 배달 관찰 57/57 통과, 실패/건너뜀 0. 연결·위치 결속 보완을 포함한 최종 소스의 컴파일과 집중 회귀 결과다. `artifacts/local/validation/shared-neighborhood-warehouse/unity-final-editmode.xml` 참조. 최초 34/34 결과와 중복 합산하지 않는다.
- 문서 Fast 검증과 변경 소스 `git diff --check` 통과. 기존 서버 시험 11/11과 Unity 시험은 실제 인증 HTTP·MySQL·Play Mode 증거를 대신하지 않는다.
- 실제 View 캡처는 기존 월드·공간·배치 담당에 이번 범위만 의뢰했으나 연결된 Unity Pipeline 인스턴스가 없어 `Blocked`로 반환됐다. Game View PNG는 0장이고 최종 편집기 시험 성공을 화면 검증으로 대신하지 않는다. 한스/Blender 이전 작업 재개·Scene 저장·업무 생성 없음. 인증 미준비이므로 업무 전중후 증거를 가짜 응답으로 만들지 않았다.
- commit/push 없음. E5/E7 완료 또는 통합 완료 선언 없음.
