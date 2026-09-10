# 사가정역 중심 공간 참고 확장 r2

- 승인: 2026-09-08 사용자 요청. 기존 도형 동네를 버리지 않고 같은 Unity 세계에 사가정역 중심 공간을 표현한다. 이번 1km는 반경이 아닌 **1km × 1km 정사각형**(역에서 동서남북 각각 500m)이다.
- r1의 시장 중심을 OpenStreetMap 사가정역 노드 6046633864, version 5, 위도 37.5806971·경도 127.0884106으로 변경했다. 기존 합성 동네와 배달 경로는 이동하거나 지우지 않았다. 지역 내 실제 상대 거리는 WGS84 ECEF→ENU, 1 Unity 단위=1m이며 세계 배치 중심 (550,8)은 게임상의 평행이동이다.
- 출처: [역 노드](https://www.openstreetmap.org/node/6046633864), [이용조건·저작자 표시](https://www.openstreetmap.org/copyright). © OpenStreetMap contributors · ODbL 1.0. 정부 측량 성과가 아닌 공개 공동편집 지리 참고 자료다.
- 원문: `artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm`. 수집 bbox 127.0825,37.5762,127.0942,37.5854, SHA256 `3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3`. 원문 hash와 역 좌표를 검사하는 `eng/neighborhood/build-sagajeong-reference.ps1`이 Unity `Resources/SagajeongReference.json`을 생성한다. 결과에는 출처·식별자·판본·변환법·높이 근거를 보존하고 기여자 개인정보 태그는 복사하지 않는다.
- 건물 외곽 602개, 도로선분 2,397개. 경계 밖/불완전/복합 외곽 건물 93개와 경계 밖 선분 887개는 제외했다. 교량·터널·공사/계획 도로, 복합 내곽은 이번 표현에서 제외한다. 빈 곳은 실제 공터가 아닌 자료 미포함일 수 있다.
- 건물은 단순 입체 외곽으로 표시한다. 해석 가능한 원문 높이를 사용하고 미상 높이는 기호값 4m이다. 도로 폭도 판독용 기호값이다. 실제 지형·층수·출입구·차량 접근성·길찾기·업무 권위를 추정하지 않는다.
- Unity `사가정공간참고View.cs`는 기존 프로필5의 같은 관찰 Root에 결합 메시 두 개로 조립한다. 별도 Scene·프로필·Map Manager·DB를 만들지 않았다. 시장 표식은 r1과 같은 참고 지위이며 검토보류 원장의 공개/업무 승격이 아니다.
- 실제 Play에서 도로·건물 메시의 카메라 레이어 누락을 발견하여 기존 관찰 레이어31·표현 셰이더로 수정했다. 재컴파일 오류0 후 다시 Play하고 관찰 카메라 출력 PNG를 확인했다. 캡처는 UI 전체 스크린샷이 아닌 실제 Play 카메라 렌더다.
- 화면: Unity 저장소 `Assets/Documentation/Changes/2026-09-08-sagajeong/overview.png`. 프로필5, canonical `SimulationWorldShell`, 같은 Root 85자식, 격리 저장 경로, Tick0/Revision0에서 공간을 확인했다. NPC 배송 완주·5분 실행·저장 회귀·새 도로 이동은 이번 실행 증거에 포함하지 않는다. Scene 저장·E 승격·commit·push 없음.
- Unity에서 좌하단 `사가정역 · 1km 확장 보기`로 확장 개요를 볼 수 있다. 기존 동네 집중으로 복귀할 수 있다. Play 화면은 관찰을 위해 열어 둔다.
