# 면목동 오행 업무 객체 대장 r1

- 기획 ID: `PLAN-SYSTEM-MYEONMOK-OBSERVER`
- 분야·판본: 월드·공간·배치 / `myeonmok-five-element-game-object-catalog.r1`
- 상태: `Approved / FoundationImplemented / MongoReadbackVerified / UnityExecutionDeferred`
- 승인 근거: 2026-09-09 사용자가 면목동에서 사용할 게임 객체를 먼저 정리하고 주문자·음식점·배차·배송·창고를 오행 업무 엔진의 맥락으로 분류한 계획의 구현을 요청했다.
- 표현 방향: [공공데이터 기반 아이소메트릭 3D 디오라마](public-data-isometric-diorama-direction.r1.md)
- 정본 객체 대장: `eng/world-seedbeds/object-catalogs/jungnang-myeonmok-game-objects.v1.json` / `jungnang-myeonmok-game-objects.r2`

## 확정

### 두 분류를 분리한다

객체의 **기본 역할 괘**는 그 객체가 세계에서 주로 맡는 업무 정체성이다. **행위 괘**는 특정 WI에서 지금 무엇을 하는지를 나타낸다. 행위 괘가 기본 역할 괘를 덮어쓰지 않는다.

| 업무 역할 | 기본 괘 | 오행 | 색상 토큰 | 오행 업무 모듈 |
| --- | --- | --- | --- | --- |
| 주문자·주문 | `JIN` 진괘 | 목 | `role.jin.wood` | `Order` |
| 음식점·조리 | `RI` 리괘 | 화 | `role.ri.fire` | `Restaurant` |
| 배차 조정 | `GAN` 간괘 | 토 | `role.gan.earth` | `Dispatch` |
| 배달·운송 | `GAM` 감괘 | 수 | `role.gam.water` | `Delivery` |
| 창고·적재 | `TAE` 태괘 | 금 | `role.tae.metal` | `Warehouse` |

예를 들어 음식점 운영자의 기본 역할은 `RI`지만, 현행 `WI-CITY-RESTAURANT-ACCEPT`와 `WI-CITY-RESTAURANT-COOK`의 정보 확인·물류 판단 행위는 분류 대장 r10에 따라 `GAM`이다. 창고 작업자의 기본 역할은 `TAE`지만 `WI-001` 검수 행위는 `GAM`, `WI-002` 적재 이동 행위는 `JIN`이다. 이 구분 덕분에 색상은 역할을 안정적으로 보여주면서도 오행 업무 엔진은 WI별 실제 행위 의미를 판정할 수 있다.

### 객체 원형과 공간 후보

정본 대장은 26개 객체 원형을 정의한다.

- 중립 현실층: 면목동 건물, 도로망
- 주문: 주문자, 음식 주문 원장 참조, 마법 주문 통신구, 수령 표식
- 음식점: 음식점 업무 장소, 운영자, 조리대, 픽업 인계대
- 배차: 배차 조정자, 배차 대기열 조회, 기사 대기 구역
- 배송: 음식 배달 기사, 이동 수단, 음식 꾸러미, 전달 지점, 화물 차량
- 창고: 창고 업무 장소, 작업자, 검수대, 적재대, 피킹 카트, 포장대, 팔레트, 인계 표식

기존 Seedbed 객체와 의미가 같은 경우에만 `seedbedObjectRef`를 재사용한다. 객체 원형은 Unity Prefab이나 운영 원장이 아니며 `O0/O1` 후보 상태다.

사가정 Geometry의 건물 602개는 각각 원본 `sourceElementRef`와 500m `tileRef`만 가진 `BackdropOnly` 후보로 만들었다. 도로 2,397개는 원본 선분 ID를 보존한 하나의 중립 도로망 후보로 묶었다. 둘 다 업무 장소·출입구·통행 권위가 없다. 음식점·주택·창고 역할은 이후 사람이 고른 건물 후보 위에 `ScenarioOverlayOnly`로 결속한다.

