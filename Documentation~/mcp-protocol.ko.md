# Editor 번호표 프로토콜

명령은 대상 Unity Editor 안에서 실행하며, 그 프로젝트 폴더의 대기열을 사용합니다. 별도 작업 등록·리비전·역할·파일 scope·외부 실행 파일은 필요하지 않습니다.

## 보유 기간: 활성 대화 한 턴

번호표는 현재 활성 대화 턴 안에서만 보유합니다. 현재 Editor 작업이 끝나면 즉시 반납하고, 최종 응답으로 턴을 끝내거나 사용자의 응답·확인·승인을 기다리려고 작업을 멈추기 전에는 반드시 정리합니다. 전체 작업이 미완성이거나 같은 대화에서 계속 진행할 예정이어도 번호표를 유지할 수 없습니다. 현재 작업을 계속하는 동안의 진행 상황 메시지와 도구 실행 대기는 턴 종료가 아닙니다.

사용자에게 제어를 넘기기 전에 자신이 시작한 Editor 작업을 완료하거나 안전하게 중단한 뒤, 사용 중인 번호표는 `release`, 대기 중인 번호표는 `cancel`합니다. 실패한 경우에도 같으며, 대기 번호표를 반납하려고 먼저 점유하지 않습니다. 정리 성공 여부와 응답의 `queue.tickets`에서 자기 번호표가 제거되었는지 확인합니다. 응답이 없거나 실패했거나 결과가 불명확하면 `status`로 현재 상태를 확인한 뒤 필요한 경우 자기 번호표 정리를 재시도합니다. 끝내 정리할 수 없거나 확인이 불가능하면 Editor 사용을 멈추고 `session_id`, `ticket_id`, 정리 오류를 사용자에게 알립니다. 반납 완료나 대기열이 비었다고 단정하지 않습니다.

`session_id`는 여러 턴에서 유지할 수 있지만 번호표는 유지할 수 없습니다. Editor 작업을 재개할 때는 새 번호표를 발급받아 FIFO 순서를 다시 기다립니다. 이전 턴에서 남긴 자기 번호표가 있다면 먼저 정리와 제거 확인을 마치며, 이전 점유 권한이나 대기 순서를 재사용하지 않습니다. 패키지는 대화 턴 종료나 사용자 입력 대기를 감지할 수 없으므로 에이전트가 직접 정리해야 합니다.

## 명령

| 명령 | 필수 입력 | 결과 |
| --- | --- | --- |
| `status` | 없음 | 대기열, 사용 중인 번호표, 다음 번호표 |
| `enqueue` | `session_id` | `ticket.ticket_id`, `ticket.number`; `label`, `program`, `model`, `reasoning_effort`, `session_name`은 선택 |
| `acquire` | `session_id`, `ticket_id` | 성공한 뒤에만 Editor 사용 |
| `release` | `session_id`, `ticket_id` | 사용 중인 번호표 반납 |
| `cancel` | `session_id`, `ticket_id` | 사용자에게 제어를 넘기기 전 자기 대기 번호표 취소 또는 자기 번호표 포기 |

```json
{"action":"enqueue","session_id":"task-a","label":"씬 검사","program":"Codex","model":"Unknown","reasoning_effort":"Unknown","session_name":"Unknown"}
```

에이전트는 발급 시 프로그램, 실제 모델, 추론 수준, 대화 제목을 네 필드에 기입합니다. 확인할 수 없는 값은 추측하지 말고 `Unknown`으로 적습니다. 이 정보는 자동 감지가 아닌 에이전트의 자기 보고입니다. 기존 클라이언트와 대기열과의 호환성을 위해 필드는 선택 사항이며, 누락된 값은 Editor 창에서 Unknown으로 표시됩니다. 재시도는 기존 번호표를 반환하며 정보를 갱신하지 않습니다.

