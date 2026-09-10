# Blender·Synty 프로젝트 소유 파생 자산 작업 뼈대 — 2026-09-03

## 결과

기존 Unity 저장소의 `ArtSource/Blender` 환경을 재사용해 `공급사 원본 읽기 전용 고정 → 프로젝트 전용 복사본 → 작은 변경 → Blender 검증 → 별도 FBX → Unity Import 후보` 흐름의 최소 뼈대를 구현했다.

이번 작업은 실제 Synty 파일을 열거나 수정하지 않았다. 공급사 파일 없는 저다각형 대체 도형으로 독립 메시 복사, 원본 불변, 작은 손상 형태 변경, `.blend` 저장·재열기와 FBX 내보내기까지만 실행했다. 따라서 실제 Synty 외형 적합성, 구매 권리, Rig·Avatar·재질, Unity Import, Prefab, Scene, Play Mode, Game View와 Evidence 승격은 미검증이다.

## 추가한 뼈대

- `ArtSource/Blender/workflows/synty-project-owned-variant.template.json`: 원본 경로·GUID·SHA-256·권리 근거, PLAN/WI/H/VisualKey 계보, 작은 변경 예산, 출력 격리와 수용 기준을 한 작업 단위로 기록한다.
- `ArtSource/Blender/tools/validate_variant_manifest.py`: Synty 원본 덮어쓰기, 공급사 폴더 출력, 외부 업로드, 자동 실행, 허용되지 않은 대량 변경과 계보 누락을 차단한다.
- `ArtSource/Blender/validation/variant_manifest_contract_tests.py`: 정상 명세와 원본 출력·외부 업로드·원본 변경·대량 자동 변경·계보 누락 거부를 7개 집중 사례로 확인한다.
- `ArtSource/Blender/validation/variant_pipeline_smoke.py` 및 실행 스크립트: Blender 5.2.1 LTS에서 공급사 없는 표본의 독립 복사·변형·저장·재열기·FBX 생성을 검증한다.
- `ArtSource/Blender/workflows/planned-variants.r1.json`: 기존 기획의 한스 손상 농가주택, 절기 주민 복장, Nature 벌목 접촉 보정을 `PlanningOnly` 후보로 연결한다. 정확 Synty 원본은 추측하지 않는다.

## 실행 결과

- Blender 명세 계약 집중 검사: `7/7` 통과.
- Blender 파생 자산 표본 왕복: `BLENDER_VARIANT_PIPELINE_SMOKE_PASS`.
- 최종 결과: `C:/Users/user/ssalddel/artifacts/blender-variant-pipeline/20260903-221207/result.json`.
- Blender: `5.2.1 LTS`.
- 실패 이력: 첫 표본 실행은 Blender 5.2의 `bound_box` 자료형을 행렬과 바로 곱해 중단됐다. `mathutils.Vector`로 명시 변환한 뒤 새 출력 폴더에서 통과했으며 실패 폴더를 성공 증거로 사용하지 않는다.

## 다음 실제 자산 관문

첫 실자산 작업은 후보 하나만 고르고 다음을 모두 결속한 뒤 시작한다.

1. 승인된 기획 판본과 대상 WI·H·VisualKey.
2. 정확 Synty 원본 경로·`.meta` GUID·SHA-256.
3. 구매 채널·적용 약관·팀 사용 근거.
4. Unity 조립으로 부족한 플레이어 판독 차이와 `Subtle` 변경 목록.
5. 프로젝트 소유 `.blend`·FBX·Unity 후보의 전용 경로.
6. Blender 재열기·FBX 왕복 뒤 Unity Import·실제 Game View 수용 기준.

후보 목록이나 표본 성공만으로 실제 가공을 자동 시작하지 않으며, Synty 원본과 현재 dirty Unity Scene을 변경하지 않는다.

## 후속 실제 표본

사용자 승인 뒤 보편 `농장 생활 주택 H1`의 첫 실제 Synty 파생형을 별도 작업으로 제작했다. 결과와 증거 상한은 [H1 생활주택 Blender 손상 파생형](H1생활주택-Blender손상파생형-2026-09-03.md)에서 확인한다. 이 후속은 초기 대체 도형 smoke 결과를 소급해 실제 자산 증거로 바꾸지 않는다.
