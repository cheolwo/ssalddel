# 중랑구 색인·면목동 배치 참고 지도 결과 r1

- 기획: `PLAN-SYSTEM-MYEONMOK-OBSERVER`
- 판본: `jungnang-myeonmok-reference.r1`
- 상태: `ReferenceMapPrepared / PlacementReviewOnly / SceneReady=false`
- 정본: [자료 판본 대장](../../../../../eng/world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json) → [Graph Map](../../../../../eng/world-seedbeds/graph-maps/jungnang-myeonmok-reference.v1.json) / [배치 Map](../../../../../eng/world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v1.json)

## 구성

중랑구를 상위 자료 색인으로, 면목동을 사가정역 기준 500m 타일 36개의 상세 후보 구역으로 정리했다. 기존 도형 동네는 삭제하거나 현실 공간으로 바꾸지 않고 `ScenarioOverlayOf` 관계로 연결했다.

Graph Map은 지역·기준점·자료층·타일·가상 시나리오의 관계만 가진다. 배치 Map은 타일 위치와 자료 종류별 집계를 가진다. 개별 주소와 사업체 7,543행은 공개 Git 산출물에 복제하지 않고 기존 비공개 검토 사본에 유지한다.

| 자료층 | 배치 참고 결과 | 경계 |
| --- | ---: | --- |
| 면목동 상가 위치 후보 | 5,411 | 경위도 측지 기준 미확정, `ReviewRequired` |
| 사가정 OSM 건물 외곽선 | 602 | 실제 측량·높이·출입구 아님 |
| 사가정 OSM 도로 선분 | 2,397 | 통행·방향·내비게이션 권위 아님 |
| 주소 기반 건물 연결 | 801행 / 204건물 | 실제 입주·현재 영업 미확정 |
| 주소 미연결 | 6,742행 | 최근접 건물에 강제로 연결하지 않음 |
| 중랑구 공공시설 색인 | 146 | 현행 1km 안 후보 10, 밖 100, 좌표 없음 36 |

## 생성과 검사

```powershell
$env:SSALDDEL_UNITY_ROOT = '<Unity 프로젝트 루트>'
pwsh -NoProfile -File eng/world-seedbeds/manage-myeonmok-reference-maps.ps1 -Mode Check
pwsh -NoProfile -File eng/tests/myeonmok-reference-maps.ps1
```

생성기는 동결된 비공개 사본과 OSM 자원의 hash를 먼저 대조한다. 각 객체는 사가정역 ENU 기준과 기존 World offset을 보존하고, 중심점이 속한 대표 타일 하나에만 집계한다. 같은 입력에서는 Graph Map과 배치 Map의 정규화 결과가 같아야 한다.

정상 검사와 중복 노드, 누락 관계, 시나리오 경계 누락, 타일 누락·크기 오류, 집계 변경, 미배치 합계 변경, 운영 권위 혼합의 오류 주입 8종을 통과했다.

기존 `hongdal-mysql-1 / hongdal_dev`도 읽기 전용으로 다시 확인했다. 중랑구 공간 신규층 134행, 면목동 주택·병의원 362행, 상가·공장 5,537행이 동결 입력과 일치했으며 세 조회 모두 `databaseWriteAttempted=false / committed=false`였다.

## 남은 관문

- 면목동 행정경계 도형을 확보하기 전 36개 타일은 상가 관측 범위를 덮는 상세 후보 범위다.
- 주소 연결은 단일 건물 후보일 뿐 입주·출입구·동·호수와 현재 영업을 확정하지 않는다.
- Unity 기본 화면은 기존 도형 동네를 유지한다. 이 자료는 `PlacementReviewOnly`이며 실제 Scene 적용·Play Mode·Game View 검증은 수행하지 않았다.
- DB 쓰기, 외부 재수집, 업무 상태 변경, Scene 저장, commit과 push는 수행하지 않았다.
