# NPC 앱·업무 서버 관찰 — 구현 결과 r2

[승인 범위](observer-phone.r2.md) · [수직 작업 명세](observer-api-work.r2.md) · [기존 로컬 휴대폰](observer-phone-result.r1.md)

상태: 격리 음식 배달 구현·5분 제한 관찰·재연결 검증 완료 (2026-09-07). 승인 문서의 본문/hash는 동결하고 현재 상태는 이 결과와 PLANNING이 소유한다. 기존 E/Goal 승격과 실제 영업 준비를 의미하지 않는다.

## 구현 구조

```text
관찰자: NPC 선택 / 휴대폰 / 명시적 5분 검증 시작
  → Unity HTTP Adapter → 검증 전용 조회 결과
                           ↑
격리 서버 NPC 실행기 → 정상 로그인·기존 업무 Controller API
                           → 기존 Command·배차 서비스 → 전용 MySQL / MongoDB
                           → 주문자·음식점 API 재조회 + 독립 DB 재조회
```

- 서버가 업무 사실을 소유한다. Unity 프로필6은 해당 주문에 LocalSimulationRuntime을 설치하지 않고 읽은 상태만 도형·휴대폰에 표현한다. 역할 선택은 관찰 대상 변경이지 로그인 권한 전환이 아니다.
- 주문자1·음식점1·기사2, 합성 주문1건이다. 기사 위치는 정상 위치 API로 기록한다. 이동 경로·좌표는 합성 표본이며 실제 면목동 지도·운영 주소가 아니다.
- Figma `0KhuQLc1MleUBIQnARC21Z`, `2269:177`을 실제 조회해 같은 주문번호·역할 카드·상태·대기 이유 구성을 재사용했다. 전체 MAUI 앱이나 픽셀 클릭 봇을 만들지 않았다.
- 실행 검증 기본값은300초다. 기존 로컬 30분 세계 수명·근무/휴식·저장 규칙은 보존한다. 빌드/서버 준비 시간은300초에 포함하지 않는다.

| 책임 | 먼저 볼 코드 |
| --- | --- |
| 검증 환경·API 등록 | Hongdal `Ssalddel/Services/Development/FoodObserver/음식배달관찰검증Options.cs`, `음식배달관찰검증Hosting.cs` |
| NPC 입력·같은 주문 재조회·300초 제한 | 같은 폴더 `음식배달관찰검증Runner.cs` |
| 실제 주문·수락·조리·수령 | 기존 `Ssalddel/Application/Food/Handlers/`, `Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs` |
| 실제 추천·기사 수행 | 기존 `Ssalddel/Services/Dispatch/`, `Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs` |
| 서버 결과를 Unity 자료로 변환 | Unity `Assets/Ssalddel/Infrastructure/Simulation/업무배달관찰ApiAdapter.cs`, `업무배달관찰ApiMapper.cs` |
| 조회 판본·연결·선택·화면 | Unity `Assets/Ssalddel/Runtime/World/업무배달관찰Model.cs`, `Bootstrap/업무배달관찰Binding.cs`, `Presentation/World/업무배달관찰휴대폰View.cs` |

첫 실행에서 운행 시작 요청의 `일반` 값이 기존 Validator의 허용 목록과 달라 HTTP400으로 중단됐다. `바로시작`으로 수정하고 실제 `운행시작CommandValidator`를 통과하는 회귀 시험을 추가했다. 정상 API의 검증을 완화하거나 우회하지 않았다.

두 번째 실행은 주문·수락·배차대기까지 진행했지만 실제 큐가 `계획배차(10)`인데 `추천대기처리Async`를 호출해 전환되지 않았다. 현재 단계에 따라 기존 `계획배차에서추천으로전환Async` 또는 `추천대기처리Async`를 호출하고 반환 결과를 검사하도록 수정했다. 임의로 추천 기사 ID를 DB에 쓰지 않는다.

## 실행 방법

저장소 루트에서 `pwsh -NoProfile -File eng/verification/food-observer.ps1 -Action Prepare`, `Build`, `Up` 순서로 준비한다. 생성된 `artifacts/local/verification/food-observer/connection.json`은 로컬 비밀이며 공유·Git 추가하지 않는다.

