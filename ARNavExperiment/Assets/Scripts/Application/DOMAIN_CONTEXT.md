# Application 계층 컨텍스트

## 역할
- **DomainEventBus**: 도메인 이벤트 중앙 발행/구독 (동기, try-catch per handler)
- **ObservationService**: 도메인 이벤트 → EventLogger CSV 위임 (기존 CSV 포맷 100% 호환)

## 의존성 규칙
- Domain 계층만 참조 가능 (Infrastructure/Presentation 참조 금지)
- 예외: ObservationService는 Logging 계층(EventLogger) 참조 — Phase 완료 후 Infrastructure로 이동 예정

## 초기화 순서
- DomainEventBus.Awake() → DontDestroyOnLoad
- ObservationService.OnEnable() → DomainEventBus 구독

## 관련 파일
- `Scripts/Application/DomainEventBus.cs`
- `Scripts/Application/ObservationService.cs`
