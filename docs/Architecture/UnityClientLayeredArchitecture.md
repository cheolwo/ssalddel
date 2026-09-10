# Ssalddel Unity 클라이언트 계층 구조 설계

> 상태: 기준 설계와 현재 저장소 적용 상태
>
> 적용 대상: Hongdal의 `Ssalddel.Unity` 공통 package와 별도 `ssalddel` Unity 프로젝트
>
> 상위 제품 기준: [Unity 생산·유통·협력 경험 플랫폼](UnityCooperativeExperiencePlatformProposal.md)
>
> World·원장 책임 기준: [Unity World·원장 투영 아키텍처](UnityWorldLedgerProjectionArchitectureProposal.md)
>
> 데이터·시뮬레이션 기준: [Unity 농업·유통 시뮬레이션](UnityAgricultureDistributionSimulationProposal.md)
>
> Unity 읽기 데이터의 기본 변환 기준: [Unity Data·Interpretation·Presentation 기준 아키텍처](UnityDataInterpretationPresentationArchitecture.md)

## 먼저 보기 — 웹·MAUI에서 Unity로

웹에서 이해한 DI·API·ViewModel 구조를 그대로 출발점으로 삼으면 된다. 차이는 **상태를 결정하는 Core와 화면·물체를 표시하는 Unity 코드가 분리**된다는 것이다. DbContext를 Unity에 주입하지 않는다.

| 웹·MAUI에서 익숙한 역할 | Unity 대응 | 바꾸지 않는 것 |
| --- | --- | --- |
| 앱 시작과 DI | Bootstrap / CompositionRoot에서 생성 후 Initialize·Configure로 주입 | 업무 규칙 |
| API 호출 서비스 | Runtime 포트 또는 API Client·Repository | UI에서 DB 직접 접근하지 않음 |
| 서버 UseCase·Domain | Simulation Core의 명령 검증·NPC 규칙·Tick | 화면이 주문 완료를 결정하지 않음 |
| 응답 DTO | 특정 판본의 Snapshot | Unity 표시와 권위 원본은 별개 |
| ViewModel | 일반 C# Presenter와 읽기 전용 표시 Model | GameObject·애니메이션과 독립 |
| Razor·XAML | View / MonoBehaviour / Prefab | 입력 전달과 표시만 담당 |

읽기는 `권위 상태 → Snapshot → 관점·표시 Model → View`, 변경은 `View 입력 → Controller/Presenter → 명령 → Core 검증·변경 → 최신 재조회 → View`다. CompositionRoot는 이 순서 중 매번 통과하는 계층이 아니라 **시작할 때 연결하는 코드**다. NPC 자동 행동도 Core의 Tick이 실행하며 Animator 종료가 업무 완료를 뜻하지 않는다.

Solo는 `LocalSimulationRuntime`이 같은 Core를 Unity 프로세스 안에서 실행한다. Hosted는 HTTP를 거쳐 원격 Simulation Host가 실행한다. 운영 서버의 실제 주문·재고는 별개이며, 로컬 예제 성공은 HTTP·인증·운영 DB 연결 증거가 아니다. 모든 기능의 Local/Remote 어댑터가 이미 완성된 것은 아니다.

### 두 저장소와 음식점 한 기능의 코드 안내

- **Hongdal**: `Ssalddel.Simulation.Application/RuntimeCore`, `Ssalddel.Simulation.Domain/UnityPackage/Runtime`, `Ssalddel.Unity/Runtime/Warehouse`. 공통 Core·계약·표시 연결을 소유한다.
- **별도 Unity 저장소 `C:\Users\user\ssalddel`**: `Assets/Ssalddel/{Bootstrap,Infrastructure,Presentation}`와 공식 `SimulationWorldShell`. `Packages/manifest.json`의 로컬 package 참조가 Hongdal 코드를 연결한다.

| 음식점에서 찾을 책임 | 실제 코드 시작점 | 다음에 확인할 대상 |
| --- | --- | --- |
| 예제 선택·조립 | Unity `음식점관찰ProfileMenu`, `음식점관찰AutoBootstrap` | `SimulationWorldLocalRuntimeScope`의 프로필·저장 슬롯 |
| 입력 순서·중복 방지·수명 | Unity `음식점관찰SceneController` | 주입된 세션/주문 포트와 Presenter |
| 모의 초기 입력 | [음식점관찰표본](../../Ssalddel.Unity/Runtime/Warehouse/음식점관찰표본.cs) | 세션 생성 요청, 명시적 주문 Confirm 요청 |
| 정책 조회·변경·표시 행 | [음식점정책카드Presenter](../../Ssalddel.Unity/Runtime/Warehouse/음식점정책카드Presenter.cs) | `ISimulationNpcPolicyRuntime`, `음식점주문표시Model` |
| 권위 호출 | [LocalSimulationRuntime](../../Ssalddel.Simulation.Application/RuntimeCore/LocalSimulationRuntime.cs) | 세션 생명주기·Aggregate |
| 접수·조리 규칙 | `Simulation음식점응답.cs`, `Simulation음식점조리.cs` | 권한, FIFO, 자리, 작업 시작/종료 Tick |
| 화면 표시 | Unity `음식점관찰View` | 텍스트·입력·버튼·스크롤 목록, 업무 규칙 없음 |
| 검증 | `음식점관찰표본Tests`, `음식점정책카드Tests`, Unity `음식점관찰Tests` | 공통 규칙·표시·UI 결속의 서로 다른 증거 |

