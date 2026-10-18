# 변경 기록

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

## 0.4.0 - 2026-10-18

- Unity 패키지 import 후 Codex 스킬을 해당 프로젝트의 `.agents/skills/unity-agent-coordination`에 자동 등록합니다.
- 관리 파일 해시를 기록해 수정되지 않은 파일만 갱신하며, 정상 Package Manager 제거 시 패키지 소유 파일만 정리합니다. 사용자 수정과 비관리 스킬은 경고와 함께 보존합니다.
- 스킬을 해당 프로젝트와 이미 설치된 패키지에 한정하고, 전역 스킬 설치와 자동 패키지 bootstrap을 제거했습니다.
- 설치·반복 등록·업데이트·제거·사용자 수정 보존을 확인하는 파일시스템 회귀 테스트를 추가했습니다.
- Unity 참조 어셈블리 컴파일과 격리 테스트를 실제 Editor callback·Unity Test Runner 검증과 구분해 문서화했습니다.

## 0.3.0 - 2026-08-29

- import, 컴파일, Console 확인, 테스트, Play Mode, 빌드, 직렬화 편집과 사용자 정의 작업을 commit 없이 요청할 수 있는 Editor 점유 대기열을 추가했습니다.
- 하나의 논리적 agent와 같은 HEAD에서도 독립 작업 세션을 여러 개 등록할 수 있도록 `session_id`를 추가했습니다. 생략하면 하위 호환을 위해 `agent_id`를 세션 ID로 사용합니다.
- FIFO Editor 점유 lease, 선택형 요청 멱등성 키, heartbeat 만료, 취소와 exact-SHA 검증 간 상호 배제를 추가했습니다.
- 경로를 명시한 Editor 점유 요청은 해당 세션의 기존 claim 범위 안에서만 허용하고 Console 확인처럼 경로와 무관한 작업은 scope 없이 요청할 수 있게 했습니다.
- 저장된 coordination 상태를 schema version 2로 마이그레이션하고 상태 창과 전송 중립 API에 Editor 점유 세션과 요청을 표시합니다.
- 공개 명령 라우터 signature를 사용하는 테스트가 컴파일되도록 Editor 테스트 어셈블리에 NUnit과 Newtonsoft JSON precompiled reference를 명시했습니다.
- `com.unity.nuget.newtonsoft-json`을 패키지와 함께 자동 resolve되는 필수 UPM 의존성으로 확인했습니다.
- Codex 스킬이 기존 패키지 소스와 버전을 유지하고 패키지가 없을 때만 태그가 지정된 Git 의존성을 추가하도록 했습니다.
- 패키지 설치가 프로젝트의 Unity MCP provider를 설치하거나 교체하지 않는다는 점을 문서화했습니다.

## 0.2.0 - 2026-08-28

- 패키지와 어셈블리를 `Olivecrow.UnityAgentCoordination` namespace로 변경했습니다.
- 공개 전송 중립 C#/JSON 명령 라우터를 추가했습니다.
- Coplay Unity MCP를 필수 패키지 의존성에서 제거했습니다.
- Coplay 등록을 HTTP와 stdio를 지원하는 version-defined 선택형 어댑터로 분리했습니다.
- 패키지 UI와 기본 문서를 영어로 전환하고 별도 한국어 문서를 추가했습니다.
- package manifest의 최소 Unity 버전을 제거하고 Unity `6000.3.22f1`을 테스트 버전으로만 기록했습니다.
- 기본 상태 루트를 `%LOCALAPPDATA%/Olivecrow/UnityAgentCoordination`으로 변경했습니다.

## 0.1.1

- 작업 성격, 단계, 요약, 시작 시각과 heartbeat 시각을 구조화했습니다.
- Editor 창에 실시간 작업 및 검증 대기열 표시를 추가했습니다.
- stale agent의 claim과 대기 검증 정리를 추가했습니다.
- Unity 프로젝트에서는 기존 canonical Editor 하나만 사용하고 추가 worktree를 기본 생성하지 않도록 운영 규칙을 명확히 했습니다.

## 0.1.0

- Git worktree 사이에서 공유하는 저장소 경로 claim과 agent heartbeat를 추가했습니다.
- 컴파일과 EditMode/PlayMode 테스트를 위한 직렬 검증 대기열을 추가했습니다.
- exact-SHA 검증, owner handoff, 원자적 정리와 stale owner 복구를 추가했습니다.
- 첫 MCP 어댑터와 에이전트 운영 지침을 추가했습니다.
