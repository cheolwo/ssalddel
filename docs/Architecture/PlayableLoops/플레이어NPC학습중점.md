# 플레이어 NPC 학습 중점

## 식별과 근거

- 주제 고유 식별자: `topic:player-npc-learning-focus.v1`
- 선택적 PlayableLoop 고유 식별자: `playable-loop:player-npc-learning-focus.v1`
- 기획 revision: `player-npc-learning-focus.design.r1`
- 원천 기획 문서:
  - `docs/Architecture/PlayableLoops/PlanningSessions/플레이어내면명상/player-mind-meditation.inquiry.r1.md` (`player-mind-meditation.inquiry.r53`)
  - `docs/AI/절기전략턴-타로카드-기획-2026-09-02.md`
- 승인 근거: `docs/AI/PLANNING.md`
- 현재 구현 기준: Logic E3 / Presentation E3. E4 이상은 차단 상태를 유지한다.
- Story Beat 결속: `NotApplicable`. 독립적인 학습 중점 시스템이며 메인 스토리 사건의 완료 권위가 아니다.

## 플레이어 약속과 재미

- 플레이어는 보유한 NPC 학습 카드 한 장을 학습 중점으로 장착하거나 주 슬롯을 비워 둘 수 있다.
- 절기 안의 초반·중반·후반 구간에서 변경하면 다음 정상 월드 구간부터 적용된다.
- 카드와 결속된 실제 플레이어 행위가 성립할 때만 제한된 이해도 기여를 한 번 받는다.
- 카드 선택만으로 생산량·속도·피해·회복·위협·이데아 발견 확률을 직접 바꾸지 않는다.

## 반복 폐루프

`보유 카드 확인 → 한 장 장착 또는 비우기 → 다음 정상 구간 진입 → 결속된 WI 직접 수행 → 이해도 기여 확인 → 유지·교체·비우기`

- 진입 상태: 학습 주기 일정과 보유 NPC 학습 카드 목록이 준비돼 있다.
- 귀환 상태: 현재 중점 카드·빈 슬롯·현재 구간·다음 변경 적용 시점을 다시 확인하고 선택할 수 있다.
- 이 Loop는 위 반복 폐쇄성을 함께 검증하기 위한 선택적 묶음이다. 개별 WI의 Command와 직접 결과는 각 WI Goal이 소유한다.

## 선택·대가·성공·실패·회복

- 선택: 보유 카드 한 장 장착, 다른 카드로 교체, 주 슬롯 비우기.
- 대가: 변경은 즉시 소급 적용되지 않고 다음 정상 구간을 기다린다. 한 슬롯이므로 동시에 여러 중점을 활성화하지 못한다.
- 성공: 결속된 실제 플레이어 행위가 해당 구간에 처음 성립하면 이해도 `+1` 기여를 멱등하게 기록한다.
- 실패·무효: 미보유 카드, 잘못된 구간, NPC 대리 수행, 조회·취소·Save 복원만으로는 기여하지 않는다.
- 회복: 잘못 고른 카드를 다음 허용 구간에 교체하거나 비워도 기존 성장치를 잃지 않는다. 실패를 생산·전투 손실로 확대하지 않는다.

## WI 단일 책임 후보

| WI | 직접 책임 | 행위 주체 | 직접 대상 |
| --- | --- | --- | --- |
| `WI-FARM-01`~`WI-FARM-06` | 실제 Farm 행위와 직접 결과 | 실행 시 권위 문맥의 Player | 해당 Farm 대상 |
| `WI-NATURE-06` | 실제 벌목과 직접 결과 | 실행 시 권위 문맥의 Player | 벌목 대상 자연물 |
| `WI-NATURE-11` | 실제 Nature 행위와 직접 결과 | 실행 시 권위 문맥의 Player | 해당 Nature 대상 |

학습 중점은 이 WI들의 직접 결과를 대신하지 않는다. 실제 ActionRecord와 카드 결속을 읽어 별도 이해도 기여를 한 번 적용하는 파생 작용이다. 신규 개발 Goal은 위 WI 중 하나만 소유한다.

## 논리·표현 요구

- Logic: 현재 카드, 보유 카드, 적용 구간, 다음 변경, 마지막 기여 계보와 revision이 결정적으로 저장·복원돼야 한다.
- Logic: 같은 ActionRecord를 재처리하거나 Save/Replay해 이해도 기여가 중복되지 않아야 한다.
- Presentation: 현재 중점·빈 슬롯·적용 구간·다음 적용 시점을 읽을 수 있어야 한다.
- Presentation E4 미정: 실제 카드 슬롯 VisualKey, 입력 방식, 비색상 판독, 변경 확인 피드백.
- Logic과 Presentation은 같은 권위 revision을 소비하며 UI가 학습 상태를 직접 변경하지 않는다.

## H 공간과 자산 요구

- 이 기능 자체는 공간 배치를 요구하지 않는다.
- 카드 UI는 공간 H 계약과 분리한다.
- 실제 WI가 공간·대상·자산을 요구하면 각 WI Goal의 Graph Map·배치 맵·Presentation E4가 소유한다.
- Synty Prefab, Blender 가공, NavMesh는 이 학습 중점 주제에 `NotRequired`다.

## 전문 심화 연구 판정과 재결속

| 분야 | 필요성 | 사유 | 상태 |
| --- | --- | --- | --- |
| 건물 | `NotRequired` | 학습 중점 상태 계약은 건물 형상에 의존하지 않는다. | `AcceptedAsNotRequired` |
| 공간 | `NotRequired` | 공간 요구는 결속된 개별 WI가 소유한다. | `AcceptedAsNotRequired` |
| 배치 | `NotRequired` | 카드 선택과 이해도 기여는 배치 결과가 아니다. | `AcceptedAsNotRequired` |
| 애니메이션 | `NotRequired` | 애니메이션은 결속된 개별 WI의 E6 표현 정제 대상이다. | `AcceptedAsNotRequired` |

- 관계 기반 카드 취득, 플레이어 멘토 공유, 보조 슬롯은 후속 별도 주제다.
- E4에서는 카드 판독·입력·접근성 기준만 새로 승인한다.

## 저장·권위·외부 경계

- 최종 권위는 Shared Simulation Core의 학습 중점 상태와 ActionRecord 계보다.
- Save/Replay는 현재 카드, 적용 구간, 예약 변경, 기여 멱등 키와 revision을 보존한다.
- LocalProcess와 RemoteHost는 같은 명령·검증·투영 계약을 사용한다.
- 공개 멘토 카드 서버, 친구 공개 범위, 외부 Provider, 운영 DB 쓰기는 포함하지 않는다.
- Unity UI와 카드 이미지는 읽기·입력 표현이며 권위 상태를 소유하지 않는다.

## 제외 범위와 승인

- 제외: 실제 카드 UI, Unity 입력, Game View, 온라인 멘토 공유, 관계 기반 카드 취득, 장기 성장 단계, 생산·전투 수치 보정.
- 현재 승인 범위: 기존 E1~E3 계약·Core·투영·Local/HTTP·Save/Replay 기준선의 기획 문서화.
- 승인 상태: `Approved`
- 이 문서의 승인은 E4 이상, Runtime 실행, Unity 배치 또는 Game View를 승인하지 않는다.