예를 들어 ‘조리 시간 변경’은 View 입력 → SceneController → Presenter.적용Async → LocalRuntime.UpdateNpcPolicyAsync → Aggregate 정책 변경 → GetPolicySessionAsync 재조회 → 표시 순이다. ‘1 Tick 진행’은 SceneController → 세션 포트 → Core의 접수·진행 중 조리·새 자리 배정 → Presenter 재조회 → 표시 순이다. 진행 중 작업의 종료 시간을 View가 다시 계산하지 않는다.

`Presenter`라는 기존 이름에는 일반 C#과 MonoBehaviour가 혼재한다. 이름만 보지 말고 상속과 생성 위치를 확인한다. 신규 음식점은 일반 C# Presenter와 Unity SceneController/View로 구별한다. 위 표는 책임 안내이지 모든 기능에 빈 Repository·UseCase 클래스를 추가하라는 규칙이 아니다.

E1~E7은 기능 검증 단계, WI는 상호작용, H는 공간 의미 계층이다. 데이터가 `E1 → H1 → WI → E2`처럼 통과하는 실행 모듈이 아니다.

### 음식점 예제 사용법과 제한

1. 별도 Unity 프로젝트에서 공식 `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`를 연다.
2. Play 중이 아닐 때 `Ssalddel > 실행 프로필 > 음식점 관찰 (수동 Tick)`을 선택하고 직접 Play한다. 메뉴는 Scene을 저장하거나 자동 실행하지 않으며 현재 Editor 프로세스에만 적용된다. 독립 실행 환경은 `SSALDDEL_RESTAURANT_OBSERVER=1`을 사용한다.
3. 샘플 주문을 여러 건 추가하고 `1 Tick 진행`으로 접수 대기 → 조리 배정/대기 → 조리 중 → 픽업 준비를 확인한다. 새 표본은 자리 1개·시간 2 Tick이며 Tick을 초로 해석하지 않는다.
4. 설정 적용은 미배정 작업부터 반영한다. ‘새 조리 배정’ 해제는 접수 거절이나 진행 중 조리 취소가 아니다. 실패하면 최신 상태를 확인하고 다시 입력한다.
5. `저장`은 `restaurant-observer-r1-primary` 슬롯에 저장한다. 다시 실행하면 마지막 저장 상태를 복원하며 오프라인 시간·자동 주문·자동 Tick을 보충하지 않는다. 저장하지 않은 변경은 재시작 시 보존되지 않는다. 표본 상한 100 Tick 뒤에는 새 주문·진행을 막는다.
6. 종료 후 `기존 기본 프로필` 메뉴로 복귀한다. 음식점 슬롯과 기존 자연 생존 슬롯을 삭제·변환하지 않는다.

현재 범위는 로컬 설정·주문 카드 연결이다. 3D NPC 조리·도로·배차·원격 서버는 포함하지 않는다. 실제 Play Mode·Game View 완주 여부는 [현재 작업](../AI/CURRENT_WORK.md)의 별도 검증 결과를 확인한다.

## 1. 목적

이 문서는 서버 중심의 기존 데이터와 업무 도메인을 Unity 클라이언트로 확장할 때 사용하는 코드 계층, 의존성 방향, Prefab·Inspector 결합 경계와 자동화 검증 기준을 정의한다.

Ssalddel Unity의 제품 지위는 일반적인 독립 농장 게임이나 Web 화면의 3D 복제가 아니다.

> **연구자료와 실제 데이터를 서버에서 권위 있게 관리하고, Unity에서 그 상태와 관계를 공간·센서·업무 오브젝트로 체험하게 하는 World Projection Client**

Unity는 서버 응답을 GameObject에 직접 넣지 않는다. transport, mapping, repository, UseCase, interpretation, presentation model, scene orchestration과 View를 단계적으로 통과시킨다. 구체적인 읽기 변환은 `Data → Interpretation → Presentation`을 기본으로 하고 Query/Command Application은 그 옆에서 흐름을 조율한다. View는 외부 3D asset을 연결하는 명시적 socket 계약을 제공하며, 향후 Editor Script와 Unity CLI가 반복적인 Prefab 생성·배선·검사를 수행할 수 있게 한다.

