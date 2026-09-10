# Nature 한스 농장 첫 울타리 수리

## 식별과 근거

- 주제 고유 식별자: `topic:nature-hans-farm-fence-restoration.v1`
- 선택적 PlayableLoop 고유 식별자: `playable-loop:nature-hans-farm-fence-restoration.v1`
- 기획 revision: `nature-hans-farm-fence-restoration.design.r1`
- 원천 기획: `docs/AI/한스농장-첫벌목과울타리수리-기획-2026-09-02.md` (`hans-farm-first-lumber-repair.r27`)
- 기획 승인 근거: 위 원천 기획 문서
- 현재 범위: Logic E5 / Presentation E4 기준선을 문서화한다. 이 문서로 E5·E7을 새로 승격하지 않는다.

## 플레이어 약속과 재미

플레이어는 별도의 부러진 농장 손도끼를 회수하고, 개인 손도끼로 허용된 나무 한 그루를 직접 베어 목재 2개를 얻는다. 목재를 쓰기로 선택하면 첫 사건의 손상 울타리 세 구간이 한 번에 수리되고, 농장 생활을 계속할지 다음 길로 떠날지 다시 선택할 수 있다.

핵심 재미는 퀘스트 강제가 아니라 주변의 손상을 알아차리고 이미 가진 벌목 능력으로 자발적으로 해결했을 때 공간과 관계의 변화를 확인하는 데 있다.

## 반복 폐루프

`농장과 손상 울타리 발견 → 부러진 농장 손도끼 회수 → 개인 손도끼 장착 확인 → 허용 나무 벌목 → 목재 획득 → 울타리 전체 수리 선택 → 변화 확인 → 농장 생활 또는 이동`

이 PlayableLoop는 네 WI의 반복 폐쇄성을 검증하기 위한 선택적 묶음이다. 신규 Goal은 아래 WI 중 하나만 소유하고 각 WI의 직접 결과를 먼저 닫는다.

## 선택·대가·성공·실패·회복

- 선택: 목재를 보유·판매·다른 제작에 쓰거나 울타리 전체 수리에 사용한다.
- 대가: 수리에는 목재 2개와 해당 행동 시간이 필요하다.
- 성공: 손상 울타리 세 구간이 한 번에 수리되고 `HansFarmLifeOrTravelChoiceAvailable`로 돌아간다.
- 실패·중단: 도끼 미장착, 허용되지 않은 나무, 목재 부족, 오래된 Preview이면 권위 상태를 바꾸지 않는다.
- 회복: 필요한 도끼·목재·유효 Preview를 다시 준비한 뒤 재시도한다. 실패만으로 보유 자원이나 관계를 추가 차감하지 않는다.

## WI 단일 책임 후보

| WI | 단일 책임 | Actor 주체 | 직접 대상 주체 |
| --- | --- | --- | --- |
| `WI-NATURE-19` | 부러진 농장 손도끼 회수 | 플레이어 | 농장 손도끼 물품 |
| `WI-NATURE-06` | 허용 나무 한 그루 벌목과 목재 직접 결과 | 플레이어 | 벌목 대상 자연물 |
| `WI-NATURE-18` | 첫 사건 수리 재료 준비·확인 | 플레이어 | 수리 재료/울타리 문맥 |
| `WI-NATURE-20` | 울타리 세 구간 전체 수리 확정 | 플레이어 | 손상 울타리 묶음 |

행위 Actor와 직접 대상은 [주체 대장](../../../eng/execution-ledgers/gameplay-subjects.json)의 계약을 먼저 통과한다. 한스의 관계 변화나 농장 거점성 변화는 직접 결과 뒤의 별도 파생 작용이며 이 문서에서 자동 생성하지 않는다.

## 논리·표현 요구

- Logic은 손도끼 소유·장착, 허용 나무, 목재 수량, Preview revision, 울타리 손상/수리 상태를 권위 상태로 판정한다.
- 울타리 수리는 세 구간을 개별 반복 클릭하지 않고 첫 사건에서는 한 번에 완료한다.
- Presentation은 부러진 도끼, 벌목 가능 나무, 목재 획득, 손상/수리 울타리, 다음 선택을 같은 revision으로 판독하게 한다.
- 애니메이션은 E6 정제 범위다. Clip·Rig가 없다는 이유로 Logic 직접 결과를 바꾸지 않는다.

## H 공간과 자산 요구

- AreaSet: `area-set:sim:pyeongchang:nature-home.v1`, `area-set:sim:pyeongchang:farm-production.v1`
- H2 후보: `h2-candidate:hans-farm:first-restoration`
- H1 재고/경계: `h1-stock:farm-fence-edge`
- 손도끼·나무·울타리의 Synty 후보는 Presentation E4 후보이며 실제 Prefab·Renderer·Collider·Bounds·InteractionAnchor 결속은 E5에서 별도로 검증한다.
- Graph Map과 배치 맵은 농장 접근, 벌목 대상, 울타리 통행과 복귀 동선에 영향을 주는 범위만 사용한다.

## 전문 심화 연구 판정과 재결속

| 분야 | 필요성 | 기준 | 상태 |
| --- | --- | --- | --- |
| 건물 | `NotRequired` | 첫 울타리 수리 직접 결과는 주택 수리에 의존하지 않는다. | `AcceptedAsNotRequired` |
| 공간 | `Required` | 나무·울타리·통행·복귀 동선 판독이 필요하다. | `Accepted` 기획 기준선 |
| 배치 | `Required` | H1 울타리 경계와 상호작용 지점의 실제 결속이 필요하다. | `E5 Blocked` |
| 애니메이션 | `Required` | 벌목 접촉·중단·복귀는 E6에서 정제한다. | `DeferredToE6` |

Presentation E5의 최종 Unity EditMode 검증은 별도 AreaSet authoring 컴파일 결손 때문에 차단돼 있다. 이 기획 문서 보완은 그 차단 해소 또는 실제 World 배치 증거가 아니다.

## 저장·권위·외부 경계

- 권위 상태는 Shared Simulation Core가 소유한다.
- Save/Replay는 부러진 도끼 회수, 개인 도끼 장착, 목재 획득·소비, 울타리 수리 상태와 revision을 보존한다.
- Unity는 입력과 표현을 담당하고 손상/수리 여부를 독자 확정하지 않는다.
- 외부 Provider, 운영 DB, 결제·거래, 실제 농장 자료는 이 주제의 필수 조건이 아니다.

## 제외 범위와 승인

- 제외: 한스 주택 수리, 한스의 은둔 고수 정체 확정, 경비대 임무, 실제 전투, 전체 농장 H4 완성, 애니메이션 E6, Play Mode·Game View E7.
- 승인 상태: `Approved`
- 승인 범위는 원천 기획의 플레이 약속과 기존 Logic E5 / Presentation E4 기준선까지다.
