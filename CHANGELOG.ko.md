# 변경 기록

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

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