## 2. 현재 저장소 적용 상태

2026-09-06 두 저장소 소스 기준이다. 아래의 구현 존재와 실제 실행 검증은 구별한다.

| 영역 | 상태 | 근거와 제한 |
| --- | --- | --- |
| engine-independent Unity package | 구현 | `Ssalddel.Unity`, `netstandard2.1`, C# 9 |
| UnityEngine 격리 | 구현 | `Ssalddel.Unity.Data.asmdef`의 `noEngineReferences=true` |
| ApiModel·Mapper·DataManager | 구현 | 기능별 계약을 사용하며 공통 package는 Simulation.Contracts도 참조 |
| 결정적 농업 simulation | 구현·headless 검증 | golden fixture와 .NET tests |
| World projection core | 구현 | page catalog, stable-ID/revision reconcile |
| 연구 근거·센서 계약 | 초기 구현 | engine-independent model과 validator |
| UnityWebRequest API Client | 별도 Unity 소스 확인 | `Infrastructure/Transport/UnityWebRequestApiClient.cs`와 기능별 ServerRepository. 소스 존재는 서버 연결 증거가 아님 |
| 의존성 연결 | 기능별 방식 혼재 | 기존 sample의 VContainer와 별도 Unity의 생성자·Initialize/Configure 수동 주입을 구별. 전역 새 DI 프레임워크 도입 없음 |
| Unity 전용 의존성 | 별도 프로젝트가 소유 | 실제 Input System·Newtonsoft 사용. 엔진 독립 package와 동일 의존성으로 설명하지 않음 |
| Unity presentation sample | 구현·Editor 검증 | Urban Market·Traditional Market Hub Scene 생성·reload wiring; PlayMode·built player 미검증 |
| 시각 자산 | 별도 Unity에서 관리 | `VisualRoot` 아래 대체 가능한 표현. 음식점 카드 작업은 자산 가공·라이선스 검증을 하지 않음 |
| 음식점 관찰 카드 | 로컬 연결 코드 | 기존 Presenter + 실제 Unity View/Controller, 수동 Tick·별도 슬롯. 실행 검증은 CURRENT_WORK 참조 |

기존 진부 Hub 입고는 `진부Hub입고UiSceneCompositionRoot → ServerRepository → Coordinator → UiPresenter`를 통해 HTTP 연결 역할을 확인할 수 있다. 해당 어댑터에는 Local 미지원 차단도 남아 있으므로 모든 기능이 자동으로 두 모드를 지원한다고 설명하지 않는다.

## 3. 핵심 경계

### 3.1 서버

서버가 최종 권위를 갖는 값:

- 공개데이터 원본·정규화·출처·기준 시각·단위·지역·품질
- 사용자·조직 권한과 공개 범위
- 농장·주문·공동구매·운송·창고 원장과 revision
- 실제 참여, 주문, 배차, 검수, 입출고와 상태 전이
- Event·Outbox, 멱등성, 감사 기록

서버가 알지 않아야 하는 값:

- Synty Prefab 이름과 경로
- Animator state·parameter
- Material, Renderer, ParticleSystem과 AudioClip
- Unity Scene hierarchy와 Inspector reference
- client 전용 색상·motion·VFX 선택

### 3.2 Unity

Unity가 맡는 책임:

- API 조회와 인증 token 전달
- transport model의 역직렬화
- schema·단위·freshness·호환성 검사
- cache·마지막 성공 snapshot·fixture 상태 관리
- 사용자의 preview와 명시적 확인
- server-confirmed snapshot을 World object와 panel로 투영
- 입력, 카메라, 이동, animation, VFX, SFX와 접근성 feedback

Unity의 animation 완료나 GameObject 위치만으로 운영 상태를 확정하지 않는다.

```text
Unity interaction
  → preview
  → explicit confirmation
  → server Command
  → server authorization and revision validation
  → persistence and event
  → canonical snapshot re-query
  → Unity presentation update
```

## 4. 의존성 방향과 실행 흐름

의존성 방향과 runtime 요청 흐름을 구분한다.

### 4.1 코드 의존성 방향

```text
Unity Presentation
  ├─ SceneController / Input / View / Prefab adapters
  └─ Composition Root
          ↓
Unity Application
  ├─ UseCases
  ├─ Presenter
  └─ repository ports
          ↓
Ssalddel.Unity Core
  ├─ ApiModels
  ├─ Mapping
  ├─ GameModels / ProjectionModels
  ├─ Evidence / Sensors / Simulation
  └─ stable ID, revision and validation
```

