# MongoDB 공간자료 보관함

판본: `spatial-catalog.r3` · 최초 범위: 2026-09-08 · [r1 실제 반입·검증 결과](../Reports/공간자료-MongoDB-JSON통합-2026-09-08.md) · [법정동별 공간 패키지 r1](../AI/Planning/시스템/PLAN-SYSTEM-NEIGHBORHOOD-SPATIAL-PACKAGES/README.md)

Graph Map의 의미 관계, 배치 맵의 배치 조건, 현실 관측과 표현 사본을 하나의 **판본 고정 JSON 조회**로 제공한다. 기존 MongoDB 설정·관리자 인증·ASP.NET Core·관리자 웹을 사용한다. 새 게임 엔진이나 새 승인 대장은 아니다.

## 원본과 조회 책임

| 대상 | 계속 소유하는 사실 | 이번 Mongo 조회의 역할 |
| --- | --- | --- |
| Graph Map 파일 | 명시된 노드·관계·제약·코드 참조 | 원본 전체와 근거 위치를 판본별 보존·검색 |
| 배치 맵 파일 | 배치 의도·기준점·타일·경로·미배치 사유 | 공간 구성요소와 표현용 JSON 조회 |
| 기존 MySQL 현실 관측 | 수집·출처·사람 검토·확정 연결 | 기존 비공개 공간 색인 사본의 제한 반입. MySQL 수정 없음 |
| 출처·Reality Context 문서 | 원천 판본·권리·미확보·승인 조건 | 원문 필드를 보존. 검토 대기와 원문 상태를 분리 |
| 공통 객체 대장·동별 패키지·후보 투영 | 객체 기본 역할 괘, WI 행위 괘, 법정동 자료 판본, 원본 공간 객체 참조 | 실행 권위 없는 공통 객체 원형과 동별 관측·배치 준비 상태 조회 |
| Unity·웹 | 허용된 읽기 결과의 소비·표현 | 서버 API에서 필요한 묶음·문서·레이어·타일 페이지를 받음 |

파일 편집 권위를 MongoDB로 옮기지 않는다. Unity나 브라우저에 Mongo 접속 문자열을 주지 않는다. `Ready`는 반입 후 저장값 검증을 통과했다는 뜻이지 원문 승인, 실제 입주·영업·출입·통행, Reality Context 승인, 게임 연결 또는 E 승격이 아니다.

## 저장 구조

기존 `MongoDbOptions.Database`를 사용하는 네 컬렉션이다. 이번 실제 실행 대상은 로컬 Docker `hongdal-mongo-1 / ssalddel_dev`였다.

| 컬렉션 | 단위·역할 |
| --- | --- |
| `spatial_snapshot_sets` | 문서 ID·원본 SHA·기록 hash·구성요소 수의 manifest. `Staging`은 조회에서 제외, 전수 재조회 뒤 `Ready` |
| `spatial_documents` | 파일 하나의 특정 원본 판본. 원본 `payload`는 문자열이 아니라 native BSON 객체 |
| `spatial_elements` | 문서 안의 노드·배치 객체·건물·도로 등. `documentId + JSON Pointer`로 복수 표현 보존 |
| `spatial_relations` | 원문에 명시된 방향·참조 종류·기대 판본/hash. 가까움·같은 이름으로 생성하지 않음 |

공통 추적 필드는 `dataset`, `areaStableId`, `sourcePath`, `schema`, `revision`, `revisionKey`, `rawSha256`, `recordHash`, `adapterVersion`, `documentId`, `stableId`, `pointer`다. 공유 문서 자체의 `areaStableId`는 비어 있을 수 있으며, 그 안의 동별 구성요소와 관계는 부모 등록 항목의 값을 이어받는다. 컬렉션 종류에 맞는 필드만 갖는다. 원천 기준일과 권리는 `payload`에 남기며, 반입 생성 시각 `createdAtUtc`로 대신하지 않는다.

문자열 판본과 숫자 판본은 다른 값이다. 루트 `revision`이 없으면 원문에 값을 주입하지 않고 `revisionOrigin=ContentHashFallback`, `revisionKey=content:<SHA256>`로 구별한다. 원문 `profileRevision` 같은 별도 필드는 그대로 유지한다.

