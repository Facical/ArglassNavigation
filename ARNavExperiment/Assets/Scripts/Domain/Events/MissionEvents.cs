namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// 미션 브리핑 시작 시 발행.
    /// </summary>
    public readonly struct MissionStarted : IDomainEvent
    {
        public readonly string MissionId;
        public readonly string RouteId;
        public readonly string MissionType;
        public readonly string BriefingText;

        public MissionStarted(string missionId, string routeId, string missionType, string briefingText)
        {
            MissionId = missionId;
            RouteId = routeId;
            MissionType = missionType;
            BriefingText = briefingText;
        }
    }

    /// <summary>
    /// 목적지 도착 시 발행.
    /// </summary>
    public readonly struct MissionArrived : IDomainEvent
    {
        public readonly string MissionId;
        public readonly string WaypointId;

        public MissionArrived(string missionId, string waypointId)
        {
            MissionId = missionId;
            WaypointId = waypointId;
        }
    }

    /// <summary>
    /// 검증 문항 응답 시 발행.
    /// </summary>
    public readonly struct VerificationAnswered : IDomainEvent
    {
        public readonly string MissionId;
        public readonly string WaypointId;
        public readonly int SelectedIndex;
        public readonly bool Correct;
        public readonly float ResponseTimeSeconds;

        public VerificationAnswered(string missionId, string waypointId,
            int selectedIndex, bool correct, float responseTimeSeconds)
        {
            MissionId = missionId;
            WaypointId = waypointId;
            SelectedIndex = selectedIndex;
            Correct = correct;
            ResponseTimeSeconds = responseTimeSeconds;
        }
    }

    /// <summary>
    /// 확신도 평정 완료 시 발행.
    /// </summary>
    public readonly struct ConfidenceRated : IDomainEvent
    {
        public readonly string MissionId;
        public readonly string WaypointId;
        public readonly int Rating;

        public ConfidenceRated(string missionId, string waypointId, int rating)
        {
            MissionId = missionId;
            WaypointId = waypointId;
            Rating = rating;
        }
    }

    /// <summary>
    /// 난이도 평정 완료 시 발행.
    /// </summary>
    public readonly struct DifficultyRated : IDomainEvent
    {
        public readonly string MissionId;
        public readonly int Rating;

        public DifficultyRated(string missionId, int rating)
        {
            MissionId = missionId;
            Rating = rating;
        }
    }

    /// <summary>
    /// 미션 완료(채점 완료) 시 발행.
    /// </summary>
    public readonly struct MissionCompleted : IDomainEvent
    {
        public readonly string MissionId;
        public readonly bool Correct;
        public readonly float DurationSeconds;

        public MissionCompleted(string missionId, bool correct, float durationSeconds)
        {
            MissionId = missionId;
            Correct = correct;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// 브리핑 강제 진행 시 발행.
    /// </summary>
    public readonly struct BriefingForced : IDomainEvent
    {
        public readonly string MissionId;

        public BriefingForced(string missionId) { MissionId = missionId; }
    }

    /// <summary>
    /// 도착 강제 선언 시 발행.
    /// </summary>
    public readonly struct ArrivalForced : IDomainEvent
    {
        public readonly string MissionId;

        public ArrivalForced(string missionId) { MissionId = missionId; }
    }

    /// <summary>
    /// 경로 내 모든 미션이 완료되었을 때 발행.
    /// MissionManager → ExperimentManager 역방향 의존성 제거용.
    /// </summary>
    public readonly struct AllMissionsCompleted : IDomainEvent
    {
        public readonly string RouteId;

        public AllMissionsCompleted(string routeId) { RouteId = routeId; }
    }

    /// <summary>
    /// 미션 강제 스킵 시 발행.
    /// </summary>
    public readonly struct MissionForceSkipped : IDomainEvent
    {
        public readonly string MissionId;
        public readonly string State;

        public MissionForceSkipped(string missionId, string state)
        {
            MissionId = missionId;
            State = state;
        }
    }
}
