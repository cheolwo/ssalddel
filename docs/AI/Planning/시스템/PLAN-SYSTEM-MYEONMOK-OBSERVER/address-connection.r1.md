# 면목동 주소 자료 → 기존 사가정 건물 참고 연결 r1

- 승인: 2026-09-08 면목동 수집 결과를 받은 사용자의 `한번 연결해 봐` 요청. 이번은 기존 지도에 비공개 자료를 연결하는 읽기 전용 참고 표현이다. NPC/주문/입주·통행 권위나 E 승격은 포함하지 않는다.
- 입력: [수집 결과](../../../../Reports/면목동-주소기반-공간자료-집중수집-2026-09-08.md)의 DB 재조회 사본 2,006행·1,395주소 묶음과 기존 `SagajeongReference.json` r3의 602건물. 각각 SHA256 `9811BEE6CFD67B741CBD47830325C8408C3499390737AED63E960CC22645E536` / `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`에 동결한다.
- 규칙: 기존 도로명주소 정규화 재사용 → 주소 속성이 일치하는 OSM 건물이 정확히 하나인 경우만 참고 연결. 주소 없음·일치 없음·복수 후보는 각각 미연결 보존. 면목동 전체/두 역 전체가 현재 사가정 1km 안에 있다고 해석하지 않는다. 주소 일치가 현재 입주·영업·출입구를 증명하지 않는다.
- 표시: 기존 사가정 조립 경로에 선택적 비공개 로컬 파일 읽기를 추가한다. 새 Resources에 개인 자료를 배포하지 않고 Editor에서만 고정 artifact를 읽는다. 기존 건물·도로는 불변, 표시 위치는 외곽 경계상자의 기호 기준점이며 실제 출입구/중심점/고도 아님. 병의원 비정상·폐업은 기본 현재목록에서 제외하고 이력임을 구분한다. 음식점 영업상태는 미확인으로 표시한다.
- 쓰기 소유: Hongdal `eng/Ssalddel.PublicDataPortalImport/면목동주소공간연결.cs`, 기존 Program dispatcher, README, 이 문서·전용 결과 보고·CURRENT. Unity `Assets/Ssalddel/Runtime/World/주소공간연결자료.cs`, `Bootstrap/면목동주소연결View.cs`, 기존 `사가정공간참고View.cs`의 연결 호출1곳, `Tests/EditMode/주소공간연결Tests.cs`와 새 파일 meta만. 기존 타 담당 변경은 보존한다.
- 출력: Hongdal `artifacts/local/public-data/myeonmok-spatial-link-20260908-r1/connection.json` CreateNew, 원본·DB 변경0. 실제 원본 hash/연결 대상/미연결 이유·자료상태·출처를 보존하고 동일 입력 결정성을 시험한다.
- 검증: 주소 정규화·복수 후보·결측·중복·hash/판본 drift·입력 불변·결정성·폐업 분리·기존 좌표 재사용. Unity 컴파일/EditMode를 우선한다. Scene 저장·Play/캡처는 이번 기본 완료 조건이 아니며 실제 미실행은 별도로 보고한다. Editor에 다른 실행이 있으면 중단시키지 않는다.
- 제외: 새 좌표 변환/지오코딩/Provider/키/공개 API/DB schema/입주 확정/새 건물/출입구·경로/Save/Scene/미소유 dirty 저장/기존 중지 작업·자동화 재개/commit·push.
