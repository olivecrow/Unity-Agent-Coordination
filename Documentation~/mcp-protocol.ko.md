# 통합 프로토콜

[English](mcp-protocol.md) | [한국어](mcp-protocol.ko.md)

## 전송 중립 API

안정적인 통합 표면은 `Olivecrow.UnityAgentCoordination.Editor` 어셈블리에 있습니다.

```csharp
CoordinationCommandResult result =
    UnityAgentCoordinationCommandRouter.ExecuteJson(jsonCommand);

string responseJson = result.ToJson();
```

Newtonsoft `JObject`를 받는 어댑터는 `Execute(JObject)`를 호출할 수 있습니다. 성공 결과는 다음 형태입니다.

```json
{
  "success": true,
  "message": "status completed",
  "data": {}
}
```

실패는 `success=false`, 안정적인 `error_code`를 포함하며 결과가 없으면 `data`를 생략합니다. 어댑터는 오류를 자유 형식 문자열로 바꾸지 말고 이 필드를 보존해야 합니다.

인자는 `snake_case`와 대응하는 `camelCase`를 모두 지원합니다. action 이름은 소문자 snake case입니다.

## Action

| Action | 호출 주체 | 필수 필드 | 선택 필드 |
|---|---|---|---|
| `status` | 모두 | 없음 | `agent_id`, `scopes` |
| `begin_work` | 모든 agent | `agent_id`, `role`, `worktree_path`, `head_sha` | `scopes`, `summary`, `work_kind`, `phase` |
| `heartbeat` | 등록 agent | `agent_id` | `head_sha`, `summary`, `work_kind`, `phase` |
| `claim` | 등록 agent | `agent_id`, 비어 있지 않은 `scopes` | 없음 |
| `release_claims` | 등록 agent | `agent_id` | `scopes`; 생략하면 전체 해제 |
| `enqueue_validation` | worker | `agent_id`, `candidate_sha` | `summary`, `test_modes`, `manual_checks` |
| `acquire_validation` | integrator | `agent_id`, `candidate_sha` | `request_id`; 생략하면 FIFO |
| `finish_validation` | lease 소유자 | `agent_id`, `request_id`, `passed` | `message` |
| `cancel_validation` | 요청 소유자 또는 integrator | `agent_id`, `request_id` | `message` |
| `finish_work` | 등록 agent | `agent_id` | 없음 |
| `relinquish_editor` | coordinator integrator | `agent_id`, `confirm=true` | 없음 |
| `clear_all` | coordinator integrator | `agent_id`, `confirm=true` | 없음 |

`role`은 `worker` 또는 `integrator`입니다. 패키지의 현재 프로젝트 경로와 일치하는 Editor만 integrator를 등록할 수 있으며 integrator는 하나만 존재할 수 있습니다.

`scopes`는 저장소 기준 상대 경로입니다. 동일 경로와 상위·하위 경로는 충돌합니다. 절대 경로와 `..`은 거부하며 Windows에서는 대소문자를 구분하지 않습니다.

`head_sha`와 `candidate_sha`는 40자리 Git commit SHA입니다. `acquire_validation`은 대기 중인 candidate, 요청한 candidate와 실제 canonical checkout HEAD를 비교하고 추적·미추적 변경이 있으면 거부합니다.

`test_modes`는 `EditMode`, `PlayMode`만 받고 배열 순서대로 실행합니다. agent 작업 `phase`는 보통 `inspecting`, `working`, `waiting_validation`, `validating`, `finalizing`, `blocked`를 사용합니다. 활성 검증은 `compiling`, `testing`, `awaiting_manual`, `ready_to_finish`을 사용합니다.

## 생명주기 예시

```json
{
  "action": "status",
  "agent_id": "task-42",
  "scopes": ["Assets/MyFeature"]
}
```

```json
{
  "action": "begin_work",
  "agent_id": "task-42",
  "role": "worker",
  "worktree_path": "C:/Projects/Game",
  "head_sha": "0123456789abcdef0123456789abcdef01234567",
  "scopes": ["Assets/MyFeature"],
  "work_kind": "code",
  "phase": "working",
  "summary": "Implement feature"
}
```

commit을 만든 뒤 worker가 `enqueue_validation`을 호출합니다. integrator는 exact SHA의 깨끗한 canonical checkout을 준비하고 `acquire_validation`을 호출합니다. phase가 `awaiting_manual` 또는 `ready_to_finish`이 되면 최종 Console과 수동 항목을 확인하고 `finish_validation`을 호출합니다. 마지막에는 각 agent가 `finish_work`를 호출합니다.

## 어댑터 요구 사항

MCP 어댑터는 다음 조건을 지켜야 합니다.

- 대상 Unity Editor 내부에서 실행하거나 호출을 그 Editor로 안전하게 전달합니다.
- 모든 명령 필드와 의미를 그대로 노출합니다.
- 공개 명령 라우터나 공개 coordination service만 호출하고 상태 파일에는 접근하지 않습니다.
- `CoordinationCommandResult` 필드를 그대로 직렬화합니다.
- 잘못된 Editor로 명령을 보내지 않도록 Unity 프로젝트나 instance를 식별합니다.
- canonical owner Editor 밖에서 검증을 획득하지 않습니다.

포함된 Coplay 어댑터는 다음을 제공합니다.

- tool: `manage_unity_agent_coordination`;
- resource: `get_unity_agent_coordination`;
- assembly: `Olivecrow.UnityAgentCoordination.CoplayMcp`;
- version gate: `com.coplaydev.unity-mcp` `[10.1.2,11.0.0)`.

연결된 HTTP 또는 stdio transport에서 Coplay tool과 resource discovery를 갱신합니다. Coplay의 instance, Editor state와 window resource는 canonical Editor 선택에 사용할 수 있지만 이 패키지의 이식 가능한 프로토콜에는 포함되지 않습니다.

## 주요 오류

| 오류 코드 | 의미 |
|---|---|
| `parameters_required` | 명령 객체가 없습니다. |
| `invalid_json` | JSON이 잘못되었거나 객체가 아닙니다. |
| `missing_parameter` | 필수 필드가 없거나 비어 있습니다. |
| `scope_conflict` | 다른 agent가 동일·상위·하위 scope를 소유합니다. |
| `integrator_exists` | 다른 integrator가 활성화되어 있습니다. |
| `validation_busy` | 다른 직렬 검증 lease가 활성화되어 있습니다. |
| `head_mismatch` | 대기열 SHA와 요청한 검증 SHA가 다릅니다. |
| `actual_head_mismatch` | 실제 Unity checkout HEAD와 요청 SHA가 다릅니다. |
| `tracked_worktree_dirty` | 검증 checkout에 추적 또는 미추적 변경이 있습니다. |
| `editor_busy` | Unity가 컴파일, 에셋 갱신 또는 Play Mode 전환 중입니다. |
| `not_coordinator_editor` | 다른 Unity Editor가 저장소 검증을 소유합니다. |
| `pending_validation` | agent가 아직 대기 또는 실행 중인 요청을 소유합니다. |

오류를 해결하기 위해 다른 agent의 claim을 삭제하거나 checkout 변경을 폐기하거나 coordination JSON을 직접 편집하지 않습니다.
