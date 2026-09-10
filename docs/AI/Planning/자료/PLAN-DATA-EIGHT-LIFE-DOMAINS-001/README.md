# 생활 여덟 영역 공공데이터 조사·구현 계획

- 기획 ID: `PLAN-DATA-EIGHT-LIFE-DOMAINS-001`
- 사용자 표시명: `생활 여덟 영역 공공데이터 조사·구현`
- 분야: `자료 / 공공데이터 / Reality Context`
- 판본: `life-domain-public-data.plan.r1`
- 작성일: 2026-09-08
- 상태: `Draft / ReadyForReview`
- 상위 기획: `PLAN-DATA-REALITY-MYSQL-001`
- 입력 원문: `C:/Users/user/.codex/attachments/0a9ccf79-ecd7-4f7c-ae87-adfea4966712/pasted-text.txt`
- 입력 SHA-256: `F7B1F30A8639B34008E4781FC37EA0B795F46A40E505EF74583D3199FEBFAC91`
- 현행 운영 기준: [게임 자료 조사 전문 운영](../../../../Architecture/게임자료조사전문운영.md), [현실 자료 서버·MySQL 축적](../../../현실자료-서버MySQL축적-기획과개발인계-2026-08-31.md)
- 실행 승인: 이 문서는 구현 순서와 관문을 정하는 계획이다. 신규 공급자 호출·키 사용·DB 쓰기·게임 규칙·Unity 실행은 별도 실행 범위와 현재 자격을 확인한 뒤 수행한다.

## 1. 목표

사용자가 제안한 여덟 생활 영역을 공공데이터 조사와 구현의 **사람이 읽는 분류**로 사용한다. 각 영역에서 공식 자료의 정확한 서비스·접근·권리·필드·신선도를 검증하고, 기존 서버의 원문 사본과 정규화 원장에 소량으로 축적한 뒤 읽기 전용 해석 자료로 제공한다.

이 계획의 최종 흐름은 다음과 같다.

```text
공식 자료 후보
  → 출처·접근·권리 검토
  → 기존 Source Catalog 등록
  → 공급원별 Collector 또는 파일 가져오기
  → RawSnapshot + SHA-256
  → 공급원별 Normalizer
  → 외부데이터정규화Record + MySQL
  → 별도 문맥 재조회·중복 방지
  → 영역별 읽기 Projection
  → 검토·승인된 자료만 Reality Context 후보
  → 별도 기획 승인을 받은 Simulation 규칙
```

공공데이터가 게임 상태를 직접 바꾸지 않는다. `수집 성공`, `저장 성공`, `영역별 조회 가능`, `Reality Context 채택`, `Simulation 규칙 승인`은 모두 다른 상태다.

## 2. 여덟 영역과 안정 코드

한자식 명칭은 사용자 화면의 기본 이름으로 쓰지 않는다. 아래 한국어 이름을 표시하고, 저장·API 관계에는 언어와 무관한 안정 코드를 사용한다.

| 순서 | 안정 코드 제안 | 한국어 표시명 | 포함하는 현실 자료 | 첫 제품 활용 후보 |
| --- | --- | --- | --- | --- |
| 1 | `food-and-livelihood` | 먹고사는 일 | 농수산물, 먹거리, 음식점, 식단, 영양 | 식재료·가격·식사 후보를 설명하는 읽기 자료 |
| 2 | `goods-and-commerce` | 물건과 장사 | 상품, 가격, 상점, 시장, 유통, 상권 | 시장·상점·가격 관측과 거래 후보 설명 |
| 3 | `community-culture` | 제사와 공동체 문화 | 축제, 세시풍속, 문화유산, 공동체 행사 | 지역 행사·문화 근거 카드 후보 |
| 4 | `home-road-and-place` | 집, 길, 터전 | 토지, 건물, 주거, 도로, 공간정보 | 월드·AreaSet·H 후보를 검토하는 현실 공간 문맥 |
| 5 | `teaching-and-learning` | 가르치고 배우는 일 | 학교, 교육과정, 시간표, 급식, 평생학습 | 학습 장소·시간·과정 설명 후보 |
| 6 | `safety-and-public-order` | 치안과 법질서 | 범죄, 신고, 순찰, 안전시설 | 위험·안전시설의 검토용 지역 문맥 |
| 7 | `visitors-and-exchange` | 손님과 바깥 교류 | 관광객, 외부 인구, 교통 흐름, 방문 | 방문자·관광·이동량 설명 후보 |
| 8 | `defense-and-emergency` | 방위와 비상대응 | 대피, 재난, 민방위, 비상시설 | 대피·재난 준비의 참고 문맥 |