Infrastructure adapter가 core의 repository port를 구현한다. core는 VContainer, UniTask, UnityWebRequest와 UnityEngine을 참조하지 않는다.

### 4.2 조회 실행 흐름

```text
SceneController
  → Load UseCase
  → Repository port
  → UnityWebRequest API Client
  → ApiModel
  → explicit Mapper
  → GameModel / ProjectionSnapshot
  → repository state store
  → UseCase result
  → Presenter
  → ScreenModel / PresentationCommand
  → View
  → Prefab VisualRoot
```

Repository가 mapping 정책을 직접 소유하지 않는다. Repository는 API Client, Mapper, cache와 state store를 조율하고, DTO→GameModel 호환성 판정은 독립 Mapper가 담당한다.

## 5. 권장 물리 구조

현재 `Ssalddel.Unity`는 engine-independent local UPM package로 유지한다.

```text
Ssalddel.Unity/
  Runtime/
    ApiModels/
    Mapping/
    Data/
    Simulation/
    WorldProjection/
    Evidence/
    Sensors/
    Interactions/
  DataSchemas/
  package.json
  Ssalddel.Unity.Data.asmdef
```

Unity project가 현재 monorepo 또는 별도 repository 중 어디에 위치할지는 ADR로 확정한다. 어느 쪽이든 presentation project는 다음 경계를 갖는다.

```text
Assets/Ssalddel/
  Runtime/
    Transport/
      ApiClients/
      Serialization/
      Authentication/
    Infrastructure/
      Repositories/
      Cache/
      Realtime/
    Application/
      UseCases/
      Presenters/
      ScreenModels/
    Presentation/
      SceneControllers/
      Views/
      Input/
      Camera/
      Animation/
      Effects/
    Composition/
      LifetimeScopes/
    Prefabs/
      Placeholders/
      Farm/
      Community/
      Logistics/
      Warehouse/
  Editor/
    PrefabAutomation/
    Validation/
  Tests/
    EditMode/
    PlayMode/
```

외부 asset은 별도 vendor 경계에 둔다.

```text
Assets/ThirdParty/Synty/
```

Ssalddel code가 vendor 내부 파일을 수정하거나 vendor component type을 core contract에 노출하지 않는다.

## 6. 계층별 책임

### 6.1 API Client

API Client는 HTTP transport만 담당한다.

- base URL과 route 구성
- method, header와 bearer token
- request timeout과 cancellation
- JSON deserialize
- HTTP status와 network 오류 분류
- correlation ID 같은 허용된 진단 정보 전달

API Client가 하지 않는 일:

- cache fallback 결정
- domain 상태 판정
- GameObject 생성
- 오류를 sample 성공으로 변환
- raw exception·token·개인정보 로그 출력

인터페이스 return type은 Unity 전용 adapter assembly가 선택한다. engine-independent core는 `Task`/`CancellationToken` 또는 결과 계약을 유지하고, Unity adapter에서 `Awaitable`이나 승인된 UniTask bridge를 사용할 수 있다.

### 6.2 ApiModel

ApiModel은 JSON wire contract를 표현한다.

- 서버 DTO source를 공유하지 않는다.
- nullable과 unknown code를 허용하고 Mapper가 호환성을 판정한다.
- JSON field name과 schema version을 고정한다.
- contract fixture로 server 응답과 소비 호환성을 검증한다.

ApiModel을 View, MonoBehaviour와 Prefab에 직접 전달하지 않는다.

### 6.3 Mapper

Mapper가 담당하는 변환:

- 필수 field와 schema version 확인
- wire code→client code 변환
- source·observedAt·unit·region·freshness 보존
- 단위 호환성과 명시적 normalization
- unknown·missing·stale·ambiguous 상태 반환
- server DTO와 Unity GameModel의 결합 차단

Mapper는 오류를 임의 기본값으로 숨기지 않는다.

```text
Mapped
UnsupportedSchema
MissingRequiredField
UnknownStateCode
IncompatibleUnit
Stale
InvalidStableId
```

### 6.4 Repository와 state store

Repository는 데이터 출처와 조회 정책을 숨긴다.

- API Client 호출
- Mapper 호출
- 마지막 성공 snapshot 저장
- cache와 fixture 조회
- refresh 중 기존 snapshot 유지
- stable ID/revision 기반 병합
- cancellation과 retry 가능 오류 전달

초기 로드와 refresh 실패를 구분한다.

| 전이 | visible data | 결과 |
| --- | --- | --- |
| `Idle → Loading → Success` | 새 snapshot | 정상 표시 |
| `Idle → Loading → InitialLoadError` | 없음 | empty/error와 retry |
| `Success → Refreshing → Success` | 기존 snapshot 유지 후 갱신 | 증분 반영 |
| `Success → Refreshing → RefreshError` | 마지막 성공 snapshot 유지 | stale/error badge와 retry |

