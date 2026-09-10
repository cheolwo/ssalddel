# 공간자료 MongoDB JSON 통합 r1

작업 시작: 2026-09-08 · 마감 검증: 2026-09-09 KST · 코드/로컬 Mongo/읽기 API/관리자 웹 구현 · 커밋·푸시 없음.

[구조·API·재현 방법](../Architecture/Mongo공간자료Catalog.md) · [화면 변경 기록](../Changes/2026-09-08-spatial-catalog.md)

## 요청과 완료 범위

Graph Map, 배치 맵, 현실 관측·출처·표현 자료를 기존 Docker MongoDB에 JSON 객체로 구조화하여 Unity와 웹이 같은 의미·관계를 조회할 수 있는 기반을 구현했다. 기존 파일이 편집 원본이고 MongoDB는 검토 전용 판본 사본이다. Unity 실제 소비·게임 상태·공식 연결·사람 승인을 이번에 구현하거나 변경하지 않았다.

최초 범위는 지도 폴더 전체 20파일(11 Graph Map/9 배치 맵)과 연관 자료 6파일이다. 모든 현실자료·관계 대상 문서를 재귀 수집하는 반입이 아니다. 면목동 주소204/활동801/표본30의 기존 제한 공간 색인을 재사용했다. 기존 사업체5,537행 전체를 새 공식 공간 연결로 만들지 않았다.

## 실제 저장 결과

대상: 로컬 `hongdal-mongo-1`, 기존 앱 설정의 `ssalddel_dev`.

| 컬렉션 | 저장 수 | 의미 |
| --- | ---: | --- |
| `spatial_snapshot_sets` | 1 | 전수 재조회 검증을 통과한 `Ready` 묶음 |
| `spatial_documents` | 26 | 원문 전체 native BSON 문서 |
| `spatial_elements` | 5,115 | 문서별 구성요소. 같은 대상의 판본·표현 중복을 보존 |
| `spatial_relations` | 3,689 | 원문에서 추출한 명시 참조·Graph 간선 |

묶음 ID: `bundle:2F51CCC4F230ED8C3F648F0E0710C107611CDF561FDC6B4676FF13AD006EBC4E`.

첫 반입 8,831기록, 같은 입력의 두 번째 반입 **신규0**. 서로 다른 MongoClient 연결로 저장 개수·내용 hash를 전수 재조회했다. 원본26파일의 SHA256·수정 시각은 불변이다. 대상 DB의 공간 컬렉션 외 기존 컬렉션 집계는 전후 `{}`로 같았다. 이는 다른 DB의 전체 데이터 불변 hash를 측정했다는 주장이 아니다. 이번 코드에서 MySQL 쓰기를 실행하지 않았다.

주요 구성요소는 건물602·도로선분2,397·주소204·활동관측801·토지표본30·건물표현204, Graph 노드171/간선174 등이다. 구성요소와 관계 수는 **원문 표현 기록 수**이며 실제 영업체·독립 건물·공식 관계의 전수 집계가 아니다. 원문 v1/v2도 각각 보존했다.

최종 독립 조회에서 관계 3,689개를 500개씩 전수 확인했다. 묶음 안의 명시 참조 대상 확인 `ExactReferenceInBundle` 2,366, 외부 또는 미해소 1,279, 복수 표현/판본 44다. 참조 대상 확인은 현실에서 같은 건물이라는 공식 결속이나 승인·게임 연결 성공을 뜻하지 않는다.

반입 영수증의 `committed=true`는 Mongo 검증 완료 상태이며 Git commit을 뜻하지 않는다.

## 자료와 코드 연결

