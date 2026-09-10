# 오행 업무 규칙 엔진

## 목적

`오행업무규칙Engine`은 주문·음식점·배차·배송·창고의 오래 유지되는 업무 의미와 기존 상태 전이 규칙을 하나의 순수 규칙 포트로 조립한다. 서버와 로컬 Simulation은 같은 계약을 참조할 수 있지만, 엔진은 운영 저장이나 Simulation 상태 변경의 권위를 갖지 않는다.

이 문서의 괘 대응은 업무 의미를 찾고 설명하기 위한 비권위 메타데이터다. Controller·UseCase·Command·Domain·DB·Event·Outbox가 가진 실행 책임을 대신하지 않는다.

## 엔진 자체의 조립 의미

다섯 업무 모듈 각각의 대표 괘와 별개로, 엔진 전체는 다음 두 의미를 조합한다.

| 구분 | 괘 | 엔진에서의 의미 |
| --- | --- | --- |
| 주축 | 토(간괘, `GAN`) | 모듈 경계, 허용 전이, 판본과 실패 경계를 산처럼 고정한다. |
| 보조 | 수(감괘, `GAM`) | 서버·로컬 Simulation·Unity 사이의 요청과 상태 사본 흐름을 물처럼 이어 준다. |

이 조합은 `five-element-workflow-engine-meaning.r1` 판본의 설명 메타데이터다. 개별 업무 전이의 대표 괘를 바꾸거나 상생·상극으로 명령을 자동 라우팅하지 않으며, `isExecutionAuthority=false`를 유지한다. `I오행업무규칙Engine.Engine정보조회()`로 조회할 수 있고 호출자가 반환값을 수정해도 엔진의 내부 기준은 바뀌지 않는다.

## 다섯 업무 모듈

| 모듈 | 대표 괘 | 의미 판본 | 보존하는 의미 |
| --- | --- | --- | --- |
| 주문 (`Order`) | 목(진괘, `JIN`) | `order-workflow-meaning.r1` | 수요를 주문 원장으로 열고 이행 대상을 식별한다. |
| 음식점 (`Restaurant`) | 화(리괘, `RI`) | `restaurant-workflow-meaning.r1` | 주문 수락·거절, 조리, 픽업 준비를 구분한다. |
| 배차 (`Dispatch`) | 토(간괘, `GAN`) | `dispatch-workflow-meaning.r2` | 요청과 기사·차량 후보 사이에서 담당 주체와 인계 경계를 확정한다. |
| 배송 (`Delivery`) | 수(감괘, `GAM`) | `delivery-workflow-meaning.r1` | 픽업 대상을 등록 경로를 따라 목적지로 이동해 전달한다. |
| 창고 (`Warehouse`) | 금(태괘, `TAE`) | `warehouse-workflow-meaning.r1` | 입고·검수·적재와 출고·분리를 통해 시설의 입구와 출구를 유지한다. |

배차와 배송은 같은 모듈이 아니다. 배차는 담당 주체와 인계 경계를 고정하므로 간괘이고, 배정 뒤 실제 이동·픽업·전달은 감괘다. `dispatch-workflow-meaning.r1`의 감괘 기록은 역사 판본으로 보존하며 r2 이후 판정에 소급 적용하지 않는다.

## 변하지 않는 부분과 바뀔 수 있는 부분

변하지 않는 공통부는 다음과 같다.

- 업무 흐름 코드와 상태 코드
- 허용 상태 전이와 멱등 재시도 판정
- 다섯 모듈의 안정 식별자와 의미 판본
- 운영 효과를 Simulation에서 제외하는 경계
- 같은 요청을 어느 업무 의미 모듈이 설명하는지에 대한 결정적 판정

환경별로 바뀔 수 있는 부분은 다음과 같다.

- 운영 서버의 인증·권한·DB 저장·Event·Outbox
- Hosted Simulation을 호출하는 HTTP 전송
- 로컬 Simulation의 메모리 세션 실행
- Unity의 Prefab·MonoBehaviour·애니메이션·UI 표현
- 웹 화면과 디자인

따라서 엔진 모듈은 DB, `HttpClient`, Unity 객체, 화면을 참조하지 않는다. 환경별 Adapter가 공통 포트를 호출하고 결과를 각 환경의 기존 실행 경계에 전달한다.

## 코드 조립

```text
운영 서버 Controller
  → 기존 UseCase / Command / Domain
  → 업무상태전이Policy
  → I오행업무규칙Engine
     ├─ Engine정보조회 (GAN 주축 / GAM 보조, 비권위 메타데이터)
     └─ 기존 업무흐름규칙Catalog 판정
  → 운영 DB / Event / Outbox (기존 소유자만 실행)

Unity 또는 다른 로컬 클라이언트
  → I오행업무Runtime
     ├─ Orders / Restaurants / Dispatch / Delivery / Warehouse
     ├─ LocalProcess: LocalSimulationRuntime
     └─ RemoteHost: 기존 Simulation HTTP API
  → 같은 Simulation 상태 사본 재조회
  → 음식배달ActorPresentationProjector
  → 가상배달관찰Controller가 소유 GameObject의 위치·표시만 갱신
```