중복 stable ID, 낮은 revision과 목적 범위를 벗어난 snapshot은 적용하지 않는다.

### 6.5 Unity GameModel·ProjectionModel

Unity 내부 model은 서버 domain Entity의 복사본이 아니다. Unity가 계산하고 표현하는 데 필요한 최소 immutable snapshot이다.

포함 가능한 값:

- stable ID와 revision
- 상태 code
- semantic location/zone/node ID
- source·unit·observedAt·evidence reference
- 허용된 interaction code

포함하지 않는 값:

- `GameObject`, `MonoBehaviour`, `Transform`, `Animator`, `Renderer`
- 서버 EF Entity와 범용 ledger dictionary
- access token·credential
- 허용되지 않은 주소·연락처·정밀 위치
- Synty Prefab 이름

서버가 `farmerX/Y/Z` 같은 Unity 좌표를 일반 업무 DTO로 보내는 방식은 피한다. 공동 World 좌표가 정말 canonical data라면 `CoordinateSystemCode`, `WorldId`, `PositionRevision`이 있는 별도 spatial contract로 정의한다. 초기에는 `FarmZoneKey`, `WaypointKey`, `RouteNodeKey` 같은 semantic location을 Unity layout에 매핑한다.

### 6.6 UseCase

UseCase는 사용자의 하나의 의도를 표현한다.

- 농장 snapshot 불러오기
- 시장가격 조회
- 센서 상태 조회
- 작업 preview
- 명시적으로 확인된 Command 제출
- 성공 뒤 canonical snapshot 재조회

단순 조회가 한 화면에만 있고 조합·검증이 없다면 얇은 UseCase로 시작할 수 있다. 다만 SceneController가 Repository와 정책을 직접 조합하기 시작하면 즉시 UseCase로 분리한다.

### 6.7 Presenter와 ScreenModel

Presenter는 domain/projection 결과를 View가 즉시 소비할 표현 계약으로 바꾼다.

```text
FarmSnapshot
  → FarmPresenter
  → FarmScreenModel
     ├─ title
     ├─ status labels
     ├─ visual state codes
     ├─ interaction availability
     ├─ evidence summary
     └─ loading/error/conflict state
```

Presenter는 Renderer, Material과 Animator를 직접 만지지 않는다. 지역화된 문구를 Presenter가 만들지 View가 lookup할지는 하나의 정책으로 통일하되 stable state code는 유지한다.

### 6.8 SceneController

Unity SceneController는 서버 Controller와 다른 presentation coordinator다.

- Unity lifecycle entry
- UseCase 실행과 취소 token 연결
- loading·refresh·error state를 Presenter/View에 전달
- 사용자 input event를 UseCase intent로 변환
- Scene 전체의 View binding과 해제

금지:

- `Update()`에서 서버 API 호출
- `async void` 예외를 방치
- response DTO를 View에 직접 전달
- animation event로 서버 성공 확정
- Scene reload 시 canonical state를 GameObject에서 복구

`Start()`는 예외 경계가 있는 명시적 초기화 method를 시작하고, object destroy·scene unload와 application exit cancellation을 전달한다.

#### 6.8.1 Zone Controller와 Role Experience Controller

하나의 공유 World에 여러 역할 관점을 겹칠 때 SceneController를 역할별 Scene으로 복제하지 않는다.

- Zone Controller는 장소 snapshot, stable-ID object 생성·갱신·제거와 공통 World View를 소유한다.
- Role Experience Controller는 서버가 승인한 `RolePerspective` 조회, 역할 전환과 Role View socket 적용을 조율한다.
- 두 Controller는 GameObject 참조가 아니라 stable ID로 결합한다.
- 역할 전환은 World View를 유지하고 Role View와 Detail View만 clear·replace한다.

Presentation assembly의 Role Experience Controller는 `역할관점조회UseCase`와 `RolePerspectiveApplicator`를 주입받고, Zone View가 제공하는 `IRolePerspectiveTarget` 목록과 `IRoleInteractionSink`에 결과를 적용한다. Controller가 역할별 권한표, 주소 masking이나 상차 가능 여부를 자체 계산해서는 안 된다. 서버 응답의 `AllowedInteractions`에 없는 action은 표시하거나 실행하지 않으며, 운영 Command는 실행 endpoint에서 다시 권한과 revision을 검증한다.

### 6.9 View

View는 결정된 ScreenModel 또는 PresentationCommand를 Unity component로 표현한다.

허용:

- Animator parameter 적용
- Renderer·Material variant 선택
- NavMeshAgent 목적지 설정
- Transform·Collider·UI·VFX·Audio 제어
- 접근성 상태와 focus 적용

