# 에이전트 설정

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

이 패키지는 에이전트 운영 규칙과 호출 가능한 Unity 전송 어댑터가 모두 필요합니다. 문서만으로 MCP 도구가 등록되지는 않습니다.

## Codex

Unity에서 이 패키지를 임포트하고 컴파일하면 Editor 초기화가 포함된 스킬과 참조 파일을 `<UnityProject>/.agents/skills/unity-agent-coordination`에 복사합니다. Codex는 해당 프로젝트에서 작업할 때 이 프로젝트 전용 폴더를 자동 탐색합니다. 사용자 전역 설치나 `AGENTS.md` 자동 수정은 필요하지 않습니다. [Codex 스킬 경로](https://developers.openai.com/codex/skills#where-codex-loads-local-skills)를 참고하세요.

`.package-owned` 파일은 설치한 파일의 해시를 기록합니다. 이후 Editor 초기화에서는 사용자가 수정하지 않은 파일만 갱신하며 동일한 내용은 다시 쓰지 않습니다. 기존 비관리 스킬이나 사용자 수정은 Console 경고와 함께 그대로 보존합니다. Unity Package Manager로 패키지를 제거하면 수정되지 않은 패키지 관리 파일만 정리하고 다른 파일은 보존합니다. Unity가 닫힌 상태에서 패키지를 제거했거나 사용자 수정으로 정리가 중단됐다면 필요에 따라 남은 프로젝트 스킬 폴더를 직접 정리합니다. 남은 스킬도 패키지가 없으면 작업을 중단합니다.

기존 전역 설치를 전환할 때는 먼저 프로젝트 스킬이 생성됐는지 확인하고 사용자 스킬 폴더의 `unity-agent-coordination`만 제거하거나 백업합니다. 다른 전역 스킬은 유지합니다. 생성 파일은 Git에 그대로 표시되므로 clean exact-SHA 검증을 요청하기 전에 검토하여 커밋하거나 ignore 처리합니다.

자동 등록은 `v0.4.0`부터 제공됩니다. 기존 `v0.2.0`과 `v0.3.0` 태그에는 없으므로 해당 버전에서는 `Skills~/unity-agent-coordination`을 프로젝트의 `.agents/skills`에 직접 복사합니다. 새 스킬이 Codex에 표시되지 않으면 Codex를 다시 시작합니다.

## Claude Code와 다른 에이전트

프로젝트 에이전트 지침에 짧은 라우팅 규칙을 추가하고 이 패키지 문서를 연결합니다. 예시는 다음과 같습니다.

```markdown
공유 Unity Editor를 사용하거나 자동 import를 일으킬 수 있는 작업은 첫 Editor
영향 작업 전에 Unity Agent Coordination 지침을 읽는다. coordination 어댑터의
status를 먼저 호출하고 고유 세션을 등록하며 path claim을 지킨다. Editor 상태
작업 전에 점유를 대기열에 넣고, clean commit candidate에만 exact-SHA 검증을
사용하며 완료 시 finish_work를 호출한다. Git 전용 또는 파일시스템 전용 작업에는
적용하지 않고 coordination 상태 파일을 직접 편집하지 않는다.
```

`AGENTS.md`, `CLAUDE.md` 또는 사용하는 에이전트의 프로젝트 지침 파일에 넣을 수 있습니다. 표현은 바꿔도 적용 범위와 정리 계약은 유지합니다.

## 프로젝트 패키지 범위

프로젝트 스킬은 해당 Unity 프로젝트에 이미 설치된 패키지를 사용합니다. 의존성을 추가하거나 업그레이드하지 않으며, 제거된 패키지를 복구하거나 전역 스킬을 생성하지 않습니다. 패키지가 없으면 중단합니다. 조정 규칙을 적용할 때 기존 패키지 소스와 버전을 유지합니다.

이 패키지를 설치해도 MCP provider가 함께 설치되거나 선택되지는 않습니다. 에이전트는 프로젝트에 이미 있는 Unity MCP 어댑터를 사용합니다. MCP 구현을 추가하거나 교체하는 작업은 별도의 사용자 승인이 필요합니다.

## 어댑터 확인

지침을 적용하기 전에 선택한 Unity MCP가 coordination 어댑터를 노출하는지 확인합니다.

- Coplay는 재연결 후 `manage_unity_agent_coordination`과 선택형 상태 resource를 노출해야 합니다.
- 다른 MCP는 같은 action payload를 `UnityAgentCoordinationCommandRouter`로 전달하는 동등한 tool을 제공해야 합니다.
- 일반 Editor C# 실행만 제공한다면 라우터를 호출하고 직렬화된 결과를 반환하는 작은 bridge를 만듭니다.
- 호출 경로가 없다면 어댑터를 먼저 추가합니다. `%LOCALAPPDATA%` 상태를 직접 읽거나 쓰지 않습니다.

명령 schema는 [통합 프로토콜](mcp-protocol.ko.md)을 참고합니다.

어댑터는 세션과 Editor 점유 필드를 모두 노출해야 합니다. 이전 패키지 버전은 `session_id`나 `*_editor_access` action을 인식하지 않을 수 있으므로 에이전트는 action schema 또는 status state가 보고하는 schema version(포함된 라우터 응답에서는 `State.SchemaVersion`)으로 지원 여부를 확인하고, 검증 대기열을 일반적인 commit 전 점유 대기열처럼 사용하면 안 됩니다.