`I오행업무규칙Engine`은 엔진 의미 조회, 모듈 목록 조회와 상태 전이 판정만 제공한다. `요청ModuleCode`를 생략하면 업무 흐름과 목표 상태로 모듈을 결정하고, 명시하면 실제 전이 의미와 일치하는지 검사한다. 불일치는 상태를 변경하지 않고 차단 결과로 반환한다.

기존 `업무상태전이Policy`는 호환 API를 유지한 채 엔진을 통과한다. 따라서 기존 서버와 Simulation Domain 호출자는 DTO나 공개 상태 코드를 바꾸지 않고 같은 허용 전이를 사용한다.

## 음식 주문·배달 흐름의 모듈 경계

```text
주문대기
  → 음식점 수락·조리·픽업준비      Restaurant / RI
  → 기사배정                       Dispatch / GAN
  → 픽업완료·전달완료              Delivery / GAM
  → 수령확인                       Order / JIN
```

한 주문의 상태 원장은 계속 하나다. 위 구분 때문에 주문을 모듈별로 복제하거나 별도 DB 문서를 만들지 않는다. 모듈은 현재 전이가 어떤 안정 업무 의미에 속하는지 설명할 뿐이다.

## 로컬과 원격 Simulation

`I오행업무Runtime`은 기존 음식 주문·물류 Runtime을 주문·음식점·배차·배송·창고의 다섯 역할 포트로 노출하는 비상태 조립 계층이다. 역할별 포트는 모두 같은 facade와 같은 하위 Runtime 원장을 사용한다. 주문과 배송이 현재 음식 배달 Command의 일부 메서드를 함께 보더라도 주문 상태를 복제하지 않는다.

- `LocalProcess` 조립은 한 `LocalSimulationRuntime`을 음식 주문과 물류 양쪽 Adapter로 사용한다.
- `RemoteHost` 조립은 `RemoteSimulationFoodOrderRuntime`과 `RemoteSimulationLogisticsRuntime`을 기존 Simulation Server HTTP 경로에 연결한다.
- 원격 Adapter는 인증 정책이나 `HttpClient` 수명을 소유하지 않는다. 조립자가 구성한 Client를 받는다.
- 웹과 모바일은 `AddRemote오행업무Runtime()`으로 한 scope의 facade와 다섯 역할 포트를 등록한다. 개발용 호스트가 `AddLocal오행업무Runtime(...)`을 선택할 수 있지만 두 실행 위치를 동시에 등록하거나 실패 시 자동 전환하지 않는다.
- Unity Solo는 `SimulationWorldLocalRuntimeScope.BusinessRuntime`으로 기존 `Runtime`과 같은 로컬 원장을 노출한다. Unity 조립은 DI 컨테이너를 강제하지 않으며 MonoBehaviour가 업무 상태의 권위를 갖지 않는다.
- Local과 Remote 모두 운영 주문 DB나 실제 기사 배차를 직접 실행하지 않는다. Simulation 상태의 진행은 기존 Session Aggregate와 World Tick이 소유한다.

Unity의 NPC·건물은 이 포트로 받은 상태 사본을 표현할 수 있지만, NPC 한 명마다 별도 업무 엔진이나 서버 원장을 만들지 않는다. MonoBehaviour는 표시와 이동을 담당하고, 주문·배차·배송 상태의 권위는 서버 또는 선택된 Simulation Runtime에 남는다.

Unity에서 새 역할 포트를 명시적으로 받는 첫 소비자는 기존 `음식점관찰SceneController`다. `음식점관찰AutoBootstrap`은 세션·정책 포트는 기존 `LocalSimulationRuntime`에서 받고 음식 주문 포트만 `BusinessRuntime.Orders`로 전달한다. 명령 성공 뒤 같은 Session을 다시 조회하는 기존 흐름은 바꾸지 않는다.

기존 `가상배달관찰Controller`는 `SimulationWorldLocalRuntimeScope`에서 받은 한 합성 배달기사 상태 사본을 `음식배달ActorPresentationProjector`에 전달한다. 투영기는 Session·Revision·기사 안정 ID·주문 결속·좌표 유효성을 검사하고 표시 전용 모델을 만든다. 오래된 판본, 같은 Session에서 바뀐 기사 ID, 누락·중복 주문은 마지막으로 검증된 GameObject 위치를 바꾸기 전에 차단한다.

이것은 Local Simulation의 단일 기사 관찰 slice다. 공통 Runtime 조립이 추가되어도 여러 기사 Prefab을 안정 ID별로 생성·해제하는 수명 관리자, RemoteHost를 선택하는 Unity 제품 설정, 실제 Play Mode·Game View 검증은 별도다. 따라서 현재 구조를 실제 운영 서버 연결이나 다수 NPC 생활 장면 완성으로 해석하지 않는다.

## 확장 원칙

새 업무를 추가할 때는 먼저 기존 `업무흐름규칙Catalog`와 운영 계약이 있는지 확인한다. 실제 중복이 있을 때만 `I오행업무규칙Module` 구현을 추가한다.

- 모듈은 후보·규칙 판정만 반환한다.
- 저장, 알림, 결제, 실제 배차는 기존 UseCase가 수행한다.
- 의미 판본을 바꾸면 기존 판본을 삭제하지 않고 대체 관계를 남긴다.
- 서버·Local·Remote Adapter가 같은 상태 코드와 판본을 보존하는지 시험한다.
- 정적 시험 통과를 실제 Unity Play Mode·Game View 또는 운영 연결 성공으로 승격하지 않는다.
