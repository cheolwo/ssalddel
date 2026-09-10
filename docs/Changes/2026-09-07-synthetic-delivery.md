# 가상 동네 배달 관찰

- 최신 r4: [주문 원장·수락 후 배차 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/order-ledger-result.r4.md). 주문자 상태·음식점 미확인 건수·접수/배차 대기 표시와 저장 실패 후 진행 차단. Unity 컴파일·EditMode5/5, 코드·시험 검증 완료. 실제 r4 Play Mode·Game View 미검증, Scene 저장·commit·push 없음.
- 이전 r3: [살뜰마트 배송센터 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/mart-center-result.r3.md). 음식점 빨강·주택 파랑·마트 하양·기사 검정, 지붕 없는 마트 내부와 작업자 피킹·포장·인계 물품. 이전 PNG는 r3/r4 표현의 증거로 사용하지 않는다.

- 변경: 명시적 합성 배달 프로필, 도로·건물·출입구·기사·차량·물품, 재생·정지·저장·관찰 카메라.
- 기준 Scene: 기존 SimulationWorldShell. Scene 파일·기존 시각 자산 변경 없음.
- 검증: .NET Core·Unity 라이브러리 시험 통과. 재시작 후 최신 코드·카메라 분리·Unity EditMode2/2 확인. 실제 Play Mode 입력으로 첫 주문 픽업·전달·수령과 복귀 진행까지 확인했으며 전체 완주·Hosted는 미검증이다.
- [Game View 원본](../assets/changes/2026-09-07-synthetic-delivery/home.png) · [UI·Console 포함 Editor 원본](../assets/changes/2026-09-07-synthetic-delivery/editor.png). UI 잘림·타 모듈 혼입·서버 연결 오류와 승인된 다중 기사 점유/대기 기준을 [공간·실행 통합 r2](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/synthetic-spatial-study.r2.md)에 통합했다. 점유 규칙은 아직 구현되지 않았다.
- 실제 운영·시각 마감 완료로 해석하지 않는다. 커밋 전.
- 후속 r2: [마트·도로변 대기 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/waiting-fleet-result.r1.md). 기사3명·별도 정차자리5개·마트 외형·상태 패널·가까운 자리 복귀를 추가했다. 통행 묶음 하나로 동시 도로 이동1대만 허용하는 보수적 구현이며 구간별 동시 통행은 미구현이다. 실제 초기 Play Mode 배치·Unity EditMode3/3 확인. 이전 문단의 점유 미구현은 r1 시점 기록이다.
- [사용법과 상세 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/runtime-result.r1.md)
- r2 자동 Play Mode 관찰: 첫 두 주문 수령27/58 Tick·도로변 복귀·다음 묶음 확인,89 Tick 저장/일시정지. [초기 대기](../assets/changes/2026-09-07-synthetic-delivery/waiting-start.png) · [배달·도로변 대기](../assets/changes/2026-09-07-synthetic-delivery/waiting-return.png). 직접 버튼 입력 검증과는 구분한다.
