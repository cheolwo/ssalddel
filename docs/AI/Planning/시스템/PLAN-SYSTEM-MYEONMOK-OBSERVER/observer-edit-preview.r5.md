# Game View 편집 미리보기 일치 — r5

상태: Approved / 구현·편집/정지 Play 비교 완료. 2026-09-07 사용자가 Game View도 Play Mode에 맞춰 나타내도록 요청했다. 현행화가 기존 r4 표현 범위에 편집 미리보기를 추가했다. 실제 View 캡처는 기존 월드 담당이 수행했으며 업무 개발 스레드로 구현을 넘기지 않았다.

현재 관찰 도형과 카메라는 RuntimeInitialize/Start 때만 생성된다. 편집 Game View의 다른 기본 카메라와 관찰 Play 화면이 달라지는 구조를 수정한다. canonical SimulationWorldShell·선택한 합성 프로필에 한정해 Play와 같은 Build/배경 자료·명암·대각선 카메라로 정적 공간을 미리 보여준다. 편집에서는 NPC/물품 상태·휴대폰 UI를 가짜로 만들지 않는다. Play에서 실제 Session/서버 조회가 이를 대신한다.

Logic 영향 없음. Presentation E3/E4: 같은 조립 코드와 화면비, E5: Editor 전용 임시 비저장 객체, E6: 프로필 변경/Play 전환 정리·중복 방지·컴파일·정적 결속 시험, E7: 편집 Game View 확인을 분리한다. 미리보기로 실제 실행 E를 승격하지 않는다.

정확 쓰기: Unity 신규 Bootstrap/동네편집미리보기.cs, Editor/동네GameView미리보기.cs, Tests/EditMode/동네편집미리보기Tests.cs 및 meta. 기존 r4 기본 표현 파일은 필요시 안전한 결속만 수정한다. Hongdal 이 문서·PLANNING·CURRENT_WORK. 미리보기는 Start/Update/Install·DB/API·Tick·저장·Command를 호출하지 않고, ExitingEditMode에 제거한다. 다른 Scene·프로필로 전환 시 제거하고 Assembly reload/quitting 때 정리한다. 원본 Scene 저장·슬롯 변경·새 주문·commit/push 없음.

## 실제 검증 결과

- 최종 컴파일 오류 없음, 신규 EditMode2/2 통과. 초기 Application 이름 충돌2건은 UnityEngine.Application 명시로 수정했다. 신규 소스3개 범위 검사·문서 Fast 통과. 기존 광역 Scene/slnx 공백 경고는 수정하지 않았다.
- 프로필5의 실제 Editor [편집 Game 탭](../../../../assets/changes/2026-09-07-observer-edit-preview/edit.png)과 [정지 Play Game 탭](../../../../assets/changes/2026-09-07-observer-edit-preview/play-paused.png)을 같은 창에서 비교했다. 카메라 위치(-69.2820,72.2820,-61.2820), 회전(35.2644,45,0), orthographicSize36.58489, pixelRect726×470, 바닥90×58이 일치한다. NPC와 휴대폰 UI는 Play에만 추가된다.
- Play에서 preview root0/runtime controller1, 종료 뒤 preview root1 복원. 편집에서는 controller 비활성·LocalRuntime 미설치. Game View 배율만1.1→1.0으로 맞춰 가장자리 잘림을 제거했으며 창 크기/해상도는 변경하지 않았다.
- 격리된 복사 슬롯으로 정지 Play만 검증했다. Tick249/Revision529 유지, 원본/복사 슬롯 전후 SHA256 `17B4DB84EA5D4786F2A149C193CD0653BAC383FF47868BF36139C246C5DA9CCC`. 재생·저장·새 주문·서버 호출 없음. 종료 후 profile5/saveRoot null/Play off/Scene clean 복원, 배율1.0 유지.
- 상세: `artifacts/local/validation/observer-preview-r5/preview-tests.json`, `artifacts/local/validation/observer-neighborhood-map-r3/edit-preview-comparison.json`. Pipeline 기본 screenshot은 Main Camera 오프스크린 렌더이므로 실제 Editor 창 캡처와 구분했다. 종료 화면 Console 오류0/경고13이며 기존 경고의 해소나 원인을 이 작업으로 판정하지 않는다.
- 프로필6은 조립/비권위 시험만 통과했으며 이번 실제 화면 비교는 프로필5에 한정한다. 동적 재생·휴대폰 상태 일치·모든 화면비·Player 빌드·E 승격은 이 검증에 포함하지 않는다.
