# Unity Agent Coordination

[English](index.md) | [한국어](index.ko.md)

## 목적

이 패키지는 병렬 파일 소유권과 직렬 Unity Editor 검증을 분리합니다. worker는 저장소 기준 상대 경로를 claim하고 겹치지 않는 범위에서 동시에 작업합니다. 하나의 canonical Editor만 검증 lease를 소유하며 대기 중인 commit의 컴파일, Unity 테스트, 검증용 Play Mode와 직렬화 변경 확인을 수행합니다.

패키지는 claim, 단일 integrator, 단일 활성 검증 lease, exact Git SHA와 깨끗한 검증 checkout을 강제합니다. 조정을 시작하는 시점, 사용할 Editor와 새 Editor를 실행하지 말아야 하는 조건은 에이전트 지침이 적용합니다.

## 호환성과 의존성

- Unity `6000.3.22f1`에서 테스트했습니다.
- `package.json`에 최소 `unity` 버전을 선언하지 않았습니다. 다른 Unity 버전에서도 설치는 허용될 수 있지만 호환을 보장하지 않습니다.
- 필수: `com.unity.test-framework`, `com.unity.nuget.newtonsoft-json`.
- 선택: `com.coplaydev.unity-mcp`. 포함된 어댑터는 `10.1.2` 이상 `11.0.0` 미만에서 컴파일되며 `10.1.2`에서 테스트했습니다.
- 조정 기능은 Git 저장소가 필요합니다. 별도 Git worktree는 선택 사항이며 Unity 프로젝트에서 기본 생성하지 않습니다.

## 설치

Unity Package Manager에서 태그가 지정된 패키지를 설치합니다.

```text
https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0
```

embedded package로 사용할 때는 이 폴더를 `Packages/com.olivecrow.unity-agent-coordination`으로 복사하고 다음 항목을 추가합니다.

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

조정 패키지를 설치해도 MCP 구현은 설치되지 않습니다. 포함된 어댑터를 사용하려면 Coplay를 별도로 설치하고, 다른 Unity bridge를 사용한다면 그 bridge용 어댑터를 구현합니다.

## 통합 계층

패키지는 두 계층을 독립적으로 둡니다.

1. `UnityAgentCoordinationCommandRouter`가 C# 또는 JSON으로 공통 action schema를 받고 직렬화 가능한 `CoordinationCommandResult`를 반환합니다.
2. MCP별 어댑터가 이 라우터를 AI 에이전트에 노출합니다. 포함된 Coplay 어셈블리는 Coplay attribute를 사용하며 Coplay가 없으면 컴파일에서 제외됩니다.

다른 MCP도 Coplay 의존 없이 같은 명령 계약을 사용할 수 있습니다. 어댑터는 대상 Unity Editor 내부에서 실행되고, 명령 객체 전체를 전달하며, 안정적인 오류 코드와 응답 데이터를 보존하고, 상태 파일에 직접 접근하지 않아야 합니다. 자세한 내용은 [통합 프로토콜](mcp-protocol.ko.md)을 참고합니다.

MCP에는 임의 Unity 패키지 도구를 공통 방식으로 자동 발견하는 규격이 없습니다. 에이전트에게 문서를 읽으라고 지시하는 것만으로 지원하지 않는 MCP에 명령이 생기지는 않습니다. 에이전트 지침과 전송 어댑터가 모두 필요합니다.

## 적용 범위

공유 live Unity Editor를 사용하거나 영향을 줄 첫 작업 직전에 조정을 시작합니다.

- import, refresh, 컴파일, Unity Test Runner, Play Mode, 빌드 또는 live Editor 진단;
- scene, prefab 또는 직렬화 에셋 변경;
- Unity가 관찰하는 checkout의 C#, asmdef, 에셋 쓰기;
- exact-SHA 검증 또는 Editor owner handoff.

Git 전용 작업, 파일시스템 읽기 전용 조사, 문서, 계획 또는 Unity가 열지 않은 checkout의 파일 수정에는 Editor 검증을 요청하지 않는 한 조정을 시작하지 않습니다.

## Canonical Editor

첫 Editor 영향 작업 전에 실행 중인 Unity 프로세스와, 선택한 MCP가 지원한다면 MCP instance를 확인합니다. 그 다음 예정 scope를 넣어 `status`를 호출합니다.