이 코드는 새 게임 단계나 H/WI가 아니다. 하나의 자료가 여러 영역과 관계될 수 있으므로 주 영역 하나와 보조 영역 여러 개를 허용하되, 이름이나 주제가 비슷하다는 이유만으로 관계를 자동 확정하지 않는다.

## 3. 현재 저장소에서 재사용할 구현

첨부의 `현실자료공통기록`을 그대로 새로 만들지 않는다. 현재 공통 원장이 이미 핵심 필드를 소유한다.

| 첨부의 요구 | 기존 구현 | 계획상 처리 |
| --- | --- | --- |
| 자료·공급원 목록 | [`PublicDataApiMetadataCatalog`](../../../../../Ssalddel/Services/External/PublicData/PublicDataApiMetadataCatalog.cs), [`ExternalDataSourceCatalog`](../../../../../Ssalddel/Services/External/PublicData/Foundation/ExternalDataSourceCatalog.cs) | 기존 카탈로그에만 등록하고 병렬 목록을 만들지 않음 |
| 접근 방식·자격 | `ExternalDataSourceDefinition.AccessMethod`, `CredentialType`, `RequiresCredential`, `CredentialReferences` | 자격이 없으면 `MissingCredential`로 조기 차단 |
| 수집 실행 이력 | `외부데이터수집Run` | 성공·부분·실패·취소와 건수를 분리 |
| 원문·hash·판본 | `외부데이터RawSnapshot` | 원문 SHA-256 중복 방지와 실제 저장 위치 보존 |
| 정규화된 값 | `외부데이터정규화Record` | 지역·지표·값·단위·기준시각·정밀도·품질·한계·판본을 재사용 |
| 지역 대응 | `외부지역CodeMapping`, `RegionStableIdRules` | 외부 지역코드를 추측 변환하지 않고 유일 대응만 허용 |
| 수집→정규화→저장 | [`ExternalDataIngestionRuntime`](../../../../../Ssalddel/Services/External/PublicData/Foundation/ExternalDataIngestionRuntime.cs) | 공급원별 Collector·Normalizer만 추가 |
| Simulation 읽기 | [`SimulationRealityContextService`](../../../../../Ssalddel.Simulation.Application/SimulationRealityContextService.cs) | 승인·현재·유효 자료만 세션 시작 시 동결; Provider 직접 호출 금지 |

### 공통 레코드 대응

| 제안 필드 | 기존 필드 또는 관계 |
| --- | --- |
| `자료ID` | `StableId`, `RecordKey` |
| 제공기관·원본 URL·영역 | `ExternalDataSourceDefinition.Provider`, `OfficialSourceUrl`, `DataDomain` |
| `관측시각`, `자료기준시각` | `CollectedAtUtc`, `EvidenceAsOfUtc` |
| 공간범위·위치 | `RegionStableId`, `SpatialPrecisionCode`, `DimensionKey` |
| 원분류·표준분류 | `DimensionKey`, `MetricCode`; 공급원별 원코드는 원문 사본과 변환 근거에 보존 |
| 값·단위 | `NumericValue` 또는 `TextValue`, `UnitCode` |
| 원문 hash·판본 | `외부데이터RawSnapshot.ContentHashSha256`, `SourceVersion`, `DataRevision` |
| 누락·품질·제한 | `QualityCode`, `LimitationCode` |

도메인별 상세 구조는 공통 원장을 복제하는 테이블이 아니라, 실제 소비자가 생겼을 때 만드는 읽기 전용 Projection 또는 명시적 관계 원장으로 제한한다.

## 4. API별 20개 조사 항목과 현재 계약의 간격

먼저 모든 후보를 같은 조사표로 검증한다. 기존 필드로 충분한 항목은 새 schema를 만들지 않는다.