| 변경 경로 | 책임 |
| --- | --- |
| [공통 JSON contract](../../Ssalddel.Contracts/Common/WorldProjection/공간자료CatalogContracts.cs) | 묶음/페이지/상세 일반 JSON. Mongo driver 타입 비노출 |
| [SnapshotBuilder](../../Ssalddel/Services/WorldProjection/SpatialCatalog/공간자료SnapshotBuilder.cs) | 원본 판본·hash/ID·구성요소·명시 관계 추출 |
| [JSON 변환](../../Ssalddel/Services/WorldProjection/SpatialCatalog/공간자료Json.cs) | 숫자·문자열·중복 필드 검사/native BSON/기본 숨김 |
| [Mongo Store](../../Ssalddel/Services/WorldProjection/SpatialCatalog/공간자료CatalogStore.cs) | 기존 Mongo 설정, 제한 컬렉션, insert-only/인덱스/재조회 |
| [CatalogService](../../Ssalddel/Services/WorldProjection/SpatialCatalog/공간자료CatalogService.cs) | 묶음 반입·검증·고정 페이지/직접·역관계/표현 사본 |
| [관리자 Controller](../../Ssalddel/Controllers/Admin/공간자료CatalogController.cs) | 기존 관리자 정책을 거치는 읽기 전용 API |
| [DI 연결](../../Ssalddel/Extensions/ServiceCollectionExtensions.SpatialCatalog.cs) | 기존 서버 Program에 등록 |
| [기존 수집 도구의 반입 모드](../../eng/Ssalddel.PublicDataPortalImport/공간자료CatalogImport.cs) | 정확26파일 동결·로컬 대상 검사·저장/독립 재조회 |
| [수동 검증 host](../../eng/Ssalddel.PublicDataPortalImport/공간자료Catalog검증Host.cs) | 실제 Controller/Mongo와 관리자 웹의 제한 로컬 실행 |
| [관리자 화면](../../SsalddelAdmin/Components/Pages/SpatialCatalog.razor) | 판본·묶음·문서·레이어·타일·페이지·관계·JSON 미리보기 |
| [관리자 조회 adapter](../../SsalddelAdmin/Services/공간자료CatalogAdminService.cs) | 기존 로그인 세션의 관리자 토큰 사용 |
| [단위 시험](../../Ssalddel.Tests/Services/WorldProjection/공간자료CatalogTests.cs), [HTTP 시험](../../Ssalddel.Tests/Services/WorldProjection/공간자료CatalogHttpTests.cs) | 저장·권한·오류·판본·미지원·원본 불변/40개 |

기존 서버/관리자/반입 도구 `Program.cs`에는 해당 등록·모드 분기만 추가했고 기존 사용자 변경은 보존했다. 기존 관리자 navigation에 `/spatial-catalog`을 연결했다. 새 DB·공개 API·게임 계약·원천 수집기·자동 승격은 없다.

## 실제 HTTP·화면 확인

주 앱 컨테이너 `hongdal-app-1`은 점검 당시 `Restarting / exit139` 상태였다. 이를 수리하거나 DB를 초기화하지 않았다. 실제 새 Controller/Store를 연결하는 제한 검증 host를 127.0.0.1:5298, 실제 관리자 웹을 5299로 실행해 확인했다.

- 실제 HTTP: 비로그인401/일반 역할403/서버관리자200. 기본 개인정보 필드 숨김, 관리자 명시 상세, 없는 묶음404, 501개 요청400을 확인했다.
- 실제 Mongo 건물602를 500+102 두 페이지로 조회했고 중복ID0/같은 묶음 ID를 확인했다. 표현용 JSON과 건물 역관계 응답도 받았다.
- 실제 관리자 UI: 문서26조회, `SagajeongReference.json` 선택→건물 레이어602→1~100/101~200 이동, 표현 JSON의 좌표계/권한 플래그, Graph Map 지역 노드의 명시 관계178(화면 최대100), 관계 없는 건물0건 안내를 확인했다.
- 실제 UI 점검으로 발견한 페이지 이동 뒤 이전 표현 미리보기 잔류는 구성요소 재조회 시 JSON/관계/집계 초기화로 수정했다. 미기재 원본 판본 안내와 관계의 원문 경로 표시도 보강했다.
- 최종 바이너리에서 건물 레이어 1~100의 표현 JSON을 연 뒤 다음 101~200으로 이동했다. 같은 묶음/전체602 유지와 이전 표현 JSON 제거를 실제 UI 입력/조회로 재확인했다. 최종 [실제 화면](../assets/changes/2026-09-08-spatial-catalog/catalog-final.png)은 개인정보 숨김 안내와 선택한 도형 문서를 표시한다.

