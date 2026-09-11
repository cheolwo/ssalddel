# 운영 음식 배달 기사와 Unity NPC 관찰 역할 경계 r2

> 문서 분류: `시스템 / 운영 기능 이관 / SupportingSlice`. 과거 경로와 기획 ID는 호환을 위해 유지하며, 현행 소유 맥락은 [`PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001`](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)이다. 거절률·완료율·시간당 균형은 [운영 배차 공통 코어](../../공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)가 소유한다.

- 기획 ID: `PLAN-SYSTEM-MYEONMOK-OBSERVER`
- 분야·판본: 시스템 / `driver-role-boundary.r2`
- 상태: `Approved / AuthorityContractImplemented / ActiveWorkLimitImplemented / UnityDevelopmentGateImplemented / RuntimeVisualVerificationPending`
- 승인 근거: 음식 배달 기사 배려 상태를 검증하고 보완을 시작하라는 요청 뒤 제안한 r2를 사용자가 `Implement the proposed plan`으로 승인했다.

## 확정

FDriver 모바일 앱은 실제 기사가 로그인하고 운행 시작·종료, 위치 제공, 제안 수락·거절, 픽업·전달을 명시적으로 수행하는 운영 도구다. 실제 업무 상태는 인증된 운영 API와 서버 원장이 확정한다.

Unity는 가상 NPC가 주문·조리·배차·배송을 수행하는 세계를 관찰하는 클라이언트다. Unity의 카메라, NPC 선택, 휴대폰 카드, 이동 보간과 애니메이션은 실제 기사 위치·배차·픽업·전달·정산을 생성하거나 확정하지 않는다.

`IBusinessWorkflowRuntime`의 `LocalProcess`와 `RemoteHost`는 모두 `SimulationSession` 권위, `AutonomousNpcWorld` 경험, `ObservationPresentationOnly=true`, `AllowsOperationalDriverActions=false`를 만족해야 한다. 여기서 `RemoteHost`는 원격 Simulation Host이며 운영 음식 배달 서버를 뜻하지 않는다. FDriver는 이 Runtime을 주입하지 않고 `IFoodDeliveryDriverApiService`의 운영 경로만 사용한다.

서버는 기사 한 명의 진행 중 음식 배달과 새 수락 요청을 합산해 최대 3건만 허용한다. 단건과 묶음 수락 모두 같은 정책을 사용하며 초과 시 전체 요청을 변경하지 않고 HTTP 409, `FoodDeliveryActiveWorkLimitExceeded`로 응답한다. FDriver는 업무공간 응답의 `MaxActiveDeliveries`를 사용하고 숫자 3을 자체 판단 기준으로 두지 않는다.

Unity 프로필5는 로컬 자율 NPC 생활 관찰이다. 프로필6은 격리된 개발 검증 서버의 합성 Simulation 결과를 관찰하는 경로이며 Editor 또는 Development Build에서만 허용한다. 배포 빌드에서는 프로필6 설정을 거부한다. 공식 Scene은 계속 `SimulationWorldShell`이며 새 Scene·Map Manager·상태 권위를 만들지 않는다.

## 구현 경계

- 기사 수락 상한은 기사별 프로세스 동시성 관문과 DB `Serializable` 트랜잭션 안에서 다시 계산한다.
- 추천 정보에 포함된 상세 수령 정보는 기존대로 배차 확정 전 공개하지 않는다.
- Unity의 프로필6 HTTP는 격리 검증 주소와 `/verification/*` 계약만 사용한다. FDriver 운영 경로를 호출하지 않는다.
- 화면 문구는 `가상 NPC 관찰 · 실제 기사 배차 없음`을 기준으로 한다.

## 후속 보완

이번 판본은 기사 수익·수수료 정책을 바꾸지 않는다. 다음 별도 운영 기획에서 현재 거리 기반 예상 지급액, 관리자 요금 정책, 기사 지급 원장, 이용료 화면과 설정을 한 판본으로 결속한다. 대기·날씨·야간·묶음 보상, 사고·분쟁·재배달과 정산 이의 제기는 그 뒤 독립 정책으로 검토한다.

## 검증 상한

코드·단위 시험·Unity EditMode는 계약과 정적 경계를 입증한다. 실제 운영 서버 연결, 실제 기사 운행, Unity Play Mode·Game View와 배포 빌드 차단은 각각 별도 실행 증거가 있어야 완료로 올린다.

다음 질문 하나: 기사 수익·수수료 정책의 첫 기준을 `완료 건 지급 원장`과 `수락 건 이용료` 중 어느 쪽부터 단일화할지는 후속 운영 기획에서 정한다.
