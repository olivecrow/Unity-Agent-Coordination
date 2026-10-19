# Editor 번호표 프로토콜

명령은 대상 Unity Editor 안에서 실행하며, 그 프로젝트 폴더의 대기열을 사용합니다. 별도 작업 등록·리비전·역할·파일 scope·외부 실행 파일은 필요하지 않습니다.

| 명령 | 필수 입력 | 결과 |
| --- | --- | --- |
| `status` | 없음 | 대기열, 사용 중인 번호표, 다음 번호표 |
| `enqueue` | `session_id` | `ticket.ticket_id`, `ticket.number`; `label`은 선택 |
| `acquire` | `session_id`, `ticket_id` | 성공한 뒤에만 Editor 사용 |
| `release` | `session_id`, `ticket_id` | 사용 중인 번호표 반납 |
| `cancel` | `session_id`, `ticket_id` | 자기 대기/사용 번호표 포기 |

```json
{"action":"enqueue","session_id":"task-a","label":"씬 검사"}
```

반환된 `ticket.ticket_id`를 저장하고 간격을 두어 `status`를 확인합니다. `queue.next_ticket_id`가 자신의 번호표인 경우에만 점유합니다.

```json
{"action":"acquire","session_id":"task-a","ticket_id":"<반환된 ID>"}
```

성공한 뒤 기존 Unity bridge로 작업하고, 작업 성공/실패에 관계없이 정리 단계에서 반납합니다.

```json
{"action":"release","session_id":"task-a","ticket_id":"<반환된 ID>"}
```

응답은 `success`, `message`, 선택형 `error_code`를 포함합니다. 성공하면 `queue`와 필요한 경우 `ticket`이 포함됩니다. 사용 중인 번호표가 있으면 `next_ticket_id`가 생략됩니다. `not_your_turn`과 `editor_busy`는 대기를 유지하라는 뜻이고, `not_ticket_owner`는 다른 세션의 번호표 조작을 금지합니다. 점유 실패는 번호표를 삭제하거나 순서를 바꾸지 않습니다. 소유 세션이 이미 점유한 번호표로 `acquire`를 재시도하는 것은 안전합니다.

세션마다 미반납 번호표 하나를 보유합니다. `enqueue` 재시도는 기존 번호표를 반환하며, 반납/취소 후 다시 요청하면 새 번호표가 나옵니다. 번호는 저장소 잠금 안에서 부여하므로 시각이 같아도 FIFO를 지킵니다. 사용 중인 번호표는 최대 하나입니다. 자동 만료·heartbeat·컴파일 실행기·테스트 실행기·별도 검증 대기열은 없습니다.

Unity 라우터는 컴파일·import·빌드·Play Mode 전환 중에 새 점유를 거절합니다. 안정된 Play Mode에서는 점유할 수 있습니다. domain reload를 지나도 대기열과 사용 중인 번호표를 유지하며 컴파일 때문에 자동 반납하지 않습니다.

어댑터는 `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`에 JSON을 전달하고 결과를 보존합니다. Core는 `CoordinationProtocol.ExecuteJson(json, service, editorReady)`로 호출할 수도 있습니다. 어댑터나 에이전트가 상태 파일을 직접 수정하지 않습니다. 다른 bridge 명령은 가로채지 않으므로 Editor 작업 묶음마다 소유권을 지켜야 합니다.

세션이 사라져도 번호표는 유지됩니다. 사용자는 기존 소유자가 Editor 사용을 멈췄는지 확인한 뒤 Editor 창의 **Reset Abandoned Queue**를 사용할 수 있습니다. 이 복구 작업은 AI 명령으로 제공하지 않습니다. 잘못된 상태 파일은 소유권을 조용히 폐기하지 않고 오류로 중단합니다.
