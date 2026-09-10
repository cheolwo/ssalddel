# 동네 관찰·운영 휴대폰 r1

상태: Approved. 사용자는 동네 전체를 관찰·운영하는 휴대폰, 열린 동안 세계 진행 유지, 오른쪽 세로 패널을 선택하고 `Implement the proposed plan.`으로 구현을 승인했다. [30분 생활 r1](neighborhood-life.r1.md)과 [공간 기준 r3](mart-center.r3.md)은 변경하지 않는다.

## 범위와 인계

새 업무·WI·공식 Scene을 만들지 않는다. 기존 `neighborhood-life-*.e7-work-order.json` 23개의 공통 표현 보완이다. 각 WI의 권위 결과, Goal, 기획 hash, E0/승격 비활성은 유지한다. 휴대폰의 열기·닫기·앱 이동은 표현 상태이며 기존 정책 명령만 Simulation 상태를 바꾼다. 승인된 기본 프로필만 새 UI를 사용하고 r1~r4는 보존한다.

P0 실행·저장 조정과 화면 모델을 분리한다. Tick/정책/저장/재시작은 동시에 실행하지 않는다. 정책·저장 성공 뒤 이전 재생 상태를 유지하고 실패하면 정지하여 수동 저장 재시도를 요구한다. 모든 Tick의 저장을 유지하며 소요 시간을 측정한다. 앱 종료 시간을 따라잡지 않는다.

P1 오른쪽 하단 휴대폰 아이콘 → 세로 패널 → 동네·주문·NPC·창고·정책 앱. 홈·뒤로·닫기, 시간·실행 상태, 명시적 재생/정지/저장, 완료 뒤 확인을 거친 새 표본 시작을 제공한다. UI 입력은 세계로 전달하지 않는다. 패널 바깥에서는 기존 관찰이 가능하다.

P2 이름·상태·대기 사유를 한국어로 읽고 주문→담당 NPC→시설/보충 운송을 연결한다. 원장 ID는 개발 정보로만 노출한다. 대기 시간은 원본 시각이 있을 때만 표시한다. 편집 중 정책 초안은 Tick 갱신으로 덮지 않으며 적용 전에는 권위 상태가 바뀌지 않는다. 같은 UI 행을 재사용하고 사본 변경 때만 표시를 갱신한다.

P3 단위/회귀/Unity 컴파일·EditMode, 실제 입력·두 해상도 판독·30분 관찰을 각각 기록한다. View 캡처는 월드·공간·배치 담당에 대상과 절차를 인계하며 실행 불가 시 미검증으로 남긴다. 기존 저장 슬롯의 사용자 진행을 임의로 초기화하지 않는다. 장시간 검증은 별도 임시 저장 경로로 격리한다.

## 기존 E7 작업 명세의 영향 검토 보완

Logic E7→E1: 재진입/저장 실패 회복 → 명령 직렬화 → 같은 Session 재조회 → 기존 정책 입력 → 기존 상태/규칙/주체 불변. Presentation E7→E1: 실제 클릭·귀환 → 패널 입력 차단·초안 보존 → canonical Scene 런타임 결속 → uGUI/한국어 글꼴 후보 → 읽기 모델 → 원장 ID 연결 → 관찰자 의미. E1→E7은 역순으로 조립·검증하며 자동 E 승격하지 않는다.

표현 후보 `visual:neighborhood-observer-phone.r1`: 코드 생성 uGUI, 기본 OS 한국어 글꼴, 오른쪽 390 기준 폭·화면 높이 내 세로 패널. 대체는 같은 구성의 작은 화면 축소, 별도 Scene/외부 자산 없음. 입력 연결은 Canvas/GraphicRaycaster/EventSystem, 권위 연결은 기존 LocalSimulationRuntime Adapter다. 1280×720·1920×1080의 Bounds·판독·입력은 실제 검증 결과 전까지 미검증이다.

수정 경로: Hongdal `Ssalddel.Unity/Runtime/Observation`, `Ssalddel.Unity.Tests/동네관찰*`, 이 기획/결과/상태 문서. Unity `Assets/Ssalddel/Bootstrap/가상배달관찰Controller.cs`, `가상동네생활View.cs`, `동네관찰휴대폰Binding.cs`, `Infrastructure/Simulation/동네관찰RuntimeAdapter.cs`, `Presentation/World/동네관찰휴대폰View.cs`, 해당 시험. 공유 Controller는 기존 변경을 읽고 최소 합친다.

완료 판단은 코드·시험·Play Mode·Game View·실시간 관찰·서버 연결을 구분한다. 운영 DB/API·정산·지도·저장 형식 변경·commit·push는 범위 밖이다.
