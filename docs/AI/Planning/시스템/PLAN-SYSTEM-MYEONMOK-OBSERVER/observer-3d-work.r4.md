# 입체 도형 관찰 — 승인·표현 작업 명세 r4

상태: Approved / 코드·집중 시험 완료 / 실제 화면 미검증 (2026-09-07). 사용자는 위에서 비스듬히 보는 구도를 승인하고 이번 구현 담당을 개발이 아닌 현재 현행화 스레드로 지정했다. 공통 목적 r10의 구도 승인·r9의 도형 우선 방향과 기존 observer-map.r3의 공간 기준선을 유지한다. 이 범위의 실행 승인은 이 사용자 답변이며 과거 개발 읽기 조사와 별개다.

## 범위·수용한 표현 기준선

- 기존 Cube는 이미 높이가 있다. Unlit 단색과 수평 회전 없는 카메라를 우선 보완한다. 새 Synty/Blender 자산·Scene·NPC 역할·마법 UI는 만들지 않는다.
- 대각선 관찰 방향 (1,-1,1), 면 법선에 따른 고정 명암으로 상자 윗면/옆면을 구분한다. 전역 조명/날씨/다른 World 재질은 바꾸지 않는다. 기존 역할 색을 기본색으로 유지한다.
- 프로필6의 120×96 전체 맞춤과 로컬 동네의 기존 바닥·업무 좌표를 보존한다. 로컬 전체 관찰도 높이를 포함하는 Bounds 맞춤을 사용하고 근접/시설 관찰은 기존 선택을 유지한다. 열린 음식점·창고 내부를 지붕으로 가리지 않는다.
- 기존 NPC 위치 결과만 표시한다. 완료된 서버 주문의 정지 NPC를 움직이도록 조작하지 않는다. 조리·이동 시간·서버/API·저장 슬롯은 불변이다.

## E 영향·검증

Logic E1~E7: NotApplicable, 기존 상태 사본·업무 인계 재사용. Presentation E1 관찰 목적 → E2 면/높이 판독 → E3 기존 좌표/Bounds → E4 기본 Cube와 고정 명암 shader → E5 기존 조립 경로 결속 → E6 컴파일·집중 카메라/재질 시험 → E7 실제 화면 판독을 구분한다. 실제 화면은 미검증이면 승격하지 않는다. shader 실패는 컴파일/자료 오류로 보고하며 업무 성공으로 대체하지 않는다. 잘못된 구도는 표현 E3/E4를 다시 연다.

현행화가 Unity `가상배달관찰Controller.cs`, `가상동네생활View.cs`, `동네관찰휴대폰Binding.cs`, `동네전체관찰CameraMath.cs`, 기존 `업무배달동네배경Tests.cs`, 신규 `Assets/Ssalddel/Resources/관찰도형명암.shader` 및 meta를 소유한다. Hongdal은 이 문서와 공통 목적/목차/현재 상태만 수정한다. 개발로부터 관련 경로 점유 없음 확인을 받았다. Game View는 별도 실제 증거이며 임의 Play/저장/새 주문 실행으로 증거를 만들지 않는다. commit/push 없음.

## 결과·검증·남은 범위

- 공통 Cube 재질에 Resources 결속 명암 shader를 적용했다. 윗면/옆면 밝기를 구분하고 완전 검정 역할도 최소한의 어두운 명암을 갖게 한다. 대각선 전체/근접/시설 카메라와 이름표 방향을 맞췄다. 로컬 동네는 일시정지 중에도 휴대폰/화면비 변화에 전체 Bounds 맞춤을 갱신한다.
- Unity Pipeline 직접 도구는 시간 초과했으나 Unity CLI의 `command` 경로로 같은 Editor Pipeline에 접속해 컴파일·EditMode를 수행했다. 최종 컴파일 failed=false/errors=[]; 업무배달 관련46/46(배경23+기존23), 로컬 휴대폰7/7 통과. shader import/오류 없음·실제 조립 재질 결속·기존 위치·범위·화면비를 검사했다.
- 상세 결과: `artifacts/local/validation/observer-3d-r4/server-and-map-tests.json`, `local-phone-tests.json`. 새 shader meta는 Unity가 생성한 GUID를 보존했다. Scene은 `SimulationWorldShell`, isDirty=false를 확인했다.
- Play Mode·Game View·실제 움직임·면별 가독성·이름표 시각 판독은 미검증이다. 이번 작업에서 NPC 이동/주문 생성·5분 업무 재실행·서버 접속·원래 슬롯 저장을 하지 않았다. Shader가 빌드에도 포함되도록 Resources에 두었으나 Player 빌드는 미검증이다. 기존 전체 회귀 문제와 저장 지연은 해결한 것으로 주장하지 않는다.
- 마법 통신구 UI와 고급 모델은 미구현이며 이번 범위가 아니다. 시각 문제가 발견되면 이 r4의 표현 E3/E4를 다시 열고 상태·업무 규칙 변경과 분리한다. E 승격·commit·push 없음.
