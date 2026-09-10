# 업무 흐름 Runtime

## 목적

`Ssalddel.BusinessWorkflow`는 주문·음식점·배차·배송·창고 업무를 웹·모바일·Unity에서 같은 계약으로 소비하기 위한 공통 Runtime이다. 사람이 읽는 이름은 **업무 흐름 Runtime**이며, 공개 실행 API와 파일 이름에는 특정 철학 체계의 이름을 사용하지 않는다.

이 Runtime은 기존 업무 상태를 새로 소유하지 않는다. 로컬에서는 기존 Simulation Core를, 원격에서는 기존 Simulation HTTP API를 조립해 한 facade로 제공한다.

## 프로젝트 경계

| 프로젝트 | 책임 |
| --- | --- |
| `Ssalddel.Simulation.Contracts` | 상태 사본·요청·결과와 `ISimulationLogisticsRuntime` 계약 |
| `Ssalddel.WorkflowRules.Contracts` | 순수 업무 규칙 계약과 선택적 분류 메타데이터 |
| `Ssalddel.WorkflowRules` | 상태 전이·객체 결속의 결정적 규칙 |
| `Ssalddel.BusinessWorkflow` | 다섯 업무 포트와 `IBusinessWorkflowRuntime` facade |
| `Ssalddel.Simulation.Application` | `LocalProcess` 조립 |
| `Ssalddel.Simulation.Infrastructure` | `RemoteHost` HTTP 조립 |
| `Ssalddel.Client.Infrastructure` | 웹·모바일 DI 등록 |

공통 Runtime은 다음 포트를 제공한다.

- `IOrderWorkflowRuntime`
- `IRestaurantWorkflowRuntime`
- `IDispatchWorkflowRuntime`
- `IDeliveryWorkflowRuntime`
- `IWarehouseWorkflowRuntime`

다섯 포트는 한 `IBusinessWorkflowRuntime` facade와 같은 하위 상태 원장을 공유한다. facade를 등록했다고 별도 주문·배차·창고 상태가 생기지 않는다.

## 실행 위치 조립

```text
웹·모바일
  -> AddRemoteBusinessWorkflowRuntime(...)
  -> RemoteHost HTTP adapter
  -> Simulation Server의 기존 Core와 상태 권위

Unity Solo
  -> SimulationWorldLocalRuntimeScope.BusinessWorkflow
  -> LocalBusinessWorkflowRuntimeFactory
  -> 같은 LocalSimulationRuntime과 Session Aggregate
```

Local과 Remote는 명시적으로 선택한다. 원격 호출 실패를 로컬 실행으로 자동 전환하지 않으며, 인증·`HttpClient` 수명·재시도 정책은 기존 호스트 조립자가 소유한다.

Unity의 `MonoBehaviour`는 Runtime을 소유하거나 업무 상태를 판정하지 않는다. 기존 상태 사본을 읽고 NPC·건물·화물의 표시와 수명만 관리한다.

## 오행·괘상 분류의 위치

오행·괘상은 `WorkflowClassificationMetadata`에 남는 **선택적 설명 메타데이터**다.

- `IsExecutionAuthority`는 항상 `false`다.
- 분류 유무나 분류 값은 상태 전이, 객체 결속, 배차, HTTP route 선택을 바꾸지 않는다.
- 기존 JSON의 분류 필드는 호환 입력으로 읽을 수 있지만 일반 실행 계약의 필수값이 아니다.
- 새 실행 타입·메서드·파일 이름은 `BusinessWorkflow`, `WorkflowRule`, `BusinessObjectInteraction` 같은 보편 용어를 사용한다.

따라서 분류는 문서·검색·분석에 활용할 수 있지만 실행 권위는 계속 Controller·UseCase·Command·Domain·Simulation Session·DB/Event/Outbox에 있다.

## 안전 경계

- 공통 Runtime project는 Unity, MongoDB, ASP.NET DI, `HttpClient`를 직접 참조하지 않는다.
- 규칙 엔진은 후보·허용 여부·진단을 반환할 뿐 상태를 저장하지 않는다.
- 운영 효과와 로컬 Simulation을 자동 혼합하지 않는다.
- Unity Scene·Prefab·Save는 이 조립만으로 생성하거나 변경하지 않는다.
- 실제 서버 연결, Unity Play Mode와 Game View는 코드·단위 시험과 별도 증거다.
