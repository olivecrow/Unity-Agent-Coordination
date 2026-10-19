# 에이전트 설정

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

## 프로젝트 전용 Codex 스킬

패키지 import와 컴파일 후 Editor 초기화가 `Skills~/unity-agent-coordination`을 해당 Unity 프로젝트의 `.agents/skills/unity-agent-coordination`에 복사합니다. 전역 스킬이나 `AGENTS.md` 자동 수정은 필요하지 않습니다. 미수정 관리 파일만 갱신하고 사용자 수정과 비관리 스킬은 경고와 함께 보존합니다. 정상적인 Package Manager 제거에서는 미수정 패키지 관리 파일을 정리합니다. Unity가 닫힌 상태에서 제거하면 프로젝트 스킬이 남을 수 있지만 패키지가 없으면 그 스킬도 사용을 중단합니다.

기존 전역본은 프로젝트 스킬이 생성된 것을 확인한 뒤 정리합니다. 생성 파일의 커밋/ignore 여부는 프로젝트의 일반 운영 방식으로 정하며 번호표 소유권과는 무관합니다.

## 기존 Unity bridge 사용

대상 Editor 안에서 `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`을 호출할 수 있는 bridge를 사용합니다. 선택형 Coplay 10.1.2–10.x 어댑터는 `manage_unity_agent_coordination`을 노출합니다. 대기열이 MCP provider를 설치하거나 관리하지 않습니다.

다른 에이전트는 프로젝트 지침에서 [프로토콜](mcp-protocol.ko.md)을 연결하면 됩니다. 고유 세션 ID로 번호표를 발급하고 자기 차례를 기다린 뒤 점유·사용·반납하며 반납은 finally/정리 단계에서 보장합니다. 모든 참여자가 이 약속을 지켜야 하고 다른 bridge 명령을 가로채지는 않습니다.

0.5 명령 입력은 `action`, `session_id`, `ticket_id`, 선택형 `label`뿐입니다. 이전 세션 종료 후 패키지·bridge·스킬을 함께 갱신합니다. 이전 작업 등록과 검증 API는 제거됐습니다.
