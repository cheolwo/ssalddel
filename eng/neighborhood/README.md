# 동네 지도·경로 사전 검사

실제 GIS 전처리 결과를 받기 위한 **후보 검증기**다. 현재 제공하는 표본은 합성 좌표이며 면목동 지도가 아니다. 서버 실행·배차 확정·Unity 배치를 하지 않는다. [기획과 남은 작업](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/implementation.md).

## 중랑구 색인·면목동 배치 참고 지도

수집·재조회가 끝난 비공개 자료를 개별 행 그대로 Git에 복제하지 않고, 중랑구 상위 색인과 면목동 500m 타일별 집계로 정리한다. 기존 도형 동네는 별도 가상 시나리오로 유지한다.

```powershell
$env:SSALDDEL_UNITY_ROOT = '<Unity 프로젝트 루트>'
pwsh -NoProfile -File eng/world-seedbeds/manage-myeonmok-reference-maps.ps1 -Mode Check
pwsh -NoProfile -File eng/tests/myeonmok-reference-maps.ps1
```

- [자료 판본 대장](../world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json)
- [Graph Map](../world-seedbeds/graph-maps/jungnang-myeonmok-reference.v1.json)
- [배치 Map](../world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v1.json)
- [결과와 한계](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/regional-reference-map-result.r1.md)

이 검사는 DB·외부 API·Unity Scene을 변경하지 않는다. 상가 좌표는 측지 기준이 명시적으로 확인되기 전까지 `ReviewRequired`, 주소 연결은 입주가 확인되기 전까지 `Candidate`다.

## 실행

저장소 루트의 PowerShell에서:

```powershell
$지도입력 = 'eng/neighborhood/fixtures/synthetic-block.v1.json'
$검토한해시 = (Get-FileHash -LiteralPath $지도입력 -Algorithm SHA256).Hash
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $지도입력 $검토한해시 depot restaurant-stop Vehicle
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $지도입력 $검토한해시 restaurant-stop restaurant-door Pedestrian
```

차량 후보 200m, 도보 후보 5m를 반환한다. 종료 코드 0은 후보 탐색 성공, 2는 경로 없음, 1은 입력/판본 오류, 64는 명령 형식 오류다. 성공이어도 `runtimeAuthorized`는 항상 `false`다. 실제 파일은 검토할 때 기록한 해시를 다음 실행에도 사용해야 변경을 발견할 수 있다. 실행할 때마다 다시 계산한 해시만 넣으면 변경 승인 검사가 되지 않는다.

## 입력 계약 `neighborhood-geography.v1`

### 작은 배달 동네 표본

`fixtures/synthetic-delivery-block.v1.json`은 [승인된 합성 공간](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/synthetic-spatial-study.r1.md)의 건물3·노드9·도로8을 담는다. 실제 면목동이나 Unity Scene이 아니다. 기존 큰 단위 시험 표본은 유지한다.

```powershell
$배달지도 = 'eng/neighborhood/fixtures/synthetic-delivery-block.v1.json'
$고정해시 = '085252202EA010D7C40469A549AF1DB794664521E3CE27DE817C3DE6886BB8CE'
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 restaurant-stop residence-a-stop Vehicle
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 residence-a-stop residence-a-door Pedestrian
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 residence-a-stop depot Vehicle
```

각각 차량40m·도보4m·복귀50m 후보다. 새 `동네이동진행Engine`은 경로 지문에 묶인 불변 진행 후보를 차량5m/도보1m씩 계산한다. 차단 중 위치를 유지하며 중복 Tick의 내용 충돌·다른 경로 복원·유효하지 않은 거리/시간은 거부한다. `관찰시간누적기`는 재생 중 1초당 최대 한 Tick 요청 여부만 반환한다. 두 모듈 모두 기존 Runtime 명령·주문·기사·Unity에 아직 연결하지 않았다. 지원 모듈 복원은 실제 Session Save/Replay 증거가 아니다. [작업 범위](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/movement-support.r1.md).

### 형식과 제한

GeoJSON의 도형 표기와 닮았지만 **RFC 7946 GeoJSON이 아닌 전처리 후 전용 JSON**이다. SHP/NGI를 이 도구에 직접 넣거나 경위도에 미터 단위만 붙여 사용하지 않는다. 누락된 재투영·클리핑·원본 권리 검토를 자동으로 보충하지 않는다.

