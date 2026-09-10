# 음식점 로컬 관찰 카드와 코드 안내

- 구현 범위: [승인된 UI 연결 r1](../AI/Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/restaurant-observer-ui.r1.md).
- 안내: [웹·MAUI 대응과 실제 호출 경로·사용법](../Architecture/UnityClientLayeredArchitecture.md).
- 공통 코드: 읽기 전용 주문 표시 행·독립 모의 입력. 기존 정책 명령·수락·조리 규칙·저장 계약을 재사용한다.
- 별도 Unity: 음식점 선택 메뉴 → 단일 로컬 Runtime/Session → SceneController → Presenter → View. 자리·시간·새 조리 배정, 명시적 주문 추가·1 Tick·재조회·저장을 연결했다.
- 격리: 음식점 슬롯만 복원하며 자연 생존·턴 마감·물류·전투·공간 스트리밍의 기존 초기화는 음식점 프로필에서만 건너뛴다. 기본 프로필·기존 저장·Scene 파일·자산은 변경하지 않는다. 별도 Runtime을 화면마다 생성하지 않는다.
- 검증: .NET 음식점 집중 29/29·카드 8/8, 전체 Simulation 1713/1713·Unity 라이브러리 720/720, Fast 통과. 코드 지도 검사·세 솔루션 build 통과.
- 전체 Task: 범위 밖 `Ssalddel.Tests` 7건 실패(역할 API 분류 3건, Common/Admin 명명 1건, API 도입 이력 1건, 공식 음식재료 화면 1건, 통합 WebApp route 분류 1건). 해당 파일은 수정하지 않았다.
- Unity: 실제 프로젝트가 생성한 csproj와 Unity 참조로 Bootstrap 및 새 EditMode 시험 4개 소스까지 C# 컴파일 통과(기존 경고 존재). Pipeline 재컴파일 응답은 시간 초과했으며 중간 상태 조회 한 차례는 `completed/failed:false/errors:[]`였으나 최신 전체 판본의 Editor 검증 증거로 합산하지 않았다. 시험 호출도 시간 초과했고 조회 결과는 0건이라 성공으로 세지 않았다. CLI batch는 열린 Editor의 프로젝트 잠금으로 중단됐으며 강제 종료하지 않았다. 따라서 실제 EditMode 실행은 미검증이다.
- 로그: Hongdal `artifacts/local/validation/restaurant-observer/`, Task `artifacts/local/validation/20260906-174214/`, Fast `artifacts/local/validation/20260906-174425/`; Unity `artifacts/restaurant-observer/`.
- Play Mode·Game View·PNG·Hosted·운영 DB·기사 배차/이동·3D 조리는 미검증/범위 밖이다. E 승격·커밋·푸시는 하지 않았다.
