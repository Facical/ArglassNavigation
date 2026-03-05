# Navigation 도메인 컨텍스트

## 핵심 개념
- **Waypoint**: 경로 상의 지점 (waypointId, anchorTransform/fallbackPosition, radius)
- **Route**: A/B 두 경로, 각각 16개 웨이포인트
- **TriggerType**: T1(트래킹 저하), T2(정보 충돌), T3(저해상도), T4(안내 부재)
- **Fallback 패턴**: `anchorTransform ?? fallbackPosition` — 재인식 실패 시에도 내비게이션 지속

## 발행 이벤트
- `WaypointReached(waypointId, isTarget)` — 웨이포인트 도달 시
- `RouteCompleted(routeId)` — 경로 전체 완주 시
- `TriggerActivated(triggerId, triggerType)` — 불확실성 트리거 활성화
- `TriggerDeactivated(triggerId, triggerType, durationSeconds)` — 트리거 비활성화
- `ArrowShown`, `ArrowHidden`, `ArrowOffset(triggerId, offsetAngle)` — AR 화살표 상태
- `WaypointFallbackUsed(waypointId, fallbackPosition)` — fallback 사용 시
- `WaypointLateAnchorBound(waypointId, driftMeters, oldPos, newPos)` — 지연 앵커 복구 시

## 구독 이벤트
- `AnchorLateRecovered` → fallback→anchor 전환
- `ConditionChanged` → 조건별 동작 분기 없음 (현재)

## 공동 변경 규칙
- 웨이포인트 ID/fallbackPosition → WaypointGizmoDrawer, WaypointDataGenerator, EditorPlayerController

## 관련 파일
- `Scripts/Navigation/WaypointManager.cs`
- `Scripts/Navigation/TriggerController.cs`
- `Scripts/Navigation/ARArrowRenderer.cs`