- `provenance`: 출처/데이터셋 ID, 원본 판본·UTC 기준 시각·원본 해시, 원본 CRS, 전처리 CRS, 정규화·권리 근거 참조, 권리 검토 상태, `PublicData` 또는 `SyntheticFixture`.
- 실제 자료의 전처리 좌표계는 현재 `EPSG:5186`만 지원한다. 원본 CRS는 `.prj` 등에서 별도로 확인한다. 합성 표본은 `LOCAL:SYNTHETIC`만 사용한다. 이 검사기는 전처리 CRS 선언의 정확성이나 근거 문서의 진위를 인증하지 않는다.
- `coordinates`: `unit=m`, `axisOrder=EastingNorthing`, 투영 좌표의 `origin=[E,N]`, `bounds=[minE,minN,maxE,maxN]`. 지역 좌표는 `X=E-originE`, `Z=N-originN`. 높이는 만들지 않는다. 범위 밖 점은 자르거나 옮기지 않고 거부한다. 이미 잘린 면적과 연결은 전처리/공간 검토의 책임이다.
- `features`: 전체에서 고유한 `id`, `kind`, `geometry`, `properties`. 노드는 `Point`, 도로는 `LineString`, 건물은 단일 외곽 고리 `Polygon`이다. 내곽/복합 도형은 현재 거부한다. 내곽을 채우거나 건물을 연결하는 자동 단순화는 금지한다.
- 노드 역할은 `Junction`, `VehicleStop`, `Entrance`. `Entrance`만 실제 포함된 건물 ID를 명시한다. 이것은 논리 연결이며 실제 접근 가능성을 입증하지 않는다.
- 도로는 `fromNodeId/toNodeId`, `direction=Unknown|Forward|Both`, `modes=[Vehicle|Pedestrian]`, `accessReview=Unknown|Reviewed|Blocked`, 검토 근거를 담는다. Reviewed만 근거가 필수다. 방향 Unknown 또는 검토 Unknown/Blocked는 탐색에서 제외한다. 차량은 Entrance에 연결할 수 없다.
- 도로 양끝과 노드 좌표는 정확히 같아야 한다. 교차하거나 가까운 선을 자동 연결하지 않는다. 알려지지 않은 건물 높이·도로 폭은 `null`로 유지한다.
- 입력 4MiB, 최대 5,000 도형·100,000 점·도형당 512점, 범위 가로/세로 각각 최대 1,000m. 대량/전국 데이터 수집기가 아니다.

정규화 **파일 바이트 해시**는 호출자가 제공한 값과 실제 대조하고 경로 판본으로 사용한다. 원본 해시는 형식·보존만 검사한다. 실제 원본 파일과의 대조, DB 저장/재조회·중복 방지, 라이선스 근거 검토는 기존 수집 경계에서 따로 수행해야 한다. 합성 fixture의 `AAAA…` 원본 해시는 시험용 토큰이며 원본 확보 근거가 아니다.

## 다음 연결

원본/권리 확인 → 비공개 원장 저장·재조회 → 실제 CRS 재투영/경계 자르기 → 도형 입력 검사 → 출입구/정차/도로 방향·지형 연구 승인 → 단일 WI별 Core 도달/픽업/수령/귀환 상태·Save/Replay → 기존 Unity 경로 표현. 후보 길찾기는 2D 길이 기반이며 경사·차폭·실제 교통·안전 통행을 인증하지 않는다. 개별 Graph Map 원본·기존 H·Scene은 변경하지 않는다.

## 비공개 원본 등록

`Ssalddel.NeighborhoodSourceImport`는 위 지도 후보 검사기와 다르다. [확보 기록](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/source-acquisition.md)의 **승인된 두 원본 파일과 해시, `hongdal-mysql-1 / hongdal_dev`**만 다룬다. 다른 파일·판본·DB에는 재사용하지 않는다. `apply`는 실제 DB 쓰기이므로 대상과 범위의 사용자 승인을 확인한 뒤 실행한다.

```powershell
$저장소경로 = 'C:\Users\user\source\repos\Hongdal'
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- preview $저장소경로
# 원본 두 파일·대상 DB의 저장 승인이 있는 경우에만:
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- apply $저장소경로
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- verify $저장소경로
```

- `preview`/`verify`는 읽기 전용이다. `apply`는 원본 두 개의 계보를 한 트랜잭션으로 등록한다. 같은 파일 재입력은 기존 기록의 마지막 확인 시각만 갱신하며 새 행을 만들지 않는다.
- 파일 잠금·크기·SHA-256·컨테이너 소속·포트·DB 이름을 대조한다. 지정 컨테이너의 비-root 연결값은 메모리에서만 사용하고 출력하지 않는다. 새 DB·migration·기본 서버 설정 변경은 하지 않는다.
- 원본 바이트는 Git 제외 로컬 폴더, 계보는 DB에 저장한다. `Partial / NeighborhoodSourceReviewPending`을 유지하며 정규화·건축물 원장 연결이 생겼으면 검증을 실패시킨다. 이 도구는 SHP/DBF 변환기나 건물 마스터 적재기가 아니다.
- 종료 코드 0은 해당 모드 검사 성공, 1은 차단/실패, 64는 명령 형식 오류다. 실패 JSON의 `committed`를 확인한다. 저장 후 독립 조회에서 실패했다면 `committed=true`일 수 있으므로 미저장으로 단정하지 않는다. `runtimeAuthorized`는 항상 `false`다.
