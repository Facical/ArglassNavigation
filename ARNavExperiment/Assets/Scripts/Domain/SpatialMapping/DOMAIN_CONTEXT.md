# SpatialMapping 도메인 컨텍스트

## 핵심 개념
- **AnchorMapping**: 웨이포인트-앵커 GUID 매핑 (anchor_mapping.json)
- **Relocalization**: 앵커 GUID 기반 공간 재인식 (30초 타임아웃, 0.5초 폴링)
- **Background Reanchoring**: 실패 앵커 5초 루프 + TryRemap(15초) 재시도
- **Anchor Quality**: LOW/MEDIUM/GOOD (매핑 시 최대 8초 관찰, GOOD 즉시 저장)

## 발행 이벤트
- `RelocalizationProgress(total, success, fail)` — 재인식 진행 상황
- `RelocalizationCompleted(successRate, action)` — 재인식 완료 (complete/retry/proceed_partial)
- `AnchorLateRecovered(waypointId, anchorGuid)` — 지연 앵커 복구
- `AnchorSaved(waypointId, anchorGuid, quality)` — 앵커 저장 (매핑 모드)
- `AnchorDiagnostics(diagnosticsJson)` — 진단 정보

## 구독 이벤트
- `RouteStarted` → LoadAdditionalRouteAnchors (Condition2에서)

## 관련 파일
- `Scripts/Core/SpatialAnchorManager.cs`
- `Scripts/Navigation/MappingAnchorVisualizer.cs`
