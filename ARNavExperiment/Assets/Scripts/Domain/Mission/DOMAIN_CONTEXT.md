# Mission 도메인 컨텍스트

## 핵심 개념
- **MissionState**: 7단계 내부 FSM (Idle→Briefing→Navigation→Arrival→Verification→ConfidenceRating→DifficultyRating→Scored)
- **MissionType**: A_DirectionVerify, B_AmbiguousDecision, C_InfoIntegration
- **MissionResult**: 정답 여부, 응답 시간, 확신도, 난이도를 담는 값 객체
- Route당 미션 5개 (A1→B1→A2→B2→C1 순서)

## 발행 이벤트
- `MissionStarted(missionId, routeId, missionType, briefingText)`
- `MissionArrived(missionId, waypointId)`
- `VerificationAnswered(missionId, waypointId, selectedIndex, correct, rt)`
- `ConfidenceRated(missionId, waypointId, rating)`
- `DifficultyRated(missionId, rating)`
- `MissionCompleted(missionId, correct, durationSeconds)`
- `BriefingForced(missionId)`, `ArrivalForced(missionId)`, `MissionForceSkipped(missionId, state)`

## 구독 이벤트
- `WaypointReached` → 목적지 도달 감지
- `ConditionChanged` → BeamPro 탭 잠금/해제

## 공동 변경 규칙
- `MissionState` enum 추가 → MissionManager + 해당 UI 패널
- `MissionData` SO 필드 추가 → MissionDataGenerator + Inspector 사용처

## 관련 파일
- `Scripts/Mission/MissionManager.cs` (Phase 3에서 MissionOrchestrator로 분리 예정)
- `Scripts/Mission/MissionData.cs`, `POIData.cs`, `InfoCardData.cs`, `ComparisonData.cs`
- `Scripts/Mission/MissionBriefingUI.cs`, `VerificationUI.cs` (Phase 4에서 Presentation으로 이동 예정)
