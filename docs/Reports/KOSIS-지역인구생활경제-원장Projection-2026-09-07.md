# KOSIS 지역 인구·생활경제 원장·Projection 구현

## 결과

- 기존 `PublicDataApiMetadataCatalog → ExternalDataSourceCatalog → Collector → RawSnapshot → Normalizer → 외부데이터정규화Record` 흐름에 `kosis-regional-statistics`를 추가했다.
- KOSIS의 현재 HTTPS 통계자료 조회 경로 `/openapi/Param/statisticsParameterData.do`를 사용하며 원천은 기본 비활성, API 키 필수다.
- 시도·시군구별 등록인구, 세대, 3개 연령대, 사업체, 종사자, 고용률을 기존 법정동 지역 원장에 유일하게 대응하는 경우에만 정규화한다.
- 공개 조회 경로는 `GET /api/v1/community/world-map/regional-statistics`다. `countryCode=KR`, `regionLevel=Sido|Sigungu`, `period=latest|YYYY-MM`, `metrics`를 지원한다.

## 원천과 판본

- 공식 개발 안내: <https://kosis.kr/openapi/devGuide/devGuide_0201List.do>
- 사용 통계표: `DT_1B040A3`, `DT_1B040B3`, `DT_1B040M5_1`, `DT_1YL20832`, `DT_1YL15014`, `INH_1DA7014S_03`, `INH_1ES3A02S_03`.
- `newEstPrdCnt=2`, `smblChk=Y`를 사용해 최신 완료기간과 직전기간을 수집한다. 월·연 자료는 원래 시간 정밀도를 유지한다.
- 원문 사본 SHA256, 통계표·항목·분류, KOSIS 지역코드 판본, 기간, 원천 변경일, 수집시각, 단위와 품질 제한을 보존한다.

## 안전 경계

- 결측·마스킹·통계기호는 정규화 거부 건으로 남기며 `0`으로 바꾸지 않는다.
- KOSIS 지역명과 기존 법정동 시도·시군구가 정확히 하나로 대응하지 않으면 거부한다. 외부 코드를 법정동 ID로 추측 변환하지 않는다.
- API 응답에는 개인, 주문, 주소, 사용자, 기사 식별자를 포함하지 않는다.
- 조회는 저장된 정규화 원장을 읽을 뿐 공급자 호출, 원장 쓰기, Simulation 명령·상태 변경을 수행하지 않는다.
- 같은 원문 SHA256은 기존 실행기가 재정규화하지 않으며, 같은 지역·지표·기간·차원은 기존 `RecordKey` 멱등 갱신을 사용한다.

## 검증

- 신규 집중 시험 10/10 통과: 현재 HTTPS 경로와 7개 통계표 요청, 비밀값 비보존, 키 누락 조기 차단, 결측값 거부, 연령대 합산, 서울/중랑구·부산/해운대구 법정동 대응, 조회 필터·공개 필드, 잘못된 국가코드 거부.
- 공공데이터 기반·메타데이터·읽기 전용 지도 회귀를 합친 56/56 통과.
- `Ssalddel.csproj` 빌드 오류 0. 기존 nullable 경고 2건은 이번 변경 밖이다.
- 범위 Fast는 통과했다(`artifacts/local/validation/20260907-232336`). Task의 v3.5 전체 빌드는 통과했고 전체 시험은 4,886/4,893 통과, 7건 실패했다(`artifacts/local/validation/20260907-232407`). 실패는 기존 WebApp route capability 1건, UI CSS 1건, 기존 Controller metadata·이름 5건으로 이번 KOSIS 경로와 겹치지 않는다.
- 로컬 Docker MySQL 8.4는 실행·healthy 상태를 확인했다. 다만 현재 볼륨의 실제 계정 정보가 compose 파일의 개발 기본값과 달라 인증이 거부되어 migration/fixture 적재·재조회는 수행하지 못했다. 컨테이너를 재생성하거나 비밀값을 추출·변경하지 않았다.
- 현재 환경에는 KOSIS 키 설정 이름이 없었다. 실제 공급자 호출은 하지 않았고 `MissingCredential` 조기 차단을 시험으로 확인했다.

## 남은 작업

- 유효한 로컬 MySQL 개발 접속 설정으로 migration 상태 확인, fixture 적재, API 재조회와 원문 계보를 독립 검증한다.
- 사용자 환경에 KOSIS 키가 준비되면 소스별 명시 활성화 후 소량 1회 실수집한다. 키·응답 원문은 보고서에 노출하지 않는다.
- 실제 KOSIS 응답에서 품목명·분류 코드가 바뀌면 자동 추측하지 않고 해당 표만 `PendingHumanReview`에 준하는 거부 결과로 돌려 검토한다.

커밋·푸시·운영 배포·Unity 실행은 수행하지 않았다.
