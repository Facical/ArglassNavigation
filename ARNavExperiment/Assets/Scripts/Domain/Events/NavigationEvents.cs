namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// 웨이포인트 도달 시 발행.
    /// </summary>
    public readonly struct WaypointReached : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly bool IsTarget;

        public WaypointReached(string waypointId, bool isTarget)
        {
            WaypointId = waypointId;
            IsTarget = isTarget;
        }
    }

    /// <summary>
    /// 경로 전체 완주 시 발행.
    /// </summary>
    public readonly struct RouteCompleted : IDomainEvent
    {
        public readonly string RouteId;

        public RouteCompleted(string routeId) { RouteId = routeId; }
    }

    /// <summary>
    /// 불확실성 트리거 활성화 시 발행.
    /// </summary>
    public readonly struct TriggerActivated : IDomainEvent
    {
        public readonly string TriggerId;
        public readonly string TriggerType;

        public TriggerActivated(string triggerId, string triggerType)
        {
            TriggerId = triggerId;
            TriggerType = triggerType;
        }
    }

    /// <summary>
    /// 불확실성 트리거 비활성화 시 발행.
    /// </summary>
    public readonly struct TriggerDeactivated : IDomainEvent
    {
        public readonly string TriggerId;
        public readonly string TriggerType;
        public readonly float DurationSeconds;

        public TriggerDeactivated(string triggerId, string triggerType, float durationSeconds)
        {
            TriggerId = triggerId;
            TriggerType = triggerType;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// AR 화살표 표시 시 발행.
    /// </summary>
    public readonly struct ArrowShown : IDomainEvent
    {
        public static readonly ArrowShown Default = new ArrowShown();
    }

    /// <summary>
    /// AR 화살표 숨김 시 발행.
    /// </summary>
    public readonly struct ArrowHidden : IDomainEvent
    {
        public static readonly ArrowHidden Default = new ArrowHidden();
    }

    /// <summary>
    /// AR 화살표 오프셋(T1 jitter, T3 spread) 시 발행.
    /// </summary>
    public readonly struct ArrowOffset : IDomainEvent
    {
        public readonly string TriggerId;
        public readonly float OffsetAngle;

        public ArrowOffset(string triggerId, float offsetAngle)
        {
            TriggerId = triggerId;
            OffsetAngle = offsetAngle;
        }
    }

    /// <summary>
    /// 웨이포인트가 앵커 대신 fallback 좌표를 사용할 때 발행.
    /// </summary>
    public readonly struct WaypointFallbackUsed : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly string FallbackPosition;

        public WaypointFallbackUsed(string waypointId, string fallbackPosition)
        {
            WaypointId = waypointId;
            FallbackPosition = fallbackPosition;
        }
    }

    /// <summary>
    /// 지연 앵커 복구로 웨이포인트 위치가 갱신되었을 때 발행.
    /// </summary>
    public readonly struct WaypointLateAnchorBound : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly float DriftMeters;
        public readonly string OldPosition;
        public readonly string NewPosition;

        public WaypointLateAnchorBound(string waypointId, float driftMeters,
            string oldPosition, string newPosition)
        {
            WaypointId = waypointId;
            DriftMeters = driftMeters;
            OldPosition = oldPosition;
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// 10초 주기 내비게이션 상태 스냅샷 (현장 디버깅용).
    /// </summary>
    public readonly struct NavigationStateSnapshot : IDomainEvent
    {
        public readonly string WaypointId;
        public readonly bool IsAnchorBound;
        public readonly string PlayerPosition;
        public readonly string TargetPosition;
        public readonly float DistanceToTarget;
        public readonly bool ArrowVisible;
        public readonly string RouteId;
        public readonly string Condition;

        public NavigationStateSnapshot(string waypointId, bool isAnchorBound,
            string playerPosition, string targetPosition, float distanceToTarget,
            bool arrowVisible, string routeId, string condition)
        {
            WaypointId = waypointId;
            IsAnchorBound = isAnchorBound;
            PlayerPosition = playerPosition;
            TargetPosition = targetPosition;
            DistanceToTarget = distanceToTarget;
            ArrowVisible = arrowVisible;
            RouteId = routeId;
            Condition = condition;
        }
    }

    /// <summary>
    /// 경로 로드 시 전체 웨이포인트 바인딩 요약 (현장 디버깅용).
    /// </summary>
    public readonly struct RouteBindingSummary : IDomainEvent
    {
        public readonly string RouteId;
        public readonly int TotalWaypoints;
        public readonly int AnchorBound;
        public readonly int FallbackUsed;
        public readonly string DetailsJson;

        public RouteBindingSummary(string routeId, int totalWaypoints,
            int anchorBound, int fallbackUsed, string detailsJson)
        {
            RouteId = routeId;
            TotalWaypoints = totalWaypoints;
            AnchorBound = anchorBound;
            FallbackUsed = fallbackUsed;
            DetailsJson = detailsJson;
        }
    }
}