금지:

- 서버 호출
- 가격·생육·권한 규칙 계산
- 원장 저장
- raw DTO 보관
- source provenance 삭제

## 7. Prefab과 Inspector socket 계약

Prefab은 서버 데이터 저장소가 아니라 **배선된 표현 template**이다. Inspector는 실제 서버 값을 입력하는 곳이 아니라 Unity resource와 component reference를 연결하는 도구다.

```text
SsalddelFarmerView
  ├─ Animator socket
  ├─ movement adapter socket
  ├─ body Renderer socket
  ├─ right-hand attachment socket
  ├─ interaction anchor socket
  └─ VisualRoot
       └─ Synty or placeholder visual
```

View socket은 기능 요구를 정의하고 vendor asset 이름을 요구하지 않는다.

각 View는 다음 metadata를 제공하는 방향으로 확장한다.

```text
ViewContractId
ContractVersion
RequiredSockets[]
OptionalSockets[]
AllowedComponentTypes[]
AnimatorParameterContract[]
ValidationSeverity
```

필수 socket은 `OnValidate` 또는 Editor validator에서 검사한다. runtime `Awake`에서도 fail-safe 검사를 할 수 있지만, production 실행 시 매 프레임 reflection scan을 하지 않는다.

## 8. 이동과 animation

### NPC

```text
semantic route/waypoint
  → Unity route mapper
  → NavMeshAgent destination
  → actual movement
  → Animator speed and action state
```

NavMeshAgent는 client 표현 이동을 담당한다. 실제 배차·경로·도착 완료는 서버 상태와 분리한다.

NPC의 서버·Unity 경계는 다음과 같다.

```text
canonical task or explicit simulation fixture
  → NpcMovementApiModel
  → NpcMovementMapper
  → NpcMovementSnapshot
  → ZoneNpcMovementController
  → stable-ID NpcMovementView
  → semantic waypoint Transform
  → NavMeshAgent + Animator
```

서버는 `Vector3` 대신 `RouteCode`, `CurrentWaypointKey`, `DestinationWaypointKey`와 `ArrivalActionCode`를 제공한다. Zone layout이 waypoint key를 실제 Transform에 연결한다. 운영 snapshot에는 `CanonicalTaskStableId`가 필수이며 simulation snapshot은 canonical task를 가질 수 없다.

| Zone | 초기 NPC 역할 | 대표 semantic route |
| --- | --- | --- |
| 농장 | 생산자 | 입구 → 밭 → sensor → 선별·출고 준비 |
| 마트 | 주문자, 재고 담당 | 입구 → 진열대 → 주문대 / stockroom → 진열대 → loading door |
| 주거공동체 | 주문자, 분배 담당 | 세대·게시판 → 공동수령지 / loading point → 수령지 → 관리실 |
| 전통시장 | 상인, 운송자 | 점포 → 저장공간 → 상차지 / 입구 → 상차지 → 출구 |
| 도심 물류센터 | Dock 작업자, 운송자 | 입고 Dock → 분류 → 출고 Dock / 차량 gate → loading bay → 출구 |
| 창고 | picker | 작업대 → rack → 포장 Zone → 출고 Dock |
| 커뮤니티·공공데이터·협동 공간 | 구성원, 안내자, 진행자 | 입구 → 핵심 board·kiosk·table |
| 개인 공간 | 없음 | 자동 NPC를 기본 배치하지 않음 |

`NpcMovementView.Update()`는 이동 속도와 도착 animation만 처리한다. 도착 event 또는 Animator event에서 서버 Command를 호출하지 않는다. 운영 상태 전이는 별도 interaction Controller가 명시적 확인을 받고 서버 성공 뒤 canonical snapshot을 다시 조회한다.

공동주택 같이 주문의 RG4-NPC에서는 기존 표의 주거공동체를 `ResidentialGroupRepresentative` actor까지 확장한다. 사회적 표시명은 주민자치 대표·입주자대표회의 대표·관리사무소 조정자일 수 있지만 canonical 권한은 기존 `GroupPurchaseRepresentative` 역할 검증에서만 나온다. 주거공동체 `community office → community board → departure point`와 도심마트 `entrance → manager desk → exit`를 별도 route leg로 두고 상위 representative visit state로 연결한다. 이 항목은 설계 확정이며 현재 `ZoneNpcRouteCatalog`, View, Scene·NavMesh·Animator 구현 완료를 뜻하지 않는다. 상세 기준은 [도심마트 공동주택 주문자 집단 통합 설계](UrbanMarketResidentialOrdererGroupIntegrationDesign.md)를 따른다.

### Player

```text
Unity Input System
  → input adapter
  → player movement controller
  → CharacterController or approved physics body
  → Animator
```

