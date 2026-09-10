# 농업 생산구획

@spatial-knowledge h1-stock:farm-production
@hierarchy H1
@state ApprovedReference
@wi WI-FARM-01
@wi WI-FARM-02
@wi WI-FARM-03
@wi WI-FARM-04
@role FarmProductionPlot
@capability Spatial.CargoAccessible
@capability Spatial.CropCareWorkArea
@capability Spatial.CropProduction
@capability Spatial.HarvestWorkArea
@capability Spatial.SowingWorkArea
@capability Spatial.TillingWorkArea
@capability Spatial.WaterAccessible
@capability Spatial.WorkerAccessible
@connector cargo-handoff
@grammar farm:감자밭 두렁
@grammar farm:혼합 작물밭

## 존재 이유

기존 상향식 재고 v1에서 이관한 농업 생산구획 설계 지식이다.

## 설계 상태

- 재고 상태: `ApprovedReference`
- 공간 계층: `H1`
- 실제 지역 권위: 없음

## 미해결

## 플레이어 설치 기획

- 기본 표시 단위는 `경작 구획 H1`이다.
- 기본 크기는 `2.5×2.5m`다.
- 플레이어는 청사진 또는 배치 모드에서 경작 구획을 선택·미리보기·설치 요청할 수 있다.
- 미리보기는 World를 바꾸지 않는 `DraftGhost`다.
- 설치 확정은 부지·겹침·통행·권한·자원·상위 배치 규칙 검사를 통과해야 한다.
- 여러 경작 구획 H1과 통행·작업 가장자리를 조합해 논밭 H2를 구성한다.

이 기획은 플레이어 설치 가능성을 정의하지만 실제 좌표, Prefab, Collider·Bounds, 입력 처리나 E5를 성립시키지 않는다.


이 문서는 상향식 공간 설계 지식이며 실제 좌표·AreaSet·LandscapeGraph·Unity 자산 권위를 만들지 않는다.