Unity의 `음식 배달 · 업무 서버 · 5분 검증` 프로필에서 canonical `SimulationWorldShell`을 Play하고 오른쪽 휴대폰을 연다. 연결 준비를 확인한 후 시작 버튼을 한 번 누른다. 같은 실행 중 재시작하거나 실패를 자동 성공 처리하지 않는다. `-Action Status`는 현재 상태, `Result`는 비밀 없는 결과 저장, `Stop`은 이 검증 스택만 중지하며 볼륨을 보존한다.

완료/실패 후 독립된 새 표본이 필요하면 `-Action NewSample`을 명시한다. 이전 결과를 로컬에 기록하고 이전 DB·앱 볼륨을 삭제하지 않은 채 새 이름의 볼륨을 선택한다. 이어 `Up`과 휴대폰의 새 시작 대기 상태 확인·별도 시작 입력을 수행한다. 진행 중에는 NewSample을 거부한다. 이전 실패 주문을 지우거나 새 주문으로 덮지 않는다.

전용 Compose 프로젝트는 `ssalddel-food-observer`, 전용 DB는 `ssalddel_food_observer`, 접속은 `127.0.0.1:5215`뿐이다. 서버와 DB는 내부 네트워크에 두고 로컬 조회용 gateway만 연결한다. Development·Simulation·전용 DB/키 검사를 모두 통과해야 검증 API가 등록된다. 기존 운영 컨테이너·DB·비밀은 사용하지 않는다. 결제·외부 알림·공공데이터 호출 및 운영 배차 스케줄러는 가동하지 않는다.

## 함께 보완한 문제

- 음식점 준비 완료가 `조리중`만 허용해 선행 `기사배정`과 충돌했다. 준비 완료를 허용하되 기사배정 상태와 배차 결과를 보존하도록 기존 정책을 수정했다.
- Dockerfile의 Interior Contracts/Domain 프로젝트 참조 복사 누락을 보완했다.
- 새 MySQL 모델 생성에서 HR 두 복합 인덱스가3072바이트 상한을 넘었다. 기존 migration·호환 초기화기의 열 구성에 현재 모델을 맞추고 전체 문자열 인덱스 크기 회귀 시험을 추가했다. 운영 DB migration을 실행한 것은 아니다.
- 실패한 첫 DB 생성은 자동 정상으로 간주하지 않는다. 현재 모델 인덱스 존재를 검사한다. 주문 생성 전 실패한 이번 전용 MySQL 볼륨만 초기화했고 기존 데이터는 삭제하지 않았다.

## 검증

| 범위 | 결과 / 근거 |
| --- | --- |
| 집중 서버 시험 | 최종34/34 통과(운행 입력·큐 단계·스키마·저장·기존 업무 회귀), Outbox/배차수락/배달권/인계 알림 추가10/10 통과. `artifacts/local/validation/food-observer/food-observer-final.trx`, `food-observer-handoff-regression.trx` |
| Fast | 최종 `20260907-153208`: 직접 영향 build·집중37/37·diff 검사 통과 |
| Task | `20260907-145737`: v3.5 솔루션 build 통과, 전체 시험4873/4880 통과·7실패 |
| Docker | 최종 이미지 build·전용 DB 준비·Idle 조회 확인. 운영 migration 증거 아님 |
| Unity 컴파일·EditMode | 최종23/23, 기존 휴대폰 회귀8/8 통과. `food-observer-phone/unity-editmode-final-23.json`, `unity-regression-8.json`. 실제 화면·연속 실행과 구분 |
| 추가 공용 Unity 모드 회귀 | 3/5 통과·2실패. `food-observer-phone/unity-common-mode-regression-5.json`. 새 기능 시험 성공과 구분 |
| 실제 업무 HTTP·DB | 세 번째 실행에서 수령확인/배달완료. 새 DB 주문1·운송투영1·위치28건, 근거리 기사 확정. MySQL Outbox8건 Succeeded, Mongo 음식 원장/운송 원장 완료 및 투영 판본8/4 일치. `food-observer/database-proof.json` |
| 실제 UI·5분 | 15:27:27.195 KST 시작 클릭, 서버299.4018438초 Completed, 15:32:56.694 KST 완료 화면 관찰. 정상 이벤트13·Revision341. UI는 실제 경과시간을 반영해04:59/05:00으로 표시한다. 원본 `food-observer-phone/third-actual-five-minute-run.json` |
| 재연결 | app 단독 중지→연결 실패/오래된 자료 표시→같은 app 시작. 같은 run·주문·선택·이벤트13·299.4018438초 보존, Revision341→343·DB 재조회 완료. 새 주문/자동 NPC 재개 없음. `food-observer-phone/app-restart-recovery.json` |
| 기존 로컬 저장 | 원본이 아닌 복사 슬롯에서1회. Tick694/Revision1354·재생 해시 불변, Load3938.5ms / Save3726.1ms. 성능 문제는 미해결 |