이 실행의 인증은 메모리에만 존재하는 임시 서명 JWT와 기존 DevelopmentBootstrap을 쓴 검증 신원이다. **기존 운영 계정 로그인·주 서버의 정상 시작·운영 배포를 검증한 것은 아니다.** 사용자 공공데이터 탭, 기존 컨테이너, Unity Editor는 조작하지 않았다.

제한 host에 기존 앱의 `view-settings/effective` 경로는 등록하지 않아 해당 별도 요청의 404가 관측됐다. 공간자료 API 검사와 구분하며 전체 앱 요청·브라우저 Console 오류0으로 보고하지 않는다. Console 독립 수집/모바일 실기기 검증은 미실행이다.

## 시험·검증

신규 단위/HTTP **40/40, skip0**: 원문 JSON 왕복·정밀도·문자열/숫자 판본, 알 수 없는 스키마/중복 필드 거부, 같은 이름 미연결, 원문 기대 hash/판본 불일치, 저장 재조회/멱등성, 부분 실패 숨김·재시도, 이전 묶음 페이지 고정, 기본 숨김/명시 상세, 도형 좌표계, 손상·문서 누락 실패, 취소·400/404/503·쓰기 경로 없음, 전체20지도/13스키마 판독, 전체6조회 경로의 관리자 경계.

새 메타데이터를 붙인 첫 범위 Fast에서 `ReadsFrom/WritesTo`의 string/enum 타입 불일치가 발견되어 지정 enum으로 수정했다. 최초 실패 로그는 보존했고 검사 완화는 하지 않았다. 최종 범위 Fast/Task 및 최종 재조회 결과는 마감 절에 기록한다.

시험 Fixture가 실제 데이터 정확도·권리·현실 건물 연결·게임 상태를 증명하지 않는다. 상호/주소의 기본 숨김은 현재 필드에 대한 최소화이며 자유문장 전체의 개인정보 안전 검토가 아니다.

## 재현 근거와 해석 한계

실제 로컬 근거: `artifacts/local/spatial-catalog/20260908-r1/`.

- `inputs.json`: 정확26경로/종류/hash/bytes/mtime 동결.
- `apply-20260908T1438498412065.json`: 최초 저장·독립 재조회.
- `apply-20260908T1439389463331.json`: 같은 입력 신규0.
- `verify-20260908T1501354304238.json`: 최신 코드의 독립 Mongo 전수 재조회/관계 해소 집계/원본 불변.
- `http-20260908T1502180700520.json`: 최종 바이너리의 실제 Mongo 기반 JWT 미들웨어/API 검사.
- `tests/spatial-catalog-unit-final.trx`: 신규40/40.
- `browser/catalog-overview.jpg`: 첫 실제 관리자 화면. 브라우저가 반환한 원 JPEG를 보존했다. PNG라고 바꿔 부르지 않는다.
- `browser/catalog-final.jpg`, `browser/verification.json`: 최종 UI 재검증과 원본 JPEG. 문서용 PNG는 이 JPEG를 형식만 변환한 981×903 이미지이며 전체 디코드 픽셀 차이0을 확인했다. 재촬영·합성·AI 생성이 아니다.

