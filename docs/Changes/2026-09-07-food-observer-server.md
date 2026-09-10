# NPC 역할 앱·업무 서버 관찰

날짜: 2026-09-07. 커밋 전. [승인 범위](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone.r2.md) · [코드 관계·실행법·검증 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone-result.r2.md)

## 화면 변화

기존 Figma `0KhuQLc1MleUBIQnARC21Z / 2269:177`의 같은 주문번호·역할 카드·상태 구성으로 기존 Unity 휴대폰을 확장했다. 새 프로필6에서 NPC를 선택하면 역할·상태·마지막 행동·다음 행동·대기 이유를 읽는다. 지도는 기존 음식점 빨강·주문자 파랑·기사 검정 도형을 사용한다. 서버 상태를 받으며 이 프로필에 LocalSimulationRuntime을 설치하지 않는다.

서버가 끊기면 마지막 자료를 오래된 결과로 표시한다. 다른 실행/주문·낮은 판본은 자동 수용하지 않는다. 종료된 결과에서 새 서버 Idle로 바뀌면 먼저 확인하고, 주문 시작은 별도 입력이다. 기존 플레이 모드 전환은 프로필6에서만 잠근다.

## 실제 화면 증거

- [기사배정·조리 대기](../assets/changes/2026-09-07-food-observer-server/10-running-assigned-cooking-1920.png)
- [주문자 수령 확인](../assets/changes/2026-09-07-food-observer-server/12-customer-received.png)
- [이전 결과 보존·새 서버 확인 요구](../assets/changes/2026-09-07-food-observer-server/08-new-host-confirmation-required.png)
- [5분 제한 관찰 완료](../assets/changes/2026-09-07-food-observer-server/14-five-minute-completed-1920.png)
- [실제 서버 단절·완료 주문 보존](../assets/changes/2026-09-07-food-observer-server/15-app-disconnected-order-preserved.png)
- [같은 서버 재시작·같은 주문 재연결](../assets/changes/2026-09-07-food-observer-server/16-reconnected-same-completed-order.png)

1920×1080·1280×720에서 휴대폰 열기/닫기·NPC 선택·재조회와 새 서버 결과 확인·시작 분리를 실제 입력으로 확인했다. 정지된 완료 화면은 도로 이동 전 구간의 영상 증거가 아니다. 최종5분/재연결 판정과 초기 실패 이력은 결과 문서가 소유한다.

## 검증 수준·한계

Unity 컴파일·집중23/23·기존 휴대폰8/8, 서버 집중34/34·추가 인계/Outbox 회귀10/10 통과. 전체 Task는 v3.5 build 통과·4873/4880 시험 통과이며 범위 밖7실패를 결과 문서에 남겼다. 실제 서버 음식 배달은 수령확인까지 도달해299.4018초로 관찰을 종료했다. 화면은04:59/05:00이며300초로 임의 보정하지 않았다. app 단독 재시작 뒤 같은 주문·경과시간·선택을 유지하고 조회 판본341→343으로 복구했다.

원본 Scene/저장 슬롯·기존 로컬30분 생활 규칙은 보존했다. 기존 복사 슬롯 Save3726.1ms 지연, 도착 위치에서 기사/주문자 이름표 겹침, Escape 키 동작의 실제 확인, 장시간 안정성은 남은 항목이다. 실제 운영·Scene 저장·E 자동 승격·commit·push 증거가 아니다.

추가 공용 Unity 모드 회귀는3/5 통과·Farm 전환/Scene 버튼 개수2실패다. 새 관찰23/23·휴대폰8/8의 성공으로 전체 Unity 회귀까지 성공했다고 판정하지 않는다. 해당 기존 Farm 변경과 시험 기대값은 수정하지 않았으며 원인 진단 범위는 결과 문서에 남겼다.