### WI와 오행 업무 엔진 연결

11개 WI Profile과 25개 역할·행위 결속을 만들었다. 각 Profile은 현행 `mirror-world-interaction-gwae-classifications.r10`의 `action / operation / target / support` 분류를 그대로 참조한다.

상태 대응이 확정된 주문 확정, 음식점 수락·조리, 배차, 픽업·전달·수령, 창고 검수·적재는 기존 `FiveElementWorkflow` 엔진에 전달할 수 있다. 객체 Resolver는 다음 순서만 담당한다.

```text
WI + 주체/대상/시설의 안정 ID와 권한
    ↓ 객체 원형·역할 결속 검증
기본 역할 괘와 WI 행위 괘 분리
    ↓ 기존 오행 업무 모듈에 상태 전이 판정 위임
비권위 허용/차단 결과
    ↓
기존 UseCase·Domain·DB/Event가 실제 변경 여부를 결정
```

이동과 대기점 복귀 WI는 기존 음식 배달 상태 코드에 직접 대응하지 않으므로 `PendingStateMapping`으로 보류했다. Resolver나 객체 대장이 빈 상태를 추정하지 않는다.

## 구현·검증 결과

- `spatial-catalog.r2`가 `ObjectCatalog`와 `ObjectCandidateProjection` 문서를 읽고 객체 원형·WI Profile·역할 결속·배치 후보를 별도 요소로 보존한다.
- 어댑터 판본이 바뀌면 같은 원본 경로·판본이라도 새 불변 문서 ID가 되도록 문서 ID에 `adapterVersion`을 결속했다. 이전 기록을 덮어쓰지 않는다.
- 로컬 Docker `hongdal-mongo-1 / ssalddel_dev`의 현행 묶음 `bundle:4387BA718A423162819082FD07C8E828E105B6A3D4216C64C589FC8AEB78CC66`에 28문서·5,791요소·7,945명시 관계를 반입하고 독립 재조회했다. 같은 입력 재반입은 신규 기록 0개였다.
- 객체 관련 수치는 모듈 결속 5, 객체 원형 26, WI Profile 11, 역할·행위 결속 25, 건물 후보 602, 도로망 후보 1이다.
- Unity 인계용 비식별 사본은 `artifacts/local/spatial-catalog/20260909-r4/myeonmok-game-object-unity-handoff.r2.json`에 생성한다. 로컬 검증 산출물이며 배포 자원이나 Scene 권위가 아니다.
- 이전 r3 묶음은 반입 뒤 원천 manifest 안정 ID 오기를 발견해 현행에서 제외했다. 저장 자료를 삭제·수정하지 않고 객체 대장 판본을 r2로 올려 실제 `map-sources:jungnang-myeonmok.v2`와 판본까지 대조한 새 묶음으로 대체했다.
- 관련 .NET 시험 46/46과 객체 대장 검사 1/1, 전체 importer build를 통과했다. 실제 Unity Editor·Play Mode·Game View는 실행하지 않았다.

## 미정·후속 관문

- 602개 건물 중 음식점·주택·창고로 쓸 대표 건물은 아직 선택하지 않았다.
- 출입구, 보행 가능 구역, 도로 통행 가능성, Collider는 근거가 없어 만들지 않았다.
- Unity 결속은 기존 `SimulationWorldShell`, 카메라, 타일 스트리밍을 재사용해야 하며 새 Scene이나 Map Manager를 만들지 않는다.
- Unity에 붙일 때도 기본 지리층은 중립 저채도이고, 오행 색상은 검증된 업무 시나리오 오버레이에만 사용한다.

다음 질문 하나: 없음. 다음 Unity 구현을 승인할 때 사가정 1km 안에서 음식점·주문자 목적지·기사 대기점·창고로 검토할 대표 건물 후보를 먼저 고른다.
