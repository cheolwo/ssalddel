# 축소하지 않는 동네 관찰 맵

날짜: 2026-09-07. [승인 범위](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-map.r3.md) · [구현·검증 결과](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-map-result.r3.md)

| 커밋 | 변경 축 | 화면 변경 | 시각 증거 |
| --- | --- | --- | --- |
| 미커밋 | Unity 서버 관찰 배경·전체 관찰 카메라 | 실제 확인 | 아래1920·1280 최종 Game View |

기존 로컬 동네는 보존하고 서버 관찰 프로필6의 작은 별도 배경을 확장한다. 음식점·주택·서버 NPC의 현재 위치를 유지하면서 주변 도로와 미연동 주택·음식점·마트·창고를 추가한다. 휴대폰 열림으로 남은 관찰 영역이 달라져도 맵 자료를 바꾸지 않고 카메라를 맞춘다.

## 실제 화면과 검증

- [동네 전체 보기·1920×1080](../assets/changes/2026-09-07-observer-neighborhood-map/04-final-map-closed-1920.png)
- [음식점 선택·휴대폰 열림·1920×1080](../assets/changes/2026-09-07-observer-neighborhood-map/03-final-map-selected-1920.png)
- [휴대폰 열림·1280×720](../assets/changes/2026-09-07-observer-neighborhood-map/06-final-map-open-selected-1280.png)

Unity 최종 컴파일 오류 없음·신규 EditMode18/18·기존 관찰23/23 통과. 두 해상도의 실제 열림/닫힘·NPC 선택과1280 스크롤을 확인했다. 작은 이름표와 휴대폰 가장자리의 다른 카메라 비침은 최종 화면 전에 보완했다. 기존 서버 주문·판본343·4주체 좌표·13이벤트 불변, Play 종료·원래 프로필/Scene/창 설정 복원 확인.

최종 Console은 Error0·Warning12이며 미지정 스크립트/Nature 관련 경고의 원인·이전 기준선 비교는 남아 있다. 새 배경의 건물 수는 서버 주문·주체·업무 수가 아니다. 여러 음식점에서 새 주문이 생기고 여러 기사가 확장 구역을 순회하는 장면은 이번 구현/검증 범위가 아니다. Scene 저장·E 자동 승격·commit·push 없음.
