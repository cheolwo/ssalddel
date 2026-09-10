# 운영 서버 → Simulation → Unity 업무 출처 메타데이터

## 결과와 범위

서버 업무를 기준으로 Simulation의 의미 재구성·실제 공유 계산·Unity 상태 사본 소비를 구분하는 선택적 메타데이터를 구현했다. 실행 코드 본문, 주문/배차 규칙, 운영 API와 DB, Unity Scene은 변경하지 않았다. 현재 소스의 대응을 확인한 것이며 과거 작성 계보나 배포/접속 성공을 자동 증명하지 않는다.

기준 설명은 [기존 코드 메타데이터 문서](../Architecture/SsalddelCodeMetadata.md#운영-업무에서-simulationunity로-이어지는-코드-출처)에 모았다. 별도 원장·서버·공개 API를 만들지 않았다.

| 대상 | 실제 연결 | 재사용하지 않는 것 |
| --- | --- | --- |
| Simulation 음식 주문 | 운영 등록/수령 확인 Handler와 업무 의미 대응, 공통 상태 Catalog 소비 (`SemanticAdaptation`) | 운영 메뉴 조회·개인정보·DB/Event·Handler 직접 실행 |
| 음식점 응답·조리 | 운영 수락/진행 Handler·Policy의 상태 의미 대응 (`SemanticAdaptation`) | 운영 계정·메뉴·가격·Event |
| 가상 음식 배차 | 후보 선정 Policy를 경유하여 운영과 같은 픽업 평가/정렬 사용 (`SharedRuleCall`) | 운영 기사 Store·실시간 경로·알림·배차 확정 전체 |
| 화물 배차 | 운영 추천 점수·기사 대기 계산과 공통 후보 판정 사용 (`SharedRuleCall`) | 운영 기사 조회·알림·배차 원장 |
| 화물 운송 | 운영 상차·하차·인수 상태 의미와 공통 전이 대응 (`SemanticAdaptation`) | 운영 UTC·GPS·문제 신고·정산 |
| 창고 적치 | 운영 검수 후 적치 의미 대응 (`SemanticAdaptation`) | 운영 CRUD·권한·입고/재고 원장; 공통 Catalog 직접 호출 없음 |
| 창고 출고 | 운영과 같은 출고 배분 계산 사용 (`SharedRuleCall`) | 운영 DB 조회·서비스 권역·출고 원장 |
| Unity 음식배달관찰상태 | Simulation 주문 ID/Revision/상태 사본을 표시값으로 변환 (`ProjectionConsumption`) | MonoBehaviour 생성·실제 이동·주문/배차 확정 |

## 변경 위치

- `Ssalddel.CodeMetadata/SsalddelCodeMetadataAttribute.cs`: 기존 attribute/descriptor/reader에 `SourceCodeRefs`, `ReuseKind`, `SharedRuleRefs`, `Adaptation` 추가. 기존 생성자와 기본값 호환 유지.
- `Ssalddel.CodeMetadata/SsalddelCodeMetadataGraph.cs`: 출처 필수 항목과 공유 규칙 누락 검사.
- `eng/Ssalddel.CodeMap/Program.cs`: 상대 C# 참조 존재 검사, partial 타입의 StepKey별 파일 탐색, 기존 JSON/Markdown/단일 기능 조회 출력.
- `eng/work-areas/simulation-unity.json`: `food-workflow-lineage` 아홉 단계 등록. 출처를 runtime 의존성으로 만들지 않음.
- 주문·음식점·음식 배차·화물 배차/운송·창고 적치/출고·Unity 상태 표시 파일에 특성을 추가했다. 실행 본문은 변경하지 않았다.
- `Ssalddel.Simulation.Tests/운영업무출처MetadataTests.cs`: 8사례. 기본 호환·정규화/JSON·잘못된 종류/공유 규칙 누락·아홉 단계 참조/권위 경계·양쪽 실제 규칙 호출·동일 입력 평가 비교.
- `Ssalddel.Tests/Application/WorkflowRules/업무흐름규칙ParityTests.cs`: 13사례. 운영 계약·정책과 공통 상태/전이 및 Simulation 제외 운영 효과 비교.
- 기존 코드 지도와 E 책임 지도 JSON/Markdown은 생성기로 갱신. 코드 지도에는 이전 미반영 동네 import/route/movement 3단계도 함께 반영되었으며 이번 구현 성과로 세지 않는다. E 지도 역시 기존 다른 변경을 포함하는 현행 스냅샷이다.

## 확인한 원천 판본

2026-09-09 로컬 SHA256 관측이며 배포 판본이나 immutable Git commit은 아니다.

| 원천 파일 | SHA256 |
| --- | --- |
| `Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs` | `62602D4C9A34E3C13200421F4DDD1042446A8CE8743F29B16E9B8F5686A011B4` |
| `Ssalddel/Application/Food/Handlers/주문자음식주문수령확인CommandHandler.cs` | `5DE63600BA38A25382588CAF5E2E6E7E148C5D5E8A187CF374F48B058D7D8474` |
| `Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs` | `4BB38137D1B26C723628ADE73CC35A2CBF4F5559E1336277A673E1E52C8031AC` |
| `Ssalddel.WorkflowRules/UnityPackage/Runtime/음식배달픽업평가Policy.cs` | `A631DB23BDEFAA0B7293AE721A07BB2397ECE0DA15A7E0E3FDE7CF43AFFF764B` |

## 검증

- 최초 관련 26/26 통과 후 최종 범위를 확대했다. 최종 독립 회귀 **50/50**: Simulation 29, Unity 표현 라이브러리 16, 서버 메타데이터 5. 최초 26개와 중복 합산하지 않는다.
- TRX: `artifacts/local/validation/workflow-lineage-20260909/{simulation-final,unity-final,server-final}.trx`. 서버 빌드 과정에서 이번 변경 밖 nullable 2건과 기존 xUnit 경고 1건이 관측되었다. 컴파일 실패는 없었다.
- `dotnet run --project eng/Ssalddel.CodeMap -- --feature food-workflow-lineage`: 세 단계/원천/공통 규칙/변형 경계 실제 출력 확인. 코드 지도 check 통과, 탐색 미표기 경고 6개는 유지.
- Fast `20260909-080323`: 코드 지도 통과 후 E 지도 stale에서 중단. 자기 표시 클래스의 E3 책임 표기와 생성물 재동기화 뒤 Task `20260909-080431`은 코드 지도 통과, EVIDENCE001 기존 세 대상에서 중단했다. 검사 완화/기존 대상 수리는 하지 않았다.
- 남은 세 대상: `가상동네배치기준Tests`, `가상동네하루Tests`, `운영지도AreaViewModel`. E 지도 수량은 후보819/표기813/제외3/미표기3. 이는 실제 E 달성 수가 아니다. Task의 후속 전체 solution/전체 시험 단계는 실행되지 않았다.

후속 확장에서 출처 8/8과 운영·Simulation 차이 13/13을 추가로 통과했고, 코드 지도는 9단계를 생성·조회했다. Simulation/Unity 솔루션 빌드는 경고0/오류0, v3.5 제품 묶음은 기존 경고60/오류0이다. TRX는 `artifacts/local/validation/five-domain-source-lineage-20260909/`에 보존했다. 앞의 50개와 후속 21개는 중복 가능성이 있고 검증 목적도 달라 단일 누적 완성도로 사용하지 않는다.

운영 서버 접속·주문 실행·Mongo/운영 DB 저장·Unity Editor/Play/Game View·Scene 수정·commit/push는 수행하지 않았다. 다음 기능에서도 서버의 업무 의미를 먼저 확인하되, 공유 가능한 효과 없는 규칙만 재사용하고 세션 상태 및 표현 책임은 분리한다. 실제 주문자→수락→배차→픽업→전달 화면 연결은 별도의 실행 검증 범위다.