| 조사 항목 | 기존 지원 | 보완 원칙 |
| --- | --- | --- |
| 정확한 API·데이터셋명, 제공기관 | 있음 | 공식 문서의 현재 명칭과 대조 |
| 공공데이터포털 서비스 ID | URL에 포함될 수 있으나 전용 필드는 없음 | URL 파싱에 의존하지 말고 조사 근거에 명시; 반복 조회가 필요할 때만 선택 필드 검토 |
| 운영 상태 | `ImplementationStatusCode`, `ApiAvailable` 일부 지원 | `Connected/NeedsServiceKey/ReferenceOnly`와 `Deprecated/Unavailable`을 혼합하지 않음 |
| REST·파일·OGC 등 접근 방식 | `ApiType`, `AccessMethod` 있음 | 실제 endpoint와 다운로드 형식을 함께 확인 |
| JSON·XML·CSV·GeoJSON | 있음 | 문서 표기와 실제 응답을 별도 기록 |
| HTTPS 여부 | URL로 확인 가능 | 신규 HTTP endpoint는 허용하지 않음 |
| 인증·계정·활용신청 | 일부 지원 | 키 필요와 실제 활용승인 완료를 구분; 비밀값 저장·출력 금지 |
| 호출 제한 | 없음 | 공식 문서에서 확인하고 공급원 정책에 기록; 미확인은 미확인 유지 |
| 갱신 주기·기준 시점 | 있음 | 실제 자료의 기준시각과 다운로드 시각을 분리 |
| 전국·지역 범위 | 일부 지원 | 행정구역 수준과 누락 지역 기록 |
| 좌표계 | 전용 필드 없음 | 공간 자료만 원 좌표계·변환 여부·정밀도를 필수 기록 |
| 주요 필드 | `MainResponseFields` 있음 | 실제 표본과 문서가 다른 경우 수집 차단 |
| 누락·비공개 처리 | 오류·사용 주의 일부 지원 | `0`이나 빈 문자열로 대체하지 않고 거부·누락 코드로 보존 |
| 이용조건·출처 표기 | `License`, `RedistributionAllowed`, `AttributionRequirement` 있음 | 미확인 시 재배포 기본 금지 |
| 변경 이력 | 원문·SourceVersion·DataRevision 일부 지원 | 조사일·문서 판본·응답 hash를 함께 보존 |
| 프로젝트 연결 | `Purpose`, `DataDomain`만 부분 지원 | WI/H/화면을 자동 연결하지 않고 별도 근거 관계로 관리 |
| 권위 역할 | 코드 필드 없음 | `원문 관측 / 정규화 / 설명 후보 / 승인 규칙`을 문서·Projection에서 분리 |

전용 필드가 없다는 사실만으로 즉시 공통 DTO를 확장하지 않는다. 첫 세 영역의 실제 후보를 대조하여 같은 누락이 두 공급원 이상에서 반복되고 조회·검증에 필요할 때만 호환 가능한 선택 필드를 추가한다.

## 5. 후보 공급원은 조사 대기열로만 취급한다

첨부에 적힌 서비스명·ID·URL·운영 여부는 이 판본에서 **후보**다. 공식 문서와 실제 응답을 확인하기 전 `Connected`나 `사용 가능`으로 올리지 않는다.

| 영역 | 우선 조사 후보 | 현재 저장소 재사용 출발점 | 이 계획에서 아직 확정하지 않는 것 |
| --- | --- | --- | --- |
| 먹고사는 일 | KAMIS/aT 가격, 식품 영양, 음식점 | KAMIS archive, aT 조회, 농사로, 지방행정 인허가 음식점 | 모든 식품 코드 동일성, 영양·가격의 게임 효과 |
| 물건과 장사 | 상권, 소비자 가격, 전통시장 | 전통시장 metadata/client, 지방행정 인허가 사업장, 온라인 가격 | 매출·유동인구의 현재 상권 가치, 자동 거래 추천 |
| 제사와 공동체 문화 | 행사, 축제, 무형유산 | TourAPI 지역문화 metadata/client | 지역 대표성, 행사 개최 확정, 이미지 재배포 |
| 집, 길, 터전 | 건축물, 토지이용, 연속지적, 2D 공간정보 | 법정동·행정동 코드, VWorld 건물, 동네 공간 source | 지형·도로 통행 가능, AreaSet/H 또는 Unity 배치 완료 |
| 가르치고 배우는 일 | NEIS 학교·교육·시간표·급식, 평생학습 | 현재 정확한 공통 수집 경로부터 재고 조사 | 학생·개인정보, 학교 접근 권한, 학습 효과 |
| 치안과 법질서 | 안전지도, CCTV, 112 통계 | 현재 정확한 원천·공간 정밀도 조사부터 시작 | 주소 단위 범죄 위험, 개인·사건 식별, 순찰 효과 |
| 손님과 바깥 교류 | 관광, 외국인, 공항·철도·버스 흐름 | TourAPI, KOSIS 지역통계 | 방문자 도착 사건, 실시간 교통, NPC 생성 |
| 방위와 비상대응 | 대피시설, 화재·재난, 민방위 | 현재 정확한 시설 source와 지역 mapping 조사 | 군사 정보, 실제 안전 판단, 게임 방어 규칙 |