원본의 같은 경로·스키마·판본·어댑터 판본에 내용이 바뀌면 기존 문서를 덮지 않고 `SpatialSourceRevisionConflict`로 거부한다. 편집한 원문은 소유자가 정식 판본을 갱신한 뒤 새 입력 묶음으로 반입해야 한다. `spatial-catalog.r2`부터 문서 ID에도 `adapterVersion`을 결속하므로 추출기가 달라진 기록은 이전 문서를 덮지 않는 별도 불변 문서다.

JSON은 엄격하게 읽는다. 중복 필드·지원하지 않는 스키마·표현할 수 없는 숫자는 거부한다. 숫자는 가능한 정수/Decimal128로 보존하고 `$date` 같은 원문 객체를 날짜 명령으로 재해석하지 않는다. JSON 응답은 Mongo 내부 확장 형식을 노출하지 않는다. 원본 공백·필드 순서 자체는 원본 파일과 원본 바이트 SHA로 추적한다.

`recordHash`는 마스킹 전 저장 기록의 hash다. 일부 필드를 숨기거나 관계 해소 정보를 추가한 HTTP 응답 본문 자체의 hash가 아니다. 소비자가 원본 hash와 응답 hash를 혼동해 검증해서는 안 된다.

## 반입·재현

기존 [공공데이터 반입 실행 도구](../../eng/Ssalddel.PublicDataPortalImport/Program.cs)에 다음 모드를 연결했다. 자동 시작 작업·스케줄러가 아니다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- catalog-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- catalog-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- catalog-verify C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- catalog-preview C:/Users/user/source/repos/Hongdal --area-stable-id=region:kr:bjd:1126010300
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- catalog-http-verify C:/Users/user/source/repos/Hongdal
```

`preview`는 정확 20개 지도 파일과 8개 연관 직접 입력, 동별 대장에 등록한 별도 원본 2개, 같은 입력에서 결정적으로 생성한 공통 객체 대장·면목동 후보·중화동 관측 재고·동별 패키지 2개를 `artifacts/local/spatial-catalog/20260909-r5/inputs.json`에 동결한다. 기존 면목동 전용 객체 대장은 공통 대장 생성 근거로 소비하되 r3 묶음에 중복 저장하지 않아 최종 문서는 32개다. 다른 파일이 추가되거나 동결 입력이 바뀌면 거부하며 입력 목록을 자동 확장하지 않는다. 원본 파일당 8MiB, BSON 기록당 12MiB, 문서 100개·구성요소 100,000개·관계 200,000개 상한을 둔다.

`apply`는 대상 컨테이너·compose 소유·DB 이름을 확인하고 insert-only로 쓴다. 저장 전 충돌 검사, 저장 후 모든 기록의 개수·내용 hash 대조를 통과한 묶음만 `Ready`다. 새 Mongo 연결로 다시 대조한다. 같은 입력의 재실행은 기존 내용을 비교할 뿐 중복 문서를 만들지 않는다. 부분 실패는 `Staging`으로 남아 조회되지 않으며 같은 입력으로 재시도한다. 자동 삭제·덮어쓰기·자료 보충은 없다. Docker의 기존 비밀값은 실행 메모리에서만 사용하며 파일·로그·응답에 쓰지 않는다.

### r2 면목동 객체 대장 묶음

2026-09-09 현행 로컬 묶음 `bundle:4387BA718A423162819082FD07C8E828E105B6A3D4216C64C589FC8AEB78CC66`은 28문서·5,791요소·7,945명시 관계다. 객체 대장 안에는 오행 업무 모듈 결속 5, 객체 원형 26, WI Profile 11, 역할·행위 결속 25가 있다. 결정적 후보 투영은 원본 건물 602개를 개별 `BackdropOnly` 후보로, 도로 2,397개를 하나의 중립 도로망 후보로 참조한다. 독립 재조회와 같은 입력 재반입 신규 0, 원본 hash·mtime 불변을 확인했고 다른 Mongo 컬렉션, MySQL, Unity, 게임 상태는 변경하지 않았다.

첫 반입 시도는 어댑터 r2의 record가 이전 r1 문서 ID를 재사용하여 `SpatialSourceRevisionConflict`로 거부됐다. 이전 데이터를 덮어쓰거나 삭제하지 않고 문서 ID에 어댑터 판본을 결속했다. 이어 반입한 r3 묶음은 사후 안정 ID 대조에서 source manifest 참조 오기를 발견해 현행에서 제외했다. 이 기록도 삭제하지 않고 객체 대장 revision을 올린 뒤 Graph Map·배치 Map·source manifest의 실제 안정 ID와 판본을 함께 검증한 r4 묶음을 현행으로 삼았다.

### r3 법정동 패키지 묶음

2026-09-09 현행 로컬 묶음 `bundle:4055D8D43005D7BDD2D99C2878394DA9516C2A98034251BE0244D1435CB3BB1E`은 32문서·7,490요소·9,652명시 관계다. 공통 오행 업무 객체 원형 26개와 WI Profile 11개를 한 번만 저장하고 면목동·중화동 패키지가 이를 참조한다. 면목동의 건물602·도로2,397 및 후보603은 호환 유지했다. 중화동은 상가1,672·공공시설14의 개인정보 축소 관측 1,686건과 그중 좌표 후보1,683건만 보존한다.

첫 반입 신규17,175기록, 독립 재조회, 같은 입력 재반입 신규0을 확인했다. 명시 관계 해소 결과는 정확6,011·외부/미해소2,990·복수 표현651이며 해소 결과는 승인이 아니다. 다른 Mongo 컬렉션, MySQL, Unity와 게임 상태는 변경하지 않았다.

## 조회 계약

모든 경로는 서버관리자 정책이 필요한 `GET /api/v1/admin/spatial-catalog` 하위다. 새 쓰기·승인 API는 없다.

| 경로 | 조회 |
| --- | --- |
| `/snapshots` | 검증 완료 묶음 목록. 각 항목의 `_id`가 다음 조회의 `bundleId` |
| `/documents` | 원문 metadata 목록. 목록에서는 전체 payload 제외 |
| `/documents/{documentId}` | 지정 묶음의 원문 전체 JSON. `bundleId` 필수 |
| `/elements` | 문서·종류·고유 ID·레이어·타일별 구성요소 |
| `/relations` | `relationKey`에 대한 `incoming / outgoing / both` 명시 관계 |
| `/presentation` | 지정 문서/레이어/타일의 현재 페이지와 원래 좌표계. `bundleId`, `documentId` 필수 |

일반 필터: `dataset`, `areaStableId`, `kind`, `revision`, `reviewState`, `stableId`; 구성요소에는 `layer`, `tile`, `documentId`. `areaStableId`는 문서뿐 아니라 공유 등록 문서 안의 동별 구성요소와 관계도 제한한다. 문자열 동등 비교만 지원하며 Mongo 필터·실행식은 받지 않는다. `revision`은 목록의 **`revisionKey` 값을 그대로 URL 인코딩**하여 전달한다. 문자열 판본의 따옴표와 숫자 판본을 구분하기 위해서다.

기본 100개·최대 500개, `skip`, `take`, `nextSkip`을 제공한다. 첫 조회에서 받은 `bundleId`를 다음 모든 페이지에 넣는다. `skip>0`인데 묶음이 없으면 400이다. 최초 조회에서 최신 묶음을 선택할 수 있지만 이후 페이지가 새 반입과 섞이지 않는다. 스냅샷 목록은 발견용 목록이며 자체 결과의 `bundleId`는 비어 있다.

관계 결과의 `sourcePath`, `pointer`, `kind`, `fromKey`, `toKey`로 원문을 찾는다. 같은 문서의 정확 ID를 우선하고, 서로 다른 표현/판본이 남으면 다중 후보로 표시한다.

- `ExactReferenceInBundle`: 이 묶음 안의 정확한 원문 참조 1개. 공식 건물 식별 연결 또는 승인이라는 뜻은 아니다.
- `MultipleRepresentationsOrRevisions`: 복수 표현·판본이 있으므로 합치지 않음.
- `ExternalOrUnresolved`: 외부 문서·코드·WI/H 또는 아직 반입되지 않은 대상. 자동 로드하지 않음.
- `SourceRevisionMismatch`: 원문이 요구한 대상 판본/hash와 불일치. 낮은 근거로 대체하지 않음.

표현 사본은 `Geometry / PlacementMap / PresentationProjection`에 한정한다. 사가정 데이터의 WGS84 기준 ENU 원점·offset·meters를 보존하며 EPSG:5186 도형과 혼합하지 않는다. 500m 타일 배정은 기존 외곽 중심/도로선분 중점 규칙에 따른 조회 후보일 뿐 포함 면적·출입구·통행 판정이 아니다.

잘못된 질의는 400, 없는 묶음/소속 아닌 문서는 404, 저장 자료 손상·DB 실패는 503으로 구별한다. DB 예외의 접속정보·상세 원문은 반환하지 않는다. 취소 요청을 예시 결과로 바꾸지 않는다.

## 개인정보·소비

기본 응답은 알려진 `name`, 상세주소, 건물관리번호, 전화·소유자/거주자명 필드를 제거한다. 관리자만 `/documents/{id}?bundleId=...&includeSensitive=true`로 명시적 상세 원문을 받을 수 있다. 원문 안의 모든 자유문장 개인정보를 포괄 검증했다는 뜻은 아니며, 기본 응답도 비공개 검토용이다. 외부 배포 전 별도 최소화·권리 검토가 필요하다.

관리자 웹 [SpatialCatalog](../../SsalddelAdmin/Components/Pages/SpatialCatalog.razor)의 주소는 `/spatial-catalog`다. 묶음→문서→구성요소→관계/표현 JSON 순서로 조회하며 기본 숨김을 유지한다. 현재 문서 목록/관계 목록은 각각 최대 100개 표시, JSON 화면은 16,000자 미리보기다. API의 페이지·전체 문서 응답과 혼동하지 않는다.

Unity용 런타임 접속 코드는 이번에도 추가하지 않았다. r4의 로컬 `myeonmok-game-object-unity-handoff.r2.json`은 호환 증거로 보존하고 r3 동별 패키지에서 새 Scene 인계를 자동 생성하지 않는다. 향후 승인된 소비자는 같은 묶음과 hash를 검증한 개인정보 축소 투영만 사용해야 한다. Unity 클라이언트에 관리자 토큰을 내장하지 않으며, 제품 배포용 인증/권한 축소 API는 별도 준비 대상이다.

## 최초 추출 범위의 한계

현재 묶음의 원본·파생 전체 32문서를 보존하지만 모든 업무 의미를 자동 해석하지 않는다. 현재 확인한 배열 구조만 구성요소로 추출한다. 객체 후보와 중화동 관측 재고는 공간 원본과 WI 분류의 참조이며 음식점·주택·창고 선택, 출입구, 통행 가능성, Prefab 또는 업무 권위를 만들지 않는다. `farm-cultivation-plot-seed-alignment.v1.json`, `hans-farm-hex03-development-handoff.v1.json`은 독립 구성요소 0개이며 전체 payload/명시 참조 조회로 남는다. singleton 규칙이나 문자열 배열이 비어 있다는 뜻이 아니다.

`Ref/Refs`와 확인된 참조 필드만 엣지로 추출한다. source manifest의 `logicalResourcePath` 등 미지원 필드는 원문 payload로 보존되지만 자동 엣지화하지 않는다. 현재 단계를 모든 자료의 의미 결속 완료나 모든 경로의 대상 해소로 보고하지 않는다. 필요한 확장은 원문 관계 검토와 추출 판본 migration을 함께 설계한다.

## 로컬 검증 화면

`catalog-http-verify`는 실제 Controller/Mongo Store를 임시 서명 JWT로 자동 검증하고 바로 종료한다. `catalog-serve`는 같은 API와 빌드된 관리자 웹만 127.0.0.1:5298/5299에 최대 25분 실행하는 수동 검증 모드다. 기존 관리자 앱의 DevelopmentBootstrap을 사용하지만 이번 실행의 임시 서명 JWT만 메모리로 전달한다. 다른 작업의 `appsettings.Local.json`, 계정, background job, MySQL 초기화는 읽거나 실행하지 않는다. 기존 운영 로그인의 검증을 대신하지 않는다.

같은 artifacts 폴더의 `stop-local-host` 파일로 자기 검증 host를 종료할 수 있다. 종료 후 다시 검증하려면 그 실행이 종료된 것을 확인하고 해당 신호 파일만 제거한다. 주 서버 컨테이너의 재시작 실패를 이 도구로 숨기거나 해결했다고 보고하지 않는다.