- 유효한 `EditorOwner`가 저장소의 canonical Editor를 식별합니다.
- canonical Editor만 integrator를 등록하고 검증을 획득할 수 있습니다.
- Unity Editor 프로세스가 하나라도 실행 중이면 에이전트 지침은 새 Editor를 실행하지 않습니다. 패키지는 소유권을 기록하지만 프로세스 실행 자체를 차단하지 않습니다.
- 여러 Editor가 있고 owner를 안전하게 식별할 수 없으면 추측하지 말고 프로젝트 경로, PID, MCP instance와 owner 상태를 보고합니다.
- dirty canonical checkout은 exact SHA를 검증할 수 없습니다. 사용자 권한 안에서 clean 상태가 될 때까지 기다리거나 명시적인 owner handoff를 요청합니다.

준비 상태 확인에는 상한을 둡니다. 포함된 스킬은 기존 프로세스의 MCP 노출 60초, import·컴파일 준비 180초, owner handoff 100초를 권장합니다. 시작 dialog, Safe Mode, 라이선스, 업그레이드와 복구 선택은 사용자의 결정이 필요한 막힘입니다.

## 작업과 검증 생명주기

1. 안정적인 `agent_id`와 예정된 저장소 상대 `scopes`로 `status`를 호출합니다.
2. `ConflictingClaims`가 비어 있으면 `worker`로 `begin_work`를 호출합니다. canonical checkout을 연 Editor는 하나의 `integrator`를 등록할 수 있습니다.
3. 경로를 넓히기 전에 claim하고, phase·summary·worktree HEAD·work kind가 바뀌면 `heartbeat`를 보냅니다.
4. Unity가 관찰하는 파일을 쓰기 전과 모든 Editor 상태 호출 전에 `status`를 다시 확인합니다. 다른 검증 lease가 활성화되어 있으면 기다립니다.
5. candidate commit을 만든 뒤 worker가 40자리 exact SHA, 순서가 있는 test mode와 manual check로 `enqueue_validation`을 호출합니다.
6. integrator는 canonical checkout을 해당 SHA로 맞추고 추적·미추적 변경이 모두 없는지 확인한 뒤 `acquire_validation`을 호출합니다.
7. 패키지가 에셋 refresh, 컴파일 요청, `EditMode`와 `PlayMode` suite를 순서대로 실행합니다.
8. Console과 수동 항목을 확인한 뒤 lease 소유자가 `finish_validation`을 호출합니다. 기록된 컴파일·테스트 실패는 `passed=true`로 덮어쓸 수 없습니다.
9. 폐기한 요청을 취소하고 모든 소유 요청이 끝난 뒤 `finish_work`를 호출합니다.

명시적으로 승인한 별도 worktree가 Git LFS를 사용한다면 Unity를 열거나 검증을 등록하기 전에 실제 object가 materialize되었는지 확인합니다. clean `git status`만으로는 commit된 LFS pointer와 실제 파일을 구분할 수 없습니다.

## 상태와 복구

같은 Git 저장소의 모든 worktree는 `.git/commondir`에서 만든 ID를 공유합니다. 기본 상태 경로는 다음과 같습니다.

```text
%LOCALAPPDATA%/Olivecrow/UnityAgentCoordination/<repository-id>
```

테스트나 제어된 자동화에서는 `UNITY_AGENT_COORDINATOR_ROOT`로 루트를 바꿀 수 있습니다. JSON 갱신은 named mutex와 임시 파일 교체를 사용합니다.

canonical Editor는 15초마다 heartbeat를 보냅니다. 종료된 owner 프로세스는 즉시 교체할 수 있고, 그 외에는 90초 후 stale로 간주합니다. owner 복구 시 worker claim과 검증 대기열을 보존합니다. agent heartbeat와 검증 lease는 45분, 고아 대기 요청은 4시간 뒤 정리됩니다. Editor가 정상 종료되면 저장소 조정 상태와 owner 파일을 정리합니다.

현재 상태는 `Window > AI Agent Coordination`에서 확인합니다. 전체 상태 비우기는 일반 종료가 아니라 복구 기능입니다.

## 에이전트 지침

Codex용 스킬은 `Skills~/unity-agent-coordination`에 포함되어 있습니다. 다른 에이전트는 `AGENTS.md`, `CLAUDE.md` 같은 프로젝트 지침에서 같은 운영 계약을 연결할 수 있습니다. [에이전트 설정](agent-setup.ko.md)을 참고합니다.