모든 26원문은 읽을 수 있지만 추출기 r1의 의미 해석은 제한적이다. singleton 배치 규칙과 일부 문자열 배열은 독립 구성요소로 추출되지 않으며, 확인된 배열과 Ref 필드만 관계로 만든다. `logicalResourcePath` 자동 엣지화 등은 남은 범위다. 외부 문서·H/WI·코드·미반입 자산 대상은 미해소로 남고 이름·근접 좌표로 메우지 않는다. [한계와 판본 변경 절차](../Architecture/Mongo공간자료Catalog.md#최초-추출-범위의-한계)를 따른다.

웹은 관리자 검토용 목록/JSON 조회다. 실제 지도 그리기·디오라마 조립 UI가 아니며 모바일 실기기 접속·Unity HTTP 클라이언트·실제 Scene 연결·공개 배포는 후속이다. 모든 JSON을 Unity가 지금 직접 불러오고 있다고 보고하지 않는다.

## 마감 검증

| 검증 | 실제 결과 |
| --- | --- |
| 신규 단위/HTTP | 40/40, skip0 |
| 범위 Fast | v3.5 빌드 및 신규40+기존 관리자4 = 44/44 통과. `artifacts/local/validation/20260908-235846/` |
| 완료 Task | v3.5 빌드 통과. 전체 시험 4,940/4,947 통과, 7실패, skip0. `artifacts/local/validation/20260909-000107/` |
| 별도 관리자 빌드 | 오류0/경고0 |
| 문서 | 범위 Fast 통과(빌드/시험 생략), `artifacts/local/validation/20260909-001236/`. 6문서의 로컬 링크551/551 존재 확인. 최종 원본26 SHA·bytes·mtime 차이0 |
| 실제 Mongo | 최초8,831저장/동일입력 신규0/별도 연결 전수 재조회/26원본 hash·mtime 불변 |
| 실제 HTTP·UI | 관리자 권한·602건 페이지 고정·기본 숨김·JSON·명시 관계와 UI 잔류 수정 재검증 |
| 종료 | 검증용 탭2만 닫고 자기 host 종료 exit0. 2026-09-09 00:09:41 KST에 5298/5299 리스너0 확인. Mongo 자료 보존 |

전체 Task의 실패 대상은 이번 변경 경로 밖이다. 실패를 고치기 위해 관련 없는 코드를 수정하거나 검사를 완화하지 않았다. 시작 전 전체 시험을 재현한 것은 아니므로 이번 결과를 근거로 과거에도 정확히 같은 실패였다고 단정하지 않는다.

- `Controllers_HaveIntroductionHistoryMetadata`: 기존 지도신청/개인정보동의 Controller 3개 이력 메타데이터 누락. 새 공간자료 Controller는 실패 목록에 없다.
- `통합_WebApp의_모든_컴파일된_라우트는_capability_규칙으로_분류된다`: `/community/map-application-chooser` 분류 누락.
- `공식재료화면은_재료이름과_좁은폭동작영역을_실제값으로연결한다`: 기존 식재료 CSS의 `min-height: 44px` 기대 미충족.
- `RoleAppControllers_HaveAudienceAndBusinessCapability` 3사례: 주문자/기사 주거공동체 업무 영역 및 화주 농장생산자 audience 불일치.
- `CommonAndAdminDomainControllerActions_UseKoreanOrApprovedTechnicalPrefixes`: 기존 `농수산정보Controller.Hs식품국가가격Card조회` 동작 명명.

서버/도구 빌드의 기존 `커뮤니티세계지도원장ProjectionController.cs:88` CS8602, `CommunityVoteService.cs:469` CS8604 및 시험의 `개체시각대응SchemaTests.cs:21` xUnit2031 경고는 별도다. 관리자 빌드의 경고0을 전체 저장소 경고0으로 확대하지 않는다.

최종 보존 점검의 첫 임시 경로 식은 `unity:` 논리 경로/문서의 절대 경로를 일반 상대 경로로 취급해 관측 오류를 냈다. 기존 반입기의 정확 `unity:`→Unity 루트 대응과 절대 경로 분기를 적용한 읽기 점검에서 원본26 불변/링크551 존재를 재확인했다. 데이터·원문을 고쳐서 통과시킨 것이 아니다.

코드·저장·읽기 API·관리자 UI 범위는 구현/검증했으나 전체 회귀 합격·운영 배포·Unity 실제 소비는 미완료다. 다음에는 필요한 소비자 권한과 공개 가능한 필드만 좁힌 조회 계약을 정해 연결한다. 관리자 토큰을 Unity에 내장하거나 미해소 관계를 게임 권위로 승격하지 않는다. 주 서버의 재시작 문제는 별도 진단 대상이며 이번 수집/공간 코드로 우회 해결했다고 보고하지 않는다.
