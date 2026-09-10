# 음식 배달 표본 E1~E7 영향과 E4 인계

- 후속 조사: [기존 업무 재사용과 구현 관문](workflow-reuse-audit.md). 주문·배차 권위 변경은 아래 표현 지원 범위와 구별한다.

- 승인 기획: 같은 폴더 README, `food-delivery-neighborhood.r1`.
- 구현 범위: 순수 위치/경로 검증·음식배달 상태 읽기 투영·표본 H 구성·자산 후보 조사. 기존 게임 상태를 변경하지 않는 표현 지원 모듈이다.
- 쓰기 경로: `Ssalddel.Unity/Presentation/음식배달관찰*.cs`, `Ssalddel.Unity.Tests/음식배달관찰Tests.cs`, 이 기획·기획 목차·현재 작업.
- 논리 재사용: `Simulation음식배달.cs`와 기존 음식배달 시험. 배달/수령 API·저장 ID·해시는 변경하지 않는다.
- 관찰 계약: 입력은 음식 주문 상태 사본과 확정된 관찰 경로/진행률, 출력은 상태 설명과 기사/차량 위치 후보다. 상태 변화를 추론해 Confirm하지 않는다.

| E | 논리 책임 | 표현 책임 |
| --- | --- | --- |
| E1 | 기존 주문/별도 수령 경계 유지 | 도로·정차·인계를 분리하고 움직임을 업무 완료로 취급하지 않음 |
| E2 | 기존 Core 재사용, 새 배정/복귀 권위는 미연결 | 엔진 독립 경로 계산·상태 투영 |
| E3 | 기존 음식배달 회귀 | 꺾인 경로·양 주택·단절·잘못된 입력·동일 입력 재현 시험 |
| E4 | 기사/오토바이 주체·명령·진행/복귀 저장 계약 결속 필요 | 아래 자산과 H 후보·상대 배치 준비, 실제 크기/통행/시야 검토 필요 |
| E5 | 경로 차단을 실제 작업에 적용하고 명령/상태 계보 결속 | 기준 Scene, Renderer·Collider·Bounds·위치 결속 미실행 |
| E6 | 중도 취소·실패·복구·복귀 검증 | 회전·탑승·정차·인계·동작 전이 정제 미실행 |
| E7 | 한 주문 성공/실패/재개와 다음 선택 | 실제 입력·Game View·관찰 판독 미실행 |

## Presentation E4 후보

별도 Unity 체크아웃의 `Assets/Synty`에서 아래 Prefab 파일 존재를 확인했다. 후보이지 형태·스케일·재질·사용자 시각 승인이 아니다. 경로는 그 저장소 기준이다.

| 용도/VisualKey 후보 | 원본 후보 | 상태·대체 |
| --- | --- | --- |
| 차도 / `delivery.road` | `PolygonCity/Prefabs/Environments/SM_Env_Road_01.prefab` | 직선 후보. 아스팔트 재질·회전 조합·유효 차폭은 시각 검토 필요 |
| 교차 / `delivery.crossing` | `PolygonCity/Prefabs/Environments/SM_Env_Road_Crossing_01.prefab` | 연결 후보. 이름만으로 회전 반경/통행 허용을 확정하지 않음 |
| 음식점 외관 / `delivery.restaurant` | `PolygonCity/Prefabs/Buildings/SM_Bld_Shop_01.prefab` | 가상 음식점 간판·인계점 조립 필요. 내부 조리 애니메이션 제외 |
| 기사 / `delivery.courier` | `PolygonCity/Prefabs/Characters/Character_Male_Jacket.prefab` | 외형 후보. Rig·탑승 자세·인계 Clip 미검증 |
| 주택 / `delivery.residence` | `PolygonStarter/Prefabs/SM_PolygonApocalypse_Bld_House_01.prefab` | 외형 후보. 동네 적합성·현관 접근 검토 필요 |
| 오토바이 / `delivery.motorcycle` | 미확보 | Prefab 이름 Motorbike/Motorcycle/Scooter 검색에서 미발견. FBX·다른 이름·구성 대장 추가 조사 필요. 도보로 임의 변경하지 않음 |

정차·인계 표현의 대체는 진행 표시/상태 카드까지만 허용한다. 오토바이 후보 결손을 사람형 직선 왕복으로 숨기지 않는다. Blender 가공/구매는 이번에 수행하지 않는다.

### 후보 파일 fingerprint

2026-09-06 SHA-256. Prefab 파일만의 fingerprint이며 종속 메시·재질·Clip까지 동결한 자산 묶음은 아니다.

| 후보 | SHA-256 |
| --- | --- |
| 도로 | `3160A53EF7B7FBA746EC7B25F466E8A9A6FA9FD0C4B792A40C3CD1BCC34CDBB7` |
| 교차 | `B7C22E2E85834DEE6EBB490578F8CA15D696C0D8B2BD05159B3F718069A38F2A` |
| 음식점 외관 | `D050ED586C2D08173C70E2830B5F04214AF75FC5EBA8852F8DF0D6F40AF31E64` |
| 기사 | `3A5AEA9067DCA7AB784306B2C5C82D9C2CC8251DC20F9F37305E447452540046` |
| 주택 | `BA960136217407CC07C29A10BB4A270F851E99EEF793BDD2AEA3F90B122F6815` |

Prefab/FBX 이름 검색을 `Motor`, `Scooter`, `Bike`, `Moped`로 넓혔으나 BikeStand 외 차량 후보는 발견하지 못했다. 이는 파일명 검색 결과이며 메시 전체를 시각 조사해 오토바이가 없음을 확정한 것은 아니다.

## 이번 검증

- 순수 표현 라이브러리 신규 시험 13/13, .NET Unity 라이브러리 전체 시험 710/710 통과. Unity Editor EditMode 실행이 아니다.
- 기존 음식배달 Simulation 시험 6/6 통과. 기존 저장재생·중복 방지·별도 수령 확인 경계가 유지됨을 확인했다.
- 새 경로는 진행률 입력으로 재현하는 표현 함수다. 실제 기사 상태·배정·진행률의 영속화와 기존 주문에 대한 경로 차단 적용은 미구현이다.

## 통합 차단과 다음 인계

현재 음식배달 Core는 시간 경과로 픽업/전달을 기록하며 경로 도달 여부를 검사하지 않는다. 경로 지원 코드의 차단만으로 기존 주문 진행이 차단됐다고 주장하지 않는다. 기존 음식배달 WI/작업 명세와 기사·차량 준비 주체를 재결속하고, 경로/업무 결과가 같은 판본을 소비하도록 하기 전 실제 Scene 자동 운행을 켜지 않는다. 자산 후보와 테스트만으로 E4 완료나 E5 승격을 선언하지 않는다.