## 6. 구현 순서

### 0단계 — 기준선과 중복 조사

1. `PublicDataApiMetadataCatalog`, 모든 `IExternalDataSourceRegistration`, 공급원별 client/collector/normalizer, 관련 시험을 목록화한다.
2. 후보마다 `재사용`, `연결 보완`, `신규 adapter 필요`, `자료만 존재`, `미확인`, `사용 부적합`을 구분한다.
3. 첨부의 서비스명·포털 ID·URL을 공식 원문에서 재검증한다. 변경·폐기·로그인·활용신청·유료 조건을 따로 기록한다.
4. 결과는 `docs/Research/GameData/life-domains-r1/`의 `README.md`, `evidence.r1.json`, `manifest.r1.json` 세 파일로 제한한다. 원자료 표본은 `artifacts/local/game-data-research/life-domains-r1/`에 두고 정식 문서와 분리한다.

완료 기준은 여덟 영역의 후보 수가 채워지는 것이 아니라, 각 후보의 현재 상태와 다음 기술 경로가 근거와 함께 판정되는 것이다.

### 1단계 — 영역 관계와 메타데이터 보완

1. 한국어 표시명과 안정 영역 코드를 한 곳에서 관리한다.
2. 기존 `DataDomain`을 무조건 바꾸지 않고, 현재 세부 도메인과 여덟 영역의 다대다 관계가 필요한지 검증한다.
3. 필요하다면 기존 카탈로그를 읽는 호환 관계만 추가한다. 공급원 정의를 복제한 두 번째 카탈로그는 만들지 않는다.
4. 20개 조사 항목 중 반복되는 계약 결손만 선택 필드로 보완하고 기존 consumer의 JSON 호환성을 시험한다.

### 2단계 — 첫 구현 묶음: 우선 세 영역

새 공급원을 먼저 늘리지 않고 현재 코드가 가장 많이 준비된 세 영역으로 수직 slice를 만든다.

#### A. 먹고사는 일

- 기존 KAMIS 또는 aT 가격 경로 한 개를 기준 표본으로 선택한다.
- 원 포장단위·비교단위·품목 대응·관측일을 보존한다.
- 기존 실제 저장 자료가 있으면 재수집하지 않고 독립 재조회와 영역 Projection부터 검증한다.

#### B. 물건과 장사

- 전통시장 또는 지방행정 인허가 사업장 중 기존 client/import 경로가 더 완성된 하나를 선택한다.
- 시장·사업장 존재와 현재 영업·거래 가능·추천을 구분한다.
- 가격 자료가 없는 시설 자료를 가격 0으로 채우지 않는다.

#### C. 집, 길, 터전

- 법정동/행정동 원장과 기존 VWorld 건물 또는 동네 공간 원본 중 하나를 선택한다.
- 원 좌표계, 내부 `RegionStableId`, 변환 판본과 공간 정밀도를 검증한다.
- 건물/도로 데이터가 있다는 이유로 통행·지지·배치 가능을 확정하지 않는다.

각 slice는 `소량 실제 수신 또는 기존 실제 원문 재사용 → RawSnapshot → 정규화 → 로컬 MySQL 비공개/검토보류 저장 → 새 조회 문맥 재조회 → 같은 입력 중복 방지`까지 닫는다. 자격이나 이용조건이 막히면 해당 공급원만 차단하고 나머지 slice를 계속한다.

### 3단계 — 나머지 다섯 영역의 공급원 준비

우선 세 영역에서 메타데이터·오류·저장·조회 계약이 안정된 뒤, 문화 → 교육 → 방문 → 안전 → 비상 순서는 **작업 대기열 제안**으로만 사용한다. 실제 순서는 공식 자료 접근성, 권리, 기존 adapter 재사용률과 제품 소비자 준비도에 따라 정한다.