선택형 알림을 사용하려면 `enqueue` 전에 `pwsh -NoProfile -File .agents/skills/unity-agent-coordination/scripts/wait_for_turn.ps1 -ProjectPath <Unity-project-path> -Timeout 120`을 유지되는 셸 세션으로 실행합니다. Windows에서도 PowerShell 7의 `pwsh`를 사용합니다. 사용할 수 없거나 스크립트 실행이 차단되면 실행 정책을 변경하지 말고 상태 확인으로 대체합니다. 첫 JSON 줄의 `notification_token`을 발급 요청에 포함합니다. 발급 응답에서 이미 자기 차례라면 바로 점유를 시도합니다. `turn` 이벤트를 받으면 `status`로 확인한 뒤 `acquire`합니다. 알림은 보장이 없는 신호이며 점유 권한이 아닙니다. 시간 초과, 연결 종료, 도메인 재로드 또는 실패 시 `status`로 확인합니다. 새 수신기를 등록할 때는 기존 대기 번호표를 취소하고 새로 발급받습니다. 프로젝트 Library를 읽을 수 있어야 하며, 종료된 에이전트 턴은 재개되지 않습니다. 사용이 어려우면 기존 상태 확인을 사용합니다.

반환된 `ticket.ticket_id`를 저장하고 간격을 두어 `status`를 확인합니다. `queue.next_ticket_id`가 자신의 번호표인 경우에만 점유합니다.

```json
{"action":"acquire","session_id":"task-a","ticket_id":"<반환된 ID>"}
```

성공한 뒤 기존 Unity bridge로 현재 Editor 작업을 수행하고, 작업 성공/실패에 관계없이 정리 단계에서 사용 중인 번호표를 반납합니다. 턴을 끝내거나 사용자 입력을 기다리기 전에 반납해야 합니다.

```json
{"action":"release","session_id":"task-a","ticket_id":"<반환된 ID>"}
```

번호표가 아직 대기 중인데 턴을 끝내거나 사용자 입력을 기다리려고 작업을 멈추면 대신 취소합니다.

```json
{"action":"cancel","session_id":"task-a","ticket_id":"<반환된 ID>"}
```

응답은 `success`, `message`, 선택형 `error_code`를 포함합니다. 성공하면 `queue`와 필요한 경우 `ticket`이 포함됩니다. 사용 중인 번호표가 있으면 `next_ticket_id`가 생략됩니다. `not_your_turn`과 `editor_busy`는 현재 턴의 작업을 계속하는 동안 대기를 유지하라는 뜻이며, 사용자에게 제어를 넘기기 전에는 대기 번호표를 취소합니다. `not_ticket_owner`는 다른 세션의 번호표 조작을 금지합니다. 점유 실패는 번호표를 삭제하거나 순서를 바꾸지 않습니다. 같은 턴 안에서 소유 세션이 이미 점유한 번호표로 `acquire`를 재시도하는 것은 안전합니다.

세션마다 미반납 번호표 하나를 보유합니다. `enqueue` 재시도는 기존 번호표를 반환하지만, 이 재시도 동작이 여러 턴에 걸친 보유를 허용하지는 않습니다. 반납/취소 후 다시 요청하면 새 번호표가 나옵니다. 번호는 저장소 잠금 안에서 부여하므로 시각이 같아도 FIFO를 지킵니다. 사용 중인 번호표는 최대 하나입니다. 자동 만료·heartbeat·컴파일 실행기·테스트 실행기·별도 검증 대기열은 없습니다.

Unity 라우터는 컴파일·import·빌드·Play Mode 전환 중에 새 점유를 거절합니다. 안정된 Play Mode에서는 점유할 수 있습니다. domain reload를 지나도 대기열과 사용 중인 번호표를 유지하며 컴파일 때문에 자동 반납하지 않습니다.

어댑터는 `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`에 JSON을 전달하고 결과를 보존합니다. Core는 `CoordinationProtocol.ExecuteJson(json, service, editorReady)`로 호출할 수도 있습니다. 어댑터나 에이전트가 상태 파일을 직접 수정하지 않습니다. 다른 bridge 명령은 가로채지 않으므로 Editor 작업 묶음마다 소유권을 지켜야 합니다.

세션이 사라져도 번호표는 유지됩니다. 사용자는 기존 소유자가 Editor 사용을 멈췄는지 확인한 뒤 Editor 창의 **Reset Abandoned Queue**를 사용할 수 있습니다. 이 복구 작업은 AI 명령으로 제공하지 않습니다. 잘못된 상태 파일은 소유권을 조용히 폐기하지 않고 오류로 중단합니다.