전체 시험 실패7개는 이번 쓰기 경로 밖의 `/community/map-application-chooser` capability, 공식재료 CSS `min-height:44px`, 역할 Controller 업무 영역3개, `Hs식품국가가격Card조회` 명명, Controller 도입 이력 검사다. 다른 작업의 코드를 함께 고치지 않았다. 전체 통과나 baseline 회귀 없음으로 주장하지 않는다.

공용 Unity 실패: `모드전환은_WorldTick과Revision을변경하지않는다`(ThirdPerson 기대/FirstPerson 실제), `저장Scene은_Farm플레이와Hub정보판을하나의진입점에가진다`(버튼3 기대/4 실제). 읽기 진단에서 새 업무 서버 잠금은false, Farm focus는 존재하지만 카메라 검사는 `FarmFocusTrackedBoundsUnavailable`이었다. 이는 함께 존재하던 Farm 조기 반환 경로와 일치한다. 같은 시험 파일의 다른 항목은 버튼4개를 기대한다. 이번 잠금은 profile6 Binding에서만 켜며 기존 Farm dirty 코드를 변경하지 않았다. 변경 전 별도 checkout 비교는 하지 않았으므로 baseline 회귀 없음의 증거로 쓰지 않는다.

실제1920×1080·1280×720에서 휴대폰 열기/닫기·NPC/역할 카드 선택·재조회·새 서버 결과 확인 후 별도 시작을 입력했다. 관찰 중 LocalRuntimeInstalled=false와 Console 오류0을 확인했다. 이동은 서버 위치와 화면 결과(음식점→주문자)를 관찰했으며 전 구간 연속 영상 증거는 아니다. [실제 화면](../../../../Changes/2026-09-07-food-observer-server.md)을 별도 보존했다.

첫 실패는1.148초/운행 시작 HTTP400, 두 번째는4.315초/계획 배차 단계 불일치였다. 첫 실패·두 번째 실패·성공 실행을 합산해5분이라고 하지 않는다. 두 번째 주문 `FOOD-20260907061707201`과 이전 DB 볼륨은 보존됐으며, 성공은 새 독립 표본 `2036bf5595634633b94e6305e2ec1cbb`의 `FOOD-20260907062729102`만 검증했다.

시험 중 Editor Test Runner 고착으로 기존 clean 상태를 확인한 뒤 해당 Unity Editor만 재시작했다. 초기 FMOD 장치 초기화 오류2건은 최종 관찰의 Console 오류0과 분리한다. 기존 로컬 슬롯은 복사본에서만1회 저장해 원본을 변경하지 않았다.

최종 전용 Docker 서버는 완료 결과를 조회할 수 있게 유지하며 자동 NPC 실행은 없다. Unity는 Play 종료·canonical Scene clean, 기존 프로필5·saveRoot/restaurantObserver null·Free Aspect/1.1배·최대화 해제로 복원했다. 임시1280 해상도만 제거했다. 정확한 변경 파일과 최종 상태는 `artifacts/local/validation/food-observer-phone/final-unity-report.json`에 기록했다. 새 기능을 다시 보려면 위 실행법의 프로필6을 명시적으로 선택한다. 문서/링크 검사는 `20260907-153827`, 이후 최종 diff 검사도 통과했다.

## 남은 범위

기존 로컬 누적 저장 비용, 창고·마트의 서버 기반 NPC 확장, 운영 migration/장애 주입 Outbox 전체 재처리, 장시간 안정성, 실제 지도·우천·정산은 완료되지 않았다. 도착 위치에서 기사/주문자 이름표가 겹치며 Escape 키의 실제 닫힘은 미확인이다(닫기 버튼은 확인). 표본의 정상 업무 API 성공은 실제 영업 자격·외부 효과·운영 배포 증거가 아니다. Scene 저장·E 자동 승격·commit·push 없음.
