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

## 2026-05-29 — [맥북] 환경 세팅 & 협업 규칙 수립

- 맥북에서 처음 작업 시작 (기존엔 데스크탑 전용).
- **Unity MCP 연동**: `uv`/`uvx` 설치(`~/.local/bin`), UnityMCP 서버를 Claude Code에 등록 — `mcpforunityserver==9.6.6` (Unity 패키지 버전과 일치), stdio transport, local scope. `claude mcp get UnityMCP` → ✓ Connected.
  - 실제 Unity 조작은 에디터에서 `Window → MCP for Unity → Start Bridge` 켜야 동작.
- **협업 규칙 수립** (CLAUDE.md에 명문화): ①선컨펌 후작업 ②버그 단번에+원인설명 ③모르면 질문.
- **크로스 머신 작업 공유**: 이 `WORKLOG.md` 도입. 양쪽 환경에서 작업 내역을 여기에 기록하기로 함.
