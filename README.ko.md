# Unity Agent Coordination

[English](README.md) | [한국어](README.ko.md)

AI가 Unity Editor를 순서대로 사용하기 위한 번호표 도구입니다. **번호표 발급 → 자기 차례까지 대기 → Editor 사용 → 반납**만 수행하며, Unity 프로젝트마다 별도 대기열을 사용합니다.

## 요구 사항과 설치

Core와 Editor 어셈블리는 Unity와 기본 .NET API만 사용합니다. 필수 UPM 의존성, 외부 실행 파일, 버전 관리 메타데이터, 별도 작업 등록이 없습니다. Git·Unity Version Control·버전 관리를 사용하지 않는 프로젝트 모두 같은 방식으로 동작합니다.

Unity Package Manager에서 패키지 `.tgz`를 설치하거나, 이 폴더를 `Packages/com.olivecrow.unity-agent-coordination`에 복사한 뒤 다음 항목을 선언합니다.

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

패키지 import와 컴파일 후 해당 프로젝트의 `.agents/skills/unity-agent-coordination`에 Codex 스킬을 등록합니다. 사용자 수정은 보존하고 패키지가 관리하는 미수정 파일만 갱신하며, 정상적인 Package Manager 제거 시 정리합니다. [에이전트 설정](Documentation~/agent-setup.ko.md)을 참고하세요.

## 사용 순서

1. AI 작업마다 고유한 `session_id`를 만들고 `enqueue`를 호출합니다. `ticket_id`와 증가하는 번호를 받습니다. 같은 세션이 재시도하면 아직 보유한 번호표를 돌려줍니다.
2. `status`를 확인하며 `queue.next_ticket_id`가 자신의 번호표가 될 때까지 대기합니다.
3. 세션 ID와 번호표로 `acquire`를 호출하고 성공한 뒤에만 Editor를 사용합니다. 사용 중인 번호표가 있으면 다른 번호표는 점유할 수 없습니다.
4. Editor 작업을 모두 마치면 정리/finally 단계에서 `release`로 반납합니다. 그 다음 순서의 AI가 점유할 수 있습니다.

포기할 때는 `cancel`로 자기 번호표를 취소합니다. 다른 세션의 번호표를 점유·반납·취소할 수 없습니다. 번호표는 자동 만료하지 않습니다. 세션이 끊기면 해당 세션이 반납/취소하거나, 사용자가 이전 세션의 사용 종료를 확인한 뒤 `Window > AI Agent Coordination`에서 방치된 대기열을 명시적으로 초기화합니다.

이 도구는 협조하는 에이전트의 순서를 조정합니다. 다른 bridge 명령을 가로채거나 컴파일·테스트·빌드·버전 관리 작업을 자동 실행하지 않습니다. 상태는 `Library/UnityAgentCoordination/tickets.json`에 보존되어 컴파일과 domain reload를 지나도 유지됩니다. 프로세스 간 mutex와 원자적 파일 교체를 사용하며, 반납된 번호표는 제거하고 작은 번호 카운터만 유지합니다.

## AI 연결

공개 C#/JSON 진입점은 `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`입니다. 프로젝트에 이미 있는 Unity bridge가 JSON을 전달하면 되며 특정 MCP provider는 필수가 아닙니다.

선택형 Coplay 어댑터는 `manage_unity_agent_coordination`과 `get_unity_agent_coordination`을 제공합니다. Coplay `10.1.2` 이상 `11.0.0` 미만이 이미 설치된 경우에만 컴파일되며, 그 어댑터만 Coplay의 기존 JSON 의존성을 사용합니다. Core와 Editor에는 그 의존성이 없습니다.

다섯 가지 명령은 [통합 프로토콜](Documentation~/mcp-protocol.ko.md)을 참고하세요. `Tests`의 선택형 NUnit 테스트는 테스트 환경에서 실행하며 프레임워크를 제품의 필수 패키지로 설치하지 않습니다.

## 업그레이드와 검증

0.5 프로토콜은 이전 작업 등록·claim·integrator·커밋 검증 API를 대체합니다. 업그레이드 전에 기존 세션을 종료하고 번호표를 반납하세요. 이전 조정 상태는 새 프로젝트 대기열로 가져오지 않습니다.

Unity `6000.3.22f1` 참조 어셈블리 컴파일을 확인하고, 버전 관리 파일과 외부 실행 파일 없이 전체 JSON 번호표 생명주기를 격리 테스트합니다. 참조 컴파일과 격리 테스트는 실제 Unity import/callback 및 Unity Test Runner 검증을 대신하지 않습니다. 다른 Unity 버전은 검증하지 않았습니다.

## 라이선스

MIT. [LICENSE.md](LICENSE.md)를 참고하세요.
