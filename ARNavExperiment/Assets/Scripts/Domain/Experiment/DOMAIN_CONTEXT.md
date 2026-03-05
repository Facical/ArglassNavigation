# Experiment 도메인 컨텍스트

## 핵심 개념
- **ExperimentState**: 9단계 외부 FSM (Idle→Relocalization→Setup→Condition1→Survey1→Condition2→Survey2→PostSurvey→Complete)
- **ExperimentCondition**: GlassOnly / Hybrid (피험자 내 설계)
- **ParticipantSession**: 참가자 ID, 순서 그룹, 경로 순서를 담는 세션 데이터

## 발행 이벤트
- `ExperimentStateChanged(prev, current)` — 상태 전환 시
- `ConditionChanged(condition)` — GlassOnly↔Hybrid 전환 시
- `SessionInitialized(participantId, orderGroup, firstRoute, secondRoute)` — 세션 초기화 시
- `RouteStarted(routeId, condition)` — 경로 시작 시
- `SurveyStarted(surveyType)` — 설문 진입 시
- `ExperimentCompleted(totalDurationSeconds)` — 실험 완료 시

## 구독 이벤트
- `MissionCompleted` → 다음 미션 또는 AdvanceState 결정

## 공동 변경 규칙
- `ExperimentState` enum 추가 → ExperimentManager, ExperimentFlowUI, ExperimentHUD, ExperimenterHUD
- `ExperimentCondition` 추가 → ConditionController + 구독자 전체

## 관련 파일
- `Scripts/Core/ExperimentManager.cs`
- `Scripts/Core/ConditionController.cs`
- `Scripts/Core/ParticipantSession.cs`
