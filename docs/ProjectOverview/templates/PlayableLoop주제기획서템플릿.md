# [주제 이름]

## 식별과 근거

- 주제 고유 식별자: `topic:[name].v1`
- WI Goal 고유 식별자: `interaction-goal:[name].v1`
- 선택적 PlayableLoop 고유 식별자: `없음 | playable-loop:[name].v1`
- 기획 revision: `[name].design.r1`
- 원천 기획 문서:
  - `docs/...`
- 문답 정밀화 기록:
  - `docs/...`
- 마지막으로 반영한 문답 revision:
- 문답에서 남은 승인 차단 미정:
- 상위 사건 마디 결속: `NotApplicable | Candidate | Accepted`
  - `StoryBeatStableId`:
  - 괘·효 참조: `HEX-##-*` / `HEX-##-*-L#`
  - 결속 역할: `PrimaryExperience | SupportingExperience | AlternativeExperience`
  - 사건 문서·revision·SHA-256:
  - 기존 주체·WI 재사용 판단 또는 새 주제 필요 사유:

상위 사건 마디가 없는 독립 플레이는 `NotApplicable` 사유를 남긴다. 사건 마디 결속은 이 주제의 승인된 주체·WI Goal 기획 관문을 대신하지 않는다.

## 주체 기반

| 역할 | 주체 후보 또는 `subject:*` | 종류 | 권위 상태 소유자 | 초기 해석·저장 정체성 | 상태 |
| --- | --- | --- | --- | --- | --- |
| Actor |  |  |  |  | `Candidate | Ready | Blocked` |
| 직접 대상 |  |  |  |  | `Candidate | Ready | Blocked` |

- 기존 주체 재사용 판단:
- 구체 인물·사물·시설·환경을 별도 주체로 만들어야 하는 이유:
- 주체 정의가 Runtime 인스턴스·Unity 배치 증거가 아님을 확인했는지:

## 플레이어 약속과 재미

- 플레이어가 처한 상황:
- 플레이어가 원하는 것:
- 반복해도 재미있어야 하는 핵심 선택:
- 짧은 플레이어 약속 한 문장:

## 반복 폐루프

`상황 → 욕구 → 선택 → 대가 → 행동 → 결과 → 회복·귀환 → 다음 선택`

- 진입 상태:
- 종료 뒤 다시 열리는 선택:
- PlayableLoop 필요성: `Required | NotRequired`
- `Required`라면 단일 WI로 닫히지 않는 반복 검증 범위:
- `NotRequired`라면 WI의 종결·귀환·다음 선택 경계:

## 선택·대가·성공·실패·회복

- 선택지:
- 자원·시간·위험 대가:
- 성공 결과:
- 실패 결과:
- 실패 뒤 회복 경로:

## WI 단일 책임 후보

| 순서 | WI 후보 | 한 번에 바꾸는 권위 상태 | Actor 주체 | 직접 대상 주체 | 비고 |
| --- | --- | --- | --- | --- | --- |
| 1 | `WI-...` |  | `subject:...` | `subject:...` |  |

- 이번 Goal이 소유할 WI 하나:
- 직접 완료 상태와 직접 효과:
- 파생 작용: `NotApplicable | OneHop | TwoHop`
- 파생 작용이 있다면 인과·멱등성·Save/Replay 근거:

## 논리·표현 요구

- 논리적으로 반드시 성립할 상태와 규칙:
- 플레이어가 화면과 소리로 식별해야 할 대상:
- 결과가 같은 revision임을 보여줄 피드백:
- 공통 표현 검증 모듈 외 조건 모듈:

## H 공간과 자산 요구

- 필요한 H1~H5 능력:
- 실외·실내 배치 요구:
- Synty 자산 후보와 대체 표현:
- Traversal, Collider, NavMesh 요구:

## 전문 심화 연구 판정과 재결속

| 분야 | 필요성 | 연구 문서 참조 또는 NotRequired 사유 | 상태 | 기획서 반영 항목 |
| --- | --- | --- | --- | --- |
| 건물 | `Required | NotRequired` |  |  |  |
| 공간 | `Required | NotRequired` |  |  |  |
| 배치 | `Required | NotRequired` |  |  |  |
| 애니메이션 | `Required | NotRequired` |  |  |  |

- `requiredDetailStudyRefs`의 모든 `Required` 연구가 `Accepted`인지:
- 연구 결과로 다시 연 Logic E와 이유:
- 연구 결과로 다시 연 Presentation E와 이유:
- 연구끼리 충돌한 사항과 기획 판단:
- 개발 인계에 고정할 측정값·자산 fallback·검증법:

## 저장·권위·외부 경계

- Simulation 권위 상태:
- Save/Replay에 고정할 값:
- LocalProcess/RemoteHost 동등성:
- 외부 Provider 또는 운영 효과 제외:

## 제외 범위와 승인

- 이번 주제에서 하지 않는 것:
- 검토할 사람 또는 근거:
- 승인 근거 참조:
- 승인 상태: `Draft`
