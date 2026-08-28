# 에이전트 설정

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

이 패키지는 에이전트 운영 규칙과 호출 가능한 Unity 전송 어댑터가 모두 필요합니다. 문서만으로 MCP 도구가 등록되지는 않습니다.

## Codex

`Skills~/unity-agent-coordination`을 Codex 스킬 위치에 복사하거나 연결하고 프로젝트 `AGENTS.md`에서 해당 스킬을 안내합니다. 포함된 스킬에는 적용 범위, 패키지 bootstrap, canonical Editor 정책, 검증 생명주기, 상한이 있는 대기와 정리 절차가 들어 있습니다.

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

## 패키지 Bootstrap

포함된 Codex 스킬은 작업이 Editor 영향 적용 범위를 넘은 뒤에만 패키지를 확인합니다. `com.olivecrow.unity-agent-coordination`이 이미 설치 또는 선언되어 있으면 기존 소스와 버전을 바꾸거나 업그레이드하지 않고 그대로 사용합니다. embedded package 폴더만 있다면 로컬 `file:` 의존성을 추가하고, 둘 다 없다면 다음 태그가 지정된 공개 Git 의존성을 추가합니다.

```json
"com.olivecrow.unity-agent-coordination": "https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0"
```

에이전트는 `Packages/manifest.json`의 다른 항목을 보존하고 `Packages/packages-lock.json`은 Unity Package Manager가 관리하게 둡니다. 패키지 설치는 그전에는 coordination API가 존재하지 않기 때문에 허용하는 일회성 bootstrap 예외입니다. 의존성을 수정하기 전에 실행 중인 Unity 프로세스를 확인하고 두 번째 Editor를 실행하지 않으며, package resolution이 끝난 다음 `status`를 첫 coordination action으로 호출합니다.

이 패키지를 설치해도 MCP provider가 함께 설치되거나 선택되지는 않습니다. 에이전트는 프로젝트에 이미 있는 Unity MCP 어댑터를 사용합니다. MCP 구현을 추가하거나 교체하는 작업은 별도의 사용자 승인이 필요합니다.

## 어댑터 확인

지침을 적용하기 전에 선택한 Unity MCP가 coordination 어댑터를 노출하는지 확인합니다.

- Coplay는 재연결 후 `manage_unity_agent_coordination`과 선택형 상태 resource를 노출해야 합니다.
- 다른 MCP는 같은 action payload를 `UnityAgentCoordinationCommandRouter`로 전달하는 동등한 tool을 제공해야 합니다.
- 일반 Editor C# 실행만 제공한다면 라우터를 호출하고 직렬화된 결과를 반환하는 작은 bridge를 만듭니다.
- 호출 경로가 없다면 어댑터를 먼저 추가합니다. `%LOCALAPPDATA%` 상태를 직접 읽거나 쓰지 않습니다.

명령 schema는 [통합 프로토콜](mcp-protocol.ko.md)을 참고합니다.
