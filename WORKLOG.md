# STIGMA 작업 로그 (WORKLOG)

> 사용자는 **데스크탑(집)**과 **맥북(외부)** 두 환경에서 작업한다.
> Claude Code의 대화/로컬 메모리는 컴퓨터 간 공유되지 않으므로, 양쪽 Claude가
> 서로의 작업을 알 수 있도록 **모든 작업 내역을 여기에 기록**한다.
>
> - **최신 항목을 맨 위에** 추가한다.
> - 형식: `## YYYY-MM-DD — [머신] 제목` 아래에 변경 내용/이유를 적는다.
> - 세션 시작 시 이 파일을 먼저 읽어 다른 머신의 작업을 파악한다.
> - 작업 후 commit & push / 다른 머신에서는 pull 먼저.

---

## 2026-05-29 — [맥북] MCP 세션 자동연결 ON + 연결 절차 정정

- **증상**: 맥북에서 MCP 도구 호출이 `no_unity_session`으로 계속 실패. Unity MCP 창의 "Python" 빨간불.
- **원인**: 이 버전(CoplayDev unity-mcp **v9.6.6**)은 "서버"와 "세션"이 별개. 파이썬 HTTP 서버(8080)는 떠 있어도 **Unity↔서버 세션**이 따로 붙어야 함. HTTP transport는 `Auto-Start on Load` 토글이 **기본 꺼짐**이라 세션을 수동으로 열어야 했음. (stdio는 항상 자동.) Play 테스트 시 도메인 리로드로 세션이 끊기는 것도 원인.
- **조치**: `EditorPrefs MCPForUnity.AutoStartOnLoad = true` 로 설정 → 이제 에디터 열 때 서버+세션 자동 연결.
- **연결 절차 정정 (중요 — CLAUDE.md의 "Start Server/Start Bridge"는 v9에서 틀린 명칭)**:
  - MCP 창 열기: `Cmd+Shift+M` (메뉴: `Window → MCP For Unity → Toggle MCP Window`)
  - 수동 연결 시 Connection 섹션의 **`Start Session`** 클릭 (상태가 `Session Active (dead_hand)` 면 정상).
  - 세션 살았는지 확인: `mcpforunity://instances` 리소스의 `instance_count`.

## 2026-05-29 — [맥북] 인트로 컷씬 전면 재작업 (시네마틱)

- **목적**: 내일 학술대회 발표용 인게임 플레이 영상 — 인트로~전투 흐름을 다듬는 중. 그 첫 단계로 컷씬(인트로→노드맵) 연출이 너무 어색해서 전면 교체.
- **이미지 교체**: 기존 `CutScene_1.jpg`(2752×1400, 마을/사제 그림) → 새 `CutScene_1.png`(820×460=16:9, 후드 인물이 검은 태양을 바라보는 구도). **guid(`1c585463...`) 유지**한 채 에셋만 교체 → 씬의 Image 참조(`fileID 21300000`) 안 건드리고 그대로 연결됨.
- **`CutsceneManager.cs` 통째로 재작성**:
  - 카메라 좌표를 픽셀 매직넘버 → **이미지 정규화 좌표(-0.5~0.5)** 로. 런타임에 rect 크기로 환산(해상도 독립).
  - 동선: 와이드 → 후드인물 → 검은태양 → 눈(핵) 클로즈업 → 무너진 성채 → 와이드+피날레. **순간이동 컷 전부 제거**, 전부 ease-in-out + 정지 샷에도 느린 켄번스 드리프트.
  - 연출 오버레이 전부 코드 생성: 레터박스(2.35:1), 비네트(절차적 라디얼 텍스처), 필름 그레인(노이즈 RawImage), 붉은 시선 펄스, 번개 플래시(태양 등장), 화이트 피날레 플래시 + 카메라 셰이크.
  - 나레이션 6줄 새로 작성(STIGMA 테마: 응시/낙인). ESC 길게=스킵 게이지 기능 유지.
- **미검증**: 맥북 MCP 세션이 `no_unity_session`으로 계속 끊겨 **컴파일 에러 확인 / Play 테스트를 못 함**. Unity 콘솔에서 컴파일 에러 없는지 + Cutscene 씬 Play로 동선/박자 직접 확인 필요. 좌표·타이밍은 플레이 보면서 미세 튜닝 예정.

## 2026-05-29 — [맥북] Unity MCP 연결 방식 stdio → HTTP(8080) 정렬

- **증상**: Unity 창에서 "파이썬 로드 안 됨" / 기능 자체가 안 됨. `manage_scene get_active` → `No Unity Editor instances found`.
- **원인**: 맥북 Claude는 UnityMCP를 **stdio**로 등록 → Claude가 stdio 서버를 *따로* 띄움. 반면 **Unity는 HTTP 모드(`http://127.0.0.1:8080`)** 로 자기 서버를 띄우고 브리지를 거기 연결. 두 서버가 서로 다른 프로세스라 stdio 서버는 Unity를 `0 instances`로만 봄 = **전송 방식 불일치**.
- **조치**: 데스크탑 환경(HTTP 8080)과 동일하게 맞춤.
  - `claude mcp remove UnityMCP -s local`
  - `claude mcp add --transport http UnityMCP http://127.0.0.1:8080/mcp -s local`
- **주의**: 재등록 후 **Claude Code 세션 재시작 필요**(세션은 시작 시점 연결을 유지). 재시작 후 `get_active`로 검증할 것.

## 2026-05-29 — [맥북] 환경 세팅 & 협업 규칙 수립

- 맥북에서 처음 작업 시작 (기존엔 데스크탑 전용).
- **Unity MCP 연동**: `uv`/`uvx` 설치(`~/.local/bin`), UnityMCP 서버를 Claude Code에 등록 — `mcpforunityserver==9.6.6` (Unity 패키지 버전과 일치), stdio transport, local scope. `claude mcp get UnityMCP` → ✓ Connected.
  - 실제 Unity 조작은 에디터에서 `Window → MCP for Unity → Start Bridge` 켜야 동작.
- **협업 규칙 수립** (CLAUDE.md에 명문화): ①선컨펌 후작업 ②버그 단번에+원인설명 ③모르면 질문.
- **크로스 머신 작업 공유**: 이 `WORKLOG.md` 도입. 양쪽 환경에서 작업 내역을 여기에 기록하기로 함.
