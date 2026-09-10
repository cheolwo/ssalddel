# 동네 관찰 맵 확장 — 결과 r3

상태: MapExpanded / ActualViewVerified / MultiOrderDeferred. [승인 기획](observer-map.r3.md)·[작업 명세](observer-map-work.r3.md)의 범위다. 기존 E 원장을 승격한 상태가 아니다.

확인된 원인은 기존 로컬 프로필의 90×58 동네와 달리 서버 프로필6에서 별도 58×28 바닥·건물 두 개를 조립했고, 휴대폰을 열 때 관찰 영역만 좁혀 카메라의 가로 시야까지 줄어든 것이다. 기존 로컬 동네 자체가 삭제된 것은 아니다.

## 현재 구현과 검토

120×96 배경·교차도로·미연동 주택14동/음식점4동/마트·보충창고2곳과 전체 관찰 영역 맞춤을 승인된 Unity5개 코드 경로 안에 구현했다. 기존 활성 음식점·주문자 집과 서버 주체4명은 유지한다. 지도는 정적 표현 자료이며 배경에 업무 주체·재고·배차·이동을 추가하지 않는다.

첫 컴파일 오류 없음, 새 집중 EditMode18/18(0.12초)·기존 서버 관찰 회귀23/23(0.18초) 통과. `artifacts/local/validation/observer-neighborhood-map-r3/editmode-first.json`, `observer-regression-23.json`은 이름표/단색 배경 최종 보완 전 결과다. 최종 재검증과 구분한다.

첫 실제1920 화면에서 동네 전체 범위는 확인했으나 이름표가 너무 작고 휴대폰 가장자리에 다른 카메라의 배경이 비치는 것을 발견했다. 개발 통합의 실제 캡처 검토 뒤, 같은 신규 View의 이름표 높이/글자 크기와 동일 Binding의 프로필6 전용 단색 배경 카메라로 보완했다. 기존 Main Camera/Scene·공용 Label/다른 프로필을 바꾸는 작업은 아니다. 이 결함 보완은 같은 승인 범위의 판독·카메라 결속이며 플레이 규칙/공간 좌표를 바꾸지 않는다.

## 최종 소스 검증

- Unity 컴파일 오류 없음. 최종 집중 EditMode18/18(0.56초)·기존 관찰 회귀23/23(1.09초) 통과. `artifacts/local/validation/observer-neighborhood-map-r3/editmode-final-18.json`, `observer-regression-final-23.json`의 최종 결과를 개발 통합이 재조회했다.
- [1920×1080 최종 음식점 선택 화면](../../../../assets/changes/2026-09-07-observer-neighborhood-map/03-final-map-selected-1920.png)·[동네 전체 보기](../../../../assets/changes/2026-09-07-observer-neighborhood-map/04-final-map-closed-1920.png)·[1280×720 휴대폰 열림](../../../../assets/changes/2026-09-07-observer-neighborhood-map/06-final-map-open-selected-1280.png): 개발 통합이 직접 이미지 검토로 전체 배경·시설 이름표·미연동 안내·단색 합성을 확인했다. 월드·공간·배치 담당은 두 해상도의 휴대폰 열림/닫힘·지도 NPC 선택과1280 스크롤 중 카메라 수치 불변을 실제 입력으로 검증했다.
- 기존 완료 결과만 GET으로 읽었다. 실행 ID `5b9fd53322824a569cb619990d35eb92`, 주문 `FOOD-20260907062729102`, 조회 판본343, 주체4명 좌표·업무 이벤트13개가 전후 동일하다. 새 주문·배차·이동을 실행한 검증이 아니다.
- Play 종료, canonical `SimulationWorldShell` clean, 원래 프로필5/저장 경로 null, Free Aspect/1.1x·창 크기 복원, 임시 관찰 카메라 제거를 확인했다. 원래 Scene/슬롯은 저장하거나 리셋하지 않았다.
- Console 최종 조회는 Error0·Warning12다. 미지정 스크립트10건과 Nature capability 대기/감각 표현 지연 각1건을 별도 미해결 경고로 남긴다. 이전 기준선과의 비교나 원인 수정은 하지 않았으므로 이 경고들이 이번 변경 전부터 있었다고 확정하지 않는다. 시험 정리 중 Pipeline 일시 불통은 정상 복귀 후 최종 시험을 확인했다.
- 최종 원본은 `artifacts/local/validation/observer-neighborhood-map-r3/final-unity-report.json`(SHA256 `995E1E5D1ED190BC28B0721932394EF3D2F829B68A566E27BE0EE1959905702F`)이다. 같은 폴더의 `source-sha256.json`에5소스·3신규 meta, 보고서에 캡처·입력·카메라 수치·서버 전후·복원값을 결속했다. 개발 통합은 현재8파일의 해시 일치와 `server-before.json`/`server-after.json` 전체 State 동등을 독립 재확인했다. 해당 코드/배치/카메라·기획 기준선이 바뀌면 영향받은 시험/화면 증거를 다시 검증한다.

## 사용과 다음 범위

기존 업무 서버 관찰 프로필6에서 Play하면 같은 서버 결과 위에 확장 동네가 나타난다. 휴대폰을 닫으면 전체 보기가 넓어지고 열면 남은 영역에 동네를 맞춘다. 연한 주택·음식점과 흰 마트·보충창고는 미연동 배경이다. 기존 로컬 생활 프로필5와 저장 슬롯도 그대로다.

다음 구현 후보는 확장 배경 일부를 실제 합성 음식점/주문자·거리 기반 배차 경로에 결속하고 여러 기사/주문이 반복되는 시나리오다. 이번에는 그 실행이나 자동 활성화를 하지 않았다. 전체 보기의 작은 NPC·도착 지점 이름표 겹침은 기존 역할 카드로 관찰하며 새 줌/따라가기, 기존 저장 지연, 전체 회귀 실패는 별도 보완 범위다. 실제 운영·Save/Replay 신규 증거·장시간 운행·신규 E 승격은 없다.

서버/DB·기존 저장 성능·전체 회귀의 현행 결과는 [r2 결과](observer-phone-result.r2.md)가 소유한다. 시각 기록은 [동네 맵 변경 기록](../../../../Changes/2026-09-07-observer-neighborhood-map.md)으로 연결한다. commit·push 없음.
