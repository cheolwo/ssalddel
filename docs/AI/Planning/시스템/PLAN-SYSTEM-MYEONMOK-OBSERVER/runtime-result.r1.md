# 가상 배달 관찰 구현 결과 r1

## 실행 방법

Unity 메뉴 `Ssalddel → 실행 프로필 → 가상 동네 배달 관찰`을 선택하고 기존 `SimulationWorldShell`에서 Play한다. `재생`을 누르면 1초당 한 Tick을 요청한다. 일시정지·한 단계 진행·전체 보기·기사 따라가기·저장 재시도를 제공한다. 기본 시작과 저장 재진입은 정지 상태이며 종료 시간은 따라잡지 않는다.

현재는 기존 Session 상한 안의 **365 Tick 제한 표본**이다. 종료에 앞서 마지막 주문을 수령하고 기사가 복귀하며 새 묶음 생성을 멈춘다. 종료 후 `새 표본 시작 · 전용 저장 교체`를 명시적으로 선택할 수 있다. 무제한 상시 운영이나 운영 서버 서비스가 아니다.

## 구현된 흐름

음식점 1곳·주택 2곳·기사 1명. Core가 모의 주문 두 건을 생성하고 기존 NPC 수락·조리(자리1·2 Tick)를 재사용한다. 기사에게 한 주문을 결속해 도로 이동→출입구 보행→조리 완료 대기→픽업→주택 이동→전달→다음 Tick 수령→보행 복귀→차량 복귀를 진행한다. 두 주문 수령과 복귀 후에만 다음 묶음을 생성한다.

- 공통 Session의 `가상배달관찰.cs`가 기사·물품·주문 결과를 소유한다. 별도 주문 원장은 만들지 않았다.
- 고정 합성 경로는 승인 공간 연구 좌표를 사용한다. 동적 GIS·실시간 도로 폐쇄·다중 기사 배차는 아니다. 이전 Application 경로 후보 모듈을 Domain에서 역참조하지 않는다.
- `SyntheticCourier`는 복제된 조회 결과다. 기존 저장의 null 필드는 기존 해시를 바꾸지 않으며, 새 프로필은 기사 상태와 자동 주문을 Tick 재생·해시에 결속한다.
- 기사 배정·이동·픽업·전달·수령·복귀 WI를 등록하고 행위 원장을 남긴다. NPC 관찰은 플레이어 진척 지급 대상이 아니다.
- Unity의 신규 Controller는 기본 도형·차량·기사·상자와 카메라만 조립한다. Scene 파일이나 다른 영역을 수정하지 않는다. 표현 객체는 별도 카메라 layer31로 분리하고 물리 충돌은 끈다.
- 각 성공 Tick 뒤 전용 슬롯 `synthetic-delivery-r1-primary`에 저장한다. 실패는 표시하고 재생을 멈추며 저장 재시도를 제공한다. 기존 음식점 수동 표본과 100 Tick 설정은 보존한다.

## 검증과 제한

- 신규 Core 시험6개: 중간 저장1/9/23/40/90 Tick의 재진입·동일 진행·최종 저장 해시, 반복 배송, 수령 시각 분리, 중복 Tick, 위치 상한·외부 사본 변경 차단, 365 Tick 종료 시 미수령 주문 없음.
- Fast `20260907-075947`: Simulation 관련304개·Unity 라이브러리 관련10개 통과. Task `20260907-080133`: Simulation 전체1803개·Unity 라이브러리 전체720개 통과, 두 솔루션 build 통과.
- WI 대장118개 및 주체/기획 결속 검사 통과. 자동 E 승격은 하지 않았다.
- 사용자 승인으로 Scene·자산을 저장하고 Editor를 정상 종료·재시작했다. 시작 시 발견된 `Application.isPlaying` 이름 충돌은 `UnityEngine.Application.isPlaying`으로 수정했다. 정상 Editor에서 canonical Scene을 다시 열고 최신 `Save` 메서드와 카메라 layer31 분리를 직접 확인했다.
- 재시작 후 실제 Unity EditMode 시험2/2 통과: 도형 좌표·상자 표시·사본 불변, 합성 프로필·전용 저장 슬롯 분리. 임시 객체는 제거했다. 기록은 `artifacts/local/validation/synthetic-delivery-editor-tests-after-restart.json` 및 `synthetic-delivery-editor-after-restart.json`이다. 이전 0개 탐지는 재시작 전 결과이며, 이번 검증도 Play Mode 완주 증거는 아니다.
- 실제 Play Mode에서 재생 버튼 입력 후 첫 주문의 Tick10 픽업·Tick27 전달·Tick28 수령을 확인했고 Tick36 복귀 중 일시정지했다. 캡처·UI 잘림·타 모듈 혼입·서버 연결 오류와 후속 점유 규칙은 [공간·실행 통합 r2](synthetic-spatial-study.r2.md)에 함께 기록한다. 두 주문 전체 완주·실제 저장 재진입·Hosted 연결은 미검증이다. 정산·날씨·실제 GIS·운영 DB는 범위 밖. commit·push 없음.

[r1 구현 명세](complete-delivery-work.r1.md) · [현행 공간·실행 통합 기준 r2](synthetic-spatial-study.r2.md) · [r1 공간 기준 이력](synthetic-spatial-study.r1.md)
