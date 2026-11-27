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

**번호표는 활성 대화 한 턴 안에서만 보유합니다.** 현재 Editor 작업이 끝나면 즉시 반납하고, 최종 응답으로 턴을 끝내거나 사용자의 응답·확인·승인을 기다리려고 작업을 멈추기 전에는 반드시 정리합니다. 같은 대화에서 작업을 계속할 예정이어도 같습니다. 현재 작업을 계속하는 동안의 진행 상황 메시지와 도구 실행 대기는 턴 종료가 아닙니다. `session_id`는 여러 턴에서 유지할 수 있지만 번호표는 유지할 수 없습니다.

1. AI 작업마다 고유한 `session_id`를 만듭니다. `enqueue`를 호출하기 전에 `program`, `model`, `reasoning_effort`, `session_name`을 조회하고 네 필드를 모두 보냅니다. 런타임과 호스트 세션 정보를 확인한 뒤에도 확인할 수 없는 값만 `Unknown`으로 적습니다. 점유 전에 반환된 번호표에 제출한 값이 기록됐는지 확인합니다. 같은 세션이 재시도하면 처음 기록한 정보가 담긴 기존 번호표를 받습니다.
2. `queue.next_ticket_id`가 자신의 번호표가 될 때까지 대기합니다. 발급 전에 선택형 PowerShell 7 수신기를 시작하면 차례 알림을 받을 수 있습니다. 알림이나 시간 초과 후 `status`로 확인합니다. [프로토콜](Documentation~/mcp-protocol.ko.md)을 참고하세요.
3. 세션 ID와 번호표로 `acquire`를 호출하고 성공한 뒤에만 Editor를 사용합니다. 사용 중인 번호표가 있으면 다른 번호표는 점유할 수 없습니다.
4. 정리/finally 단계에서 자신이 시작한 Editor 작업을 완료하거나 안전하게 중단한 뒤, 사용 중인 번호표는 `release`, 대기 중인 번호표는 `cancel`합니다. 실패한 경우에도 같으며, 턴을 끝내거나 사용자 입력을 기다리기 전에 정리합니다. 그 다음 순서의 AI가 점유할 수 있습니다.
5. 정리 성공 여부와 응답의 `queue.tickets`에서 자기 번호표가 제거되었는지 확인합니다. 응답이 없거나 실패했거나 결과가 불명확하면 `status`로 현재 상태를 확인한 뒤 필요한 경우 정리를 재시도합니다. 미해결 상태라면 세션/번호표 ID와 오류를 보고하며, 확인 없이 반납했다고 말하지 않습니다.
6. 같은 대화라도 Editor 작업을 재개할 때는 새 번호표를 발급받아 FIFO 순서를 다시 기다립니다. 이전 턴에서 남긴 자기 번호표가 있다면 먼저 정리와 제거 확인을 마칩니다.

포기할 때는 `cancel`로 자기 번호표를 취소합니다. 다른 세션의 번호표를 점유·반납·취소할 수 없습니다. 번호표는 자동 만료하지 않습니다. 사용자는 `Window > AI Agent Coordination`에서 **Cancel Ticket**으로 번호표 하나를 취소하거나 **Reset Abandoned Queue**로 대기열 전체를 초기화할 수 있습니다. 사용 중인 번호표를 취소하기 전에는 해당 세션이 Editor 사용을 끝냈는지 확인합니다.

이 도구는 협조하는 에이전트의 순서를 조정합니다. 대화 턴 종료나 사용자 입력 대기를 감지할 수 없으므로 에이전트가 직접 번호표를 정리해야 합니다. 다른 bridge 명령을 가로채거나 컴파일·테스트·빌드·버전 관리 작업을 자동 실행하지 않습니다. 상태는 `Library/UnityAgentCoordination/tickets.json`에 보존되어 컴파일과 domain reload를 지나도 유지됩니다. 프로세스 간 mutex와 원자적 파일 교체를 사용하며, 반납된 번호표는 제거하고 작은 번호 카운터만 유지합니다.

## AI 연결

공개 C#/JSON 진입점은 `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`입니다. 프로젝트에 이미 있는 Unity bridge가 JSON을 전달하면 되며 특정 MCP provider는 필수가 아닙니다.

선택형 Coplay 어댑터는 `manage_unity_agent_coordination`과 `get_unity_agent_coordination`을 제공합니다. Coplay `10.1.2` 이상 `11.0.0` 미만이 이미 설치된 경우에만 컴파일되며, 그 어댑터만 Coplay의 기존 JSON 의존성을 사용합니다. Core와 Editor에는 그 의존성이 없습니다.

다섯 가지 명령은 [통합 프로토콜](Documentation~/mcp-protocol.ko.md)을 참고하세요. `Tests`의 선택형 NUnit 테스트는 테스트 환경에서 실행하며 프레임워크를 제품의 필수 패키지로 설치하지 않습니다.

## 업그레이드와 검증

0.5 프로토콜은 이전 작업 등록·claim·integrator·커밋 검증 API를 대체합니다. 업그레이드 전에 기존 세션을 종료하고 번호표를 반납하세요. 이전 조정 상태는 새 프로젝트 대기열로 가져오지 않습니다.

번호표 생명주기를 격리 테스트합니다. 선택형 알림 수신기에는 PowerShell 7과 유지되는 셸 세션이 필요하며 기존 상태 확인도 사용할 수 있습니다. Unity `6000.6.8f1`에서 Codex와 Antigravity의 실제 알림 전달을 확인했습니다. 다른 Unity 버전과 플랫폼의 알림 전달은 검증하지 않았으며, 격리 테스트는 Unity Test Runner 검증을 대신하지 않습니다.

## 라이선스

MIT. [LICENSE.md](LICENSE.md)를 참고하세요.