NPC와 Player는 이동 구현이 달라도 공통 presentation state와 Animator contract를 사용할 수 있다.

## 9. DI와 비동기 기술 결정

### 9.1 VContainer

VContainer 1.18.0을 Unity presentation composition root로 채택한다.

- pure C# service는 constructor injection
- Scene별 `LifetimeScope`
- MonoBehaviour는 최소 method injection 또는 registered component binding
- View의 Unity resource reference는 `[SerializeField]` 유지
- core assembly에는 VContainer attribute/reference를 넣지 않음

`도심마트LifetimeScope`와 `전통시장물류거점LifetimeScope`가 현재 Zone 단위 조립의 기준 구현이다. Controller는 concrete UseCase를 `new`하거나 simulation·operational을 선택하지 않고 `[Inject]` method로 주입받는다. 실제 Unity project의 `Packages/manifest.json`에 공식 Git dependency를 고정하며, 재사용 package의 `package.json`에 Git 의존성을 선언하지 않는다.

Application 공통 Scope와 nested Zone Scope는 인증·session·API Client가 실제로 공유될 때 추가한다. 현재는 필요한 Zone Scope만 두어 수명 구조를 선행 일반화하지 않는다.

### 9.2 Task, Unity Awaitable과 UniTask

현재 core는 .NET `Task`와 `CancellationToken`을 사용한다. 이를 유지한다.

Unity 6에는 Unity lifecycle에 맞춘 pooled `Awaitable`이 있고, UniTask는 `WhenAll`, PlayerLoop timing과 Unity integration을 제공한다. 따라서 UniTask를 즉시 전 계층 표준으로 정하지 않고 다음 기준으로 ADR을 작성한다.

| 위치 | 기본 방향 |
| --- | --- |
| engine-independent core | `Task`/`ValueTask`와 `CancellationToken` |
| Unity native async adapter | Unity `Awaitable` 검토 |
| 복합 비동기·PlayerLoop·성능 요구 | UniTask 검토 |
| public interface | 구현 package를 불필요하게 노출하지 않는 return type 우선 |

어떤 방식을 선택해도 object destroy, scene unload와 application exit cancellation을 연결하고 unobserved exception 정책을 검증한다.

### 9.3 JSON

현재 Unity project package manifest가 없으므로 Newtonsoft Json 채택은 미확정이다. 선택 전 다음을 contract fixture로 비교한다.

- `System.Text.Json` 사용 가능 범위
- Unity용 Newtonsoft package 호환성
- IL2CPP/AOT와 code stripping
- `DateOnly`, nullable, decimal과 unknown field 처리
- server JSON과 exact casing

## 10. 첫 농장 Vertical Slice

문서 예시의 `GET /api/farms/{id}`는 현재 구현 route로 확인되지 않았으므로 개념 예시로만 취급한다. 실제 구현은 기존 route와 public projection을 조사한 뒤 확정한다.

첫 slice는 서버 전체 농장 aggregate를 한 번에 복제하지 않는다.

```text
1. 지역 농수산 marker projection
2. KAMIS 감자 가격 observation
3. 농사로 감자 작목·농작업 일정 projection
4. versioned weather fixture 또는 승인된 관측
5. 대표 soil profile
6. SIMULATED soil-moisture sensor
7. Farm screen model
8. primitive Farm View
9. sensor external view and emanation view
10. evidence-card Web handoff
```

Prefab 구조:

```text
FarmRoot
  ├─ FarmView
  ├─ FarmStatusPanel
  ├─ FarmerView
  │   └─ VisualRoot
  ├─ WarehouseView
  │   └─ VisualRoot
  ├─ CropPlotView[]
  │   └─ CropVisualRoot
  └─ SensorView[]
      └─ SensorVisualRoot
```

첫 단계에는 placeholder primitive를 사용한다. Synty 적용 후에도 View contract, UseCase와 data tests가 변하지 않아야 한다.

## 11. Editor·CLI·AI 자동화

자동화의 권위는 View contract와 deterministic Editor Script에 있다. AI가 Unity asset을 직접 임의 수정하는 구조로 시작하지 않는다.

```text
asset inventory
  → candidate analysis
  → dry-run wiring plan
  → deterministic Editor Script
  → isolated Prefab generation
  → socket validation
  → EditMode tests
  → PlayMode smoke
  → human visual review
  → approved Prefab promotion
```

Editor Script가 수행할 수 있는 작업:

- 허용된 vendor folder에서 Prefab 검색
- component·bone·socket 후보 조사
- Ssalddel wrapper GameObject 생성
- required component 추가
- `SerializedObject`를 이용한 reference 연결
- Prefab 저장과 validation report 생성

안전 기준:

