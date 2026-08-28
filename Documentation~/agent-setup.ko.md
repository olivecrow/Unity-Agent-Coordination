# 에이전트 설정

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

이 패키지는 에이전트 운영 규칙과 호출 가능한 Unity 전송 어댑터가 모두 필요합니다. 문서만으로 MCP 도구가 등록되지는 않습니다.

## Codex

`Skills~/unity-agent-coordination`을 Codex 스킬 위치에 복사하거나 연결하고 프로젝트 `AGENTS.md`에서 해당 스킬을 안내합니다. 포함된 스킬에는 적용 범위, canonical Editor 정책, 검증 생명주기, 상한이 있는 대기와 정리 절차가 들어 있습니다.

## Claude Code와 다른 에이전트

프로젝트 에이전트 지침에 짧은 라우팅 규칙을 추가하고 이 패키지 문서를 연결합니다. 예시는 다음과 같습니다.

```markdown
공유 Unity Editor를 사용하거나 자동 import를 일으킬 수 있는 작업은 첫 Editor
영향 작업 전에 Unity Agent Coordination 지침을 읽는다. coordination 어댑터의
status를 먼저 호출하고 path claim과 활성 validation lease를 지키며 완료 시
finish_work를 호출한다. Git 전용 또는 파일시스템 전용 작업에는 적용하지 않고
coordination 상태 파일을 직접 편집하지 않는다.
```

`AGENTS.md`, `CLAUDE.md` 또는 사용하는 에이전트의 프로젝트 지침 파일에 넣을 수 있습니다. 표현은 바꿔도 적용 범위와 정리 계약은 유지합니다.

## 어댑터 확인

지침을 적용하기 전에 선택한 Unity MCP가 coordination 어댑터를 노출하는지 확인합니다.

- Coplay는 재연결 후 `manage_unity_agent_coordination`과 선택형 상태 resource를 노출해야 합니다.
- 다른 MCP는 같은 action payload를 `UnityAgentCoordinationCommandRouter`로 전달하는 동등한 tool을 제공해야 합니다.
- 일반 Editor C# 실행만 제공한다면 라우터를 호출하고 직렬화된 결과를 반환하는 작은 bridge를 만듭니다.
- 호출 경로가 없다면 어댑터를 먼저 추가합니다. `%LOCALAPPDATA%` 상태를 직접 읽거나 쓰지 않습니다.

명령 schema는 [통합 프로토콜](mcp-protocol.ko.md)을 참고합니다.