각 영역에서 최고 우선 공급원 한 건만 먼저 검증한다. 한 공급원의 실패 때문에 같은 영역의 공개 파일 대안이나 다른 영역을 막지 않는다. 로그인·활용신청·API 키·본인인증·유료 권한이 실제 필요하면 [D404 운영 기준](../../../../Architecture/게임자료조사전문운영.md#로그인활용신청이-필요할-때-먼저-보고한다-d404)에 따라 사이트·자료·이유·사용자 조치·공개 대안·계속 가능한 범위를 먼저 반환한다.

### 4단계 — 읽기 Projection과 Reality Context 연결

1. 영역별 화면/API 소비가 실제로 있을 때만 읽기 Projection을 추가한다.
2. Projection은 출처·기준시각·지역·단위·품질·한계·검토 상태를 빠뜨리지 않는다.
3. 공급자 호출이나 원장 쓰기는 공개 조회와 Simulation session/Tick/Unity에서 수행하지 않는다.
4. Reality Context 입력은 승인된 정규화 자료의 동결 사본이며, `Available/Unavailable/Stale/Incomplete`를 보존한다.
5. 자료가 없거나 오래됐을 때 합성 Scenario 값으로 성공을 위장하지 않는다.
6. 게임 가격·수확량·범죄·관광객·재난·방위 효과는 별도 기획 revision과 Simulation 시험이 승인되기 전까지 만들지 않는다.

## 7. 제안하는 코드·시험 소유 경로

실제 수정은 0단계 재고 결과 후 아래 범위에서 필요한 파일만 고른다.

| 책임 | 우선 소유 경로 |
| --- | --- |
| 공통 API metadata·source 계약 | `Ssalddel.Contracts/Common/PublicData/`, `Ssalddel/Services/External/PublicData/PublicDataApiMetadataCatalog.cs`, `Foundation/ExternalDataSourceCatalog.cs` |
| 공급원별 등록·수집·정규화 | `Ssalddel/Services/External/PublicData/Korea/`의 공급원별 파일; 여덟 영역을 이유로 한 거대 공통 client는 만들지 않음 |
| 원장·MySQL | `Ssalddel.Domain/PublicData/`, `Ssalddel.Infrastructure/Persistence/PublicData/`; 기존 schema로 부족한 실제 반복 결손만 최소 확장 |
| 관리자 수집 진입점 | 기존 Admin public-data/content 경로를 우선 재사용; 공개 조회에서 수집을 시작하지 않음 |
| Simulation 소비 | `Ssalddel.Simulation.Application/SimulationRealityContextService.cs`와 관련 계약; 공급자 참조 금지 |
| 서버 시험 | `Ssalddel.Tests/Services/External/PublicData/`와 공급원 기존 시험 폴더 |
| Simulation 경계 시험 | `Ssalddel.Simulation.Tests/SimulationSharedPublicDataTests.cs`, `SimulationRealityContextTests.cs` |
| 조사 근거 | `docs/Research/GameData/life-domains-r1/` |
| 구현 결과 보고 | `docs/Reports/생활여덟영역-공공데이터-구현결과-YYYY-MM-DD.md` |

공용 DTO·DbContext·DI·마이그레이션은 개발이 단일 소유한다. 자료 조사 담당은 공식 출처와 필드 의미·권리·표본을, 개발은 코드·DB·시험·보안·통합을, 기획은 게임 해석과 채택 여부를 맡는다.

## 8. 시험 계획

### 계약·메타데이터

- 여덟 안정 코드와 한국어 표시명이 중복 없이 결정적으로 조회된다.
- 한 공급원의 주/보조 영역 관계가 중복 행을 만들지 않는다.
- 기존 `PublicDataApiMetadataItem`과 `ExternalDataSourceDefinition` consumer가 새 선택 필드가 없어도 동작한다.
- `ReferenceOnly`, `NeedsServiceKey`, `Connected`, 운영 중단/폐기/미확인을 서로 바꾸지 않는다.

### 수집·정규화

- 키 미설정은 외부 호출 전에 `MissingCredential`로 끝나며 sample fallback이 없다.
- timeout, network, HTTP, provider 오류, parsing, schema mismatch를 구분한다.
- 문서와 실제 필드·형식·좌표계가 다르면 해당 payload를 거부한다.
- 결측·마스킹·비공개 값은 `0`으로 변환하지 않는다.
- 원문 SHA-256이 같으면 RawSnapshot을 복제하지 않고, 의미 있는 새 기준시점은 기존 관측을 덮지 않는다.
- 같은 `RecordKey` 입력은 멱등이고 변경 revision은 이력을 추적할 수 있다.

### 저장·조회

- 합성 Fixture 시험과 실제 공급원 수신을 별도 결과로 표시한다.
- 실제 MySQL 저장은 정확 컨테이너·DB·schema를 확인한 로컬 개발 범위에서만 수행한다.
- 저장 뒤 별도 DbContext/연결로 source·dataset·hash·판본·건수·검토 상태를 재조회한다.
- 재배포 미확인 자료는 기본 비공개/검토보류이며 공개 Projection에 원문을 노출하지 않는다.

### 권위 경계

- 공개 조회는 읽기 전용이며 Collector나 `SaveChanges`를 호출하지 않는다.
- Simulation 공유 자료 경계는 쓰기를 거부한다.
- 세션 동결 뒤 같은 session의 Reality Context가 공급자 최신 응답 때문에 바뀌지 않는다.
- `Unavailable/Stale/Incomplete` 자료가 게임 규칙이나 Scenario 성공값으로 대체되지 않는다.
- Unity/화면 표시 성공을 수집·DB·Simulation 규칙 승인으로 계산하지 않는다.

## 9. 단계별 완료 조건

| 관문 | 완료 조건 | 아직 완료가 아닌 것 |
| --- | --- | --- |
| G0 재고 | 8영역 후보와 기존 코드가 근거·상태로 대응됨 | API 사용 가능, 실제 호출 |
| G1 metadata | 20개 항목의 확인·미확인·차단이 저장소에서 조회됨 | 활용신청 승인, adapter 동작 |
| G2 공급원 준비 | 정확 endpoint·자격·parser·normalizer 단위시험 통과 | 실제 수신, MySQL 저장 |
| G3 실제 축적 | 작은 실제 자료의 수신·비공개 저장·독립 재조회·중복 방지 통과 | 공개 제공, 게임 반영 |
| G4 영역 조회 | 출처와 한계를 포함한 읽기 Projection 통과 | Reality Context 채택 |
| G5 Reality 후보 | 승인 자료가 세션 동결 사본으로 제공됨 | 게임 효과·균형 승인 |
| G6 게임 적용 | 별도 기획·규칙·작업 명세·시험이 통과 | 운영 배포·Unity E5/E7 자동 완료 |

## 10. 첫 실행 묶음의 반환 형식

첫 구현 반환은 다음 네 결과를 분리한다.

1. **조사:** 영역별 후보, 공식 서비스/ID/URL, 접근·권리·호출 제한·신선도, 확인일과 미확인.
2. **코드:** 재사용한 카탈로그·Collector·Normalizer·원장·Projection, 실제 변경 파일.
3. **검증:** 합성 시험, 실제 공급원 수신, 실제 MySQL 저장, 독립 재조회, 중복 방지, Fast/Task를 각각 표시.
4. **미완료:** 사용자 조치가 필요한 키/신청, 권리·좌표계·필드 의미, 아직 없는 adapter/소비자, Reality/Simulation 미채택.

한 영역이 닫히면 그 부분을 먼저 반환한다. 여덟 영역 전체를 기다리거나, 목록만 완성한 것을 전체 구현으로 보고하지 않는다.

## 11. 이번 계획서에서 하지 않은 일

- 첨부의 API 명칭·포털 ID·URL·운영 상태를 인터넷이나 실제 응답으로 검증하지 않았다.
- 신규 API를 호출하거나 로그인·활용신청·키 발급·약관 동의를 하지 않았다.
- Collector·Normalizer·DB schema·관리 API·Projection·Simulation 규칙을 구현하지 않았다.
- 로컬 Docker MySQL, 운영 DB, Unity, Scene, Play Mode, Game View를 실행하지 않았다.
- 새 WI/H/AreaSet/E 단계, 가격·수확·치안·관광·재난·방위 게임 규칙을 만들지 않았다.
- 원자료 대량 저장, 외부 게시, 자동화, commit, push를 하지 않았다.

## 12. 검토 후 권장 착수점

이 계획이 승인되면 G0 재고를 먼저 수행하고, 첫 실제 slice는 기존 원문·보관·시험 계보가 가장 분명한 `먹고사는 일`의 KAMIS 또는 aT 경로로 정한다. 동시에 `물건과 장사`의 전통시장/인허가 사업장과 `집, 길, 터전`의 법정동/VWorld 경로는 파일 기반 재고까지만 병행한다. 세 경로가 동일 공통 계약 결손을 드러낼 때만 공용 DTO를 확장한다.

다음 기획 결정은 “여덟 영역 이름을 채택할지”가 아니라, **첫 실제 slice를 KAMIS 재사용으로 시작할지와 그 slice가 제공할 첫 읽기 Projection을 무엇으로 할지**다.