- 원본 vendor asset을 수정하지 않음
- asset GUID와 generated Prefab path를 manifest에 기록
- ambiguous socket은 자동 선택하지 않고 실패
- dry-run report 없이 대량 쓰기 금지
- 생성 범위를 전용 output folder로 제한
- 실행 전·후 version control diff 확인
- asset license와 raw file 공유 범위 준수
- AI 입력으로 asset 원본을 외부 provider에 업로드하지 않음

Unity CLI는 `-batchmode`, `-projectPath`, `-executeMethod`, `-quit`을 이용할 수 있지만, 실제 Editor version과 license 환경을 고정하고 exit code·Editor log·test result를 함께 보존한다.

## 12. 검증 전략

| 계층 | 검증 |
| --- | --- |
| ApiModel·Mapper | JSON contract fixture, schema, unknown field, unit, provenance |
| Repository | initial/refresh failure, last-success retention, cancellation, retry classification |
| UseCase | 권한 입력, expected revision, preview/confirm/re-query 순서 |
| Presenter | 모든 state의 ScreenModel과 accessibility label |
| View socket | missing·ambiguous reference, Animator parameter와 vendor-independent binding |
| EditMode | Prefab contract, serializer, mapper, Editor automation dry-run |
| PlayMode | Scene load, input, marker reconcile, View state, destroy cancellation |
| built player | Windows·Android HTTP, IL2CPP stripping, performance와 실제 렌더 |
| external runtime | 실제 API source·count·URL·visible state |

최소 화면 상태:

```text
Idle
Loading
Success
InitialLoadError
Refreshing
RefreshError
Cached
Fixture
Stale
Invalid
NoAccess
Conflict
```

build/test, Unity Editor, built player, 실제 API, visual capture, commit, push와 deploy는 각각 별도 증거다.

## 13. 보완된 구현 순서

1. Unity Editor version, render pipeline, platform, project 위치 ADR
2. 공통 data envelope와 evidence/rule reference 확정
3. 지역 농수산 marker·KAMIS·농사로를 Unity ApiModel/Mapper로 이관
4. 실제 P2 Unity runtime source 위치와 assembly dependency 확인
5. API Client·Repository port/adapter와 load-state contract 통합
6. 첫 Farm UseCase·Presenter·ScreenModel 작성
7. primitive SceneController·View·Prefab socket 작성
8. EditMode·PlayMode와 built player smoke 구축
9. DI·async·JSON 후보를 측정 후 ADR로 선택
10. sensor와 evidence-card handoff 연결
11. 무료 또는 단일 asset pack으로 wrapper compatibility 검증
12. 필요한 Synty asset 구매 범위 확정
13. Editor automation dry-run과 deterministic Prefab generator 작성
14. 사람의 최종 visual·accessibility·performance 검토

## 14. 변경된 제안 사항 요약

원안에서 그대로 채택한 내용:

- server authority와 Unity presentation 분리
- API Client→Repository→UseCase→Scene/View 계층
- MonoBehaviour의 business logic 최소화
- View socket과 Prefab·Inspector 배선
- Synty를 교체 가능한 presentation resource로 취급
- CLI·Editor Script를 이용한 반복 조립 자동화

보완한 내용:

- DTO mapping을 Repository 내부 구현 세부로 숨기지 않고 독립 Mapper로 유지
- server domain과 구분하기 위해 Unity model을 GameModel/ProjectionModel로 명명
- `Task` 기반 core와 Unity Awaitable/UniTask adapter를 분리
- `async void Start` 대신 cancellation·exception boundary가 있는 초기화 사용
- server가 Unity `Vector3`를 일반 DTO로 전달하지 않고 semantic location을 사용
- stable ID, schema, revision, provenance, unit, freshness와 limitation을 필수화
- initial load와 refresh failure 정책 분리
- 운영 Command 뒤 canonical snapshot 재조회 의무화
- AI 자동 배선을 dry-run·deterministic Editor Script·검증·사람 승인 흐름으로 제한

## 15. 참고 자료

- [Unity Awaitable 비동기 프로그래밍](https://docs.unity3d.com/kr/6000.0/Manual/async-await-support.html)
- [Unity Input System](https://docs.unity3d.com/ja/current/Manual/com.unity.inputsystem.html)
- [Unity Test Framework](https://docs.unity3d.com/kr/current/Manual/com.unity.test-framework.html)
- [Unity command-line arguments](https://docs.unity3d.com/es/current/Manual/CommandLineArguments.html)
- [VContainer repository and documentation](https://github.com/hadashiA/VContainer)
- [UniTask repository and documentation](https://github.com/Cysharp/UniTask)

외부 package의 구체 version은 이 문서에 고정하지 않는다. Unity Editor version과 compatibility를 확정하는 ADR에서 lock file과 함께 기록한다.
