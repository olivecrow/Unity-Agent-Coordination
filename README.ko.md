# Unity Agent Coordination

[English](README.md) | [한국어](README.ko.md)

여러 AI 에이전트가 하나의 공유 Unity Editor를 안전하게 사용하도록 조정하는 Editor 전용 UPM 패키지입니다. worker는 서로 겹치지 않는 저장소 경로를 claim하고, 하나의 canonical Editor만 컴파일, Unity Test Framework, Play Mode 작업, 직렬화 에셋 변경과 exact-commit 검증을 순서대로 수행합니다.

핵심 조정 엔진은 특정 MCP 구현에 의존하지 않습니다. 공개 C#/JSON 명령 API를 제공하고, MCP 연동은 이 API를 감싸는 어댑터로 분리합니다. Coplay Unity MCP 어댑터가 포함되어 있지만 호환 Coplay 패키지가 설치된 경우에만 컴파일됩니다.

## 호환성

- Unity `6000.3.22f1`에서 테스트했습니다.
- 패키지 manifest에는 최소 Unity 버전을 의도적으로 선언하지 않았습니다. 따라서 다른 버전에서도 UPM 설치가 허용될 수 있지만, 실제 테스트 전까지 호환을 보장하지 않습니다.
- Unity Test Framework와 Unity registry의 `com.unity.nuget.newtonsoft-json` 패키지가 필요합니다. 두 의존성 모두 `package.json`에 선언되어 Unity Package Manager가 자동으로 resolve합니다.
- Coplay Unity MCP는 선택 사항입니다. 포함된 어댑터는 `com.coplaydev.unity-mcp` `10.1.2` 이상 `11.0.0` 미만을 대상으로 하며 `10.1.2`에서 테스트했습니다.

## 설치

Unity Package Manager의 **Install package from git URL**에 다음 주소를 입력합니다.

```text
https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0
```

embedded package로 사용할 때는 `Packages/com.olivecrow.unity-agent-coordination`에 배치하고 소비 프로젝트의 `Packages/manifest.json`에 다음 의존성을 추가합니다.

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

## MCP와 에이전트 연동

- **Coplay Unity MCP:** 호환되는 `com.coplaydev.unity-mcp`를 설치하면 선택형 어셈블리가 연결 시 `manage_unity_agent_coordination`과 `get_unity_agent_coordination`을 자동 등록합니다.
- **다른 Unity MCP:** Editor 어댑터에서 JSON 객체를 `UnityAgentCoordinationCommandRouter.Execute(JObject)` 또는 `ExecuteJson(string)`으로 전달하고 결과를 그대로 반환하면 됩니다.
- **Codex:** `Skills~/unity-agent-coordination/SKILL.md`를 설치하거나 프로젝트 지침에서 참조합니다. Editor 영향 작업으로 스킬이 활성화되면 기존 패키지 소스와 버전은 그대로 사용하고, 패키지가 없을 때만 태그가 지정된 `v0.2.0` Git UPM 의존성을 추가합니다.
- **Claude Code 및 다른 에이전트:** 프로젝트의 에이전트 지침 파일에서 패키지 문서를 연결하고, 선택한 Unity MCP 어댑터를 통해 명령 라우터를 노출합니다.

MCP 프로토콜 자체에는 임의 Unity 패키지 도구를 공통 방식으로 자동 발견하는 규격이 없습니다. 선택한 MCP가 Editor C# API를 일반적으로 호출하는 기능을 제공하지 않는다면 어댑터가 필요합니다. 에이전트가 상태 파일을 직접 편집해 어댑터를 흉내 내면 안 됩니다.

정확한 설정은 [에이전트 설정](Documentation~/agent-setup.ko.md), 명령 계약은 [통합 프로토콜](Documentation~/mcp-protocol.ko.md)을 참고합니다.

## 패키지 어셈블리

- `Olivecrow.UnityAgentCoordination.Core`: 저장소 식별, 경로 claim, heartbeat, 검증 대기열, 원자적 상태와 owner lease 데이터.
- `Olivecrow.UnityAgentCoordination.Editor`: canonical Editor 소유권, Git checkout 검사, 컴파일, 테스트, 상태 창과 전송 중립 명령 라우터.
- `Olivecrow.UnityAgentCoordination.CoplayMcp`: Coplay가 없거나 지원 범위를 벗어나면 컴파일에서 제외되는 선택형 어댑터.
- Editor 테스트: 저장소 ID, claim 충돌, 직렬 검증, 상태 표시와 stale owner 복구를 격리된 Git fixture로 검증합니다.

현재 작업과 claim, owner, 검증 대기열은 `Window > AI Agent Coordination`에서 확인할 수 있습니다.

## 개발과 검증

Editor 테스트는 `Tests`에 있습니다. 변경 후 JSON과 asmdef, Unity 컴파일, EditMode 테스트, Coplay 설치·미설치 상태의 선택형 어댑터를 확인합니다. 현재 릴리스는 Unity `6000.3.22f1`에서 테스트했으며 더 넓은 Unity 버전 호환을 주장하지 않습니다.

## 라이선스

MIT License를 사용합니다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고합니다.
