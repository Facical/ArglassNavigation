namespace ARNavExperiment.Domain.Events
{
    /// <summary>
    /// ExperimentManager 외부 FSM 상태 전환 시 발행.
    /// prev/current는 ExperimentState enum 문자열.
    /// </summary>
    public readonly struct ExperimentStateChanged : IDomainEvent
    {
        public readonly string Prev;
        public readonly string Current;

        public ExperimentStateChanged(string prev, string current)
        {
            Prev = prev;
            Current = current;
        }
    }

    /// <summary>
    /// 실험 조건(GlassOnly/Hybrid) 전환 시 발행.
    /// </summary>
    public readonly struct ConditionChanged : IDomainEvent
    {
        public readonly string Condition; // "glass_only" | "hybrid"

        public ConditionChanged(string condition)
        {
            Condition = condition;
        }
    }

    /// <summary>
    /// 세션 초기화(참가자 등록) 시 발행.
    /// </summary>
    public readonly struct SessionInitialized : IDomainEvent
    {
        public readonly string ParticipantId;
        public readonly string Condition;
        public readonly string MissionSet;

        public SessionInitialized(string pid, string cond, string missionSet)
        {
            ParticipantId = pid;
            Condition = cond;
            MissionSet = missionSet;
        }
    }

    /// <summary>
    /// 조건/미션세트 시작 시 발행.
    /// </summary>
    public readonly struct RouteStarted : IDomainEvent
    {
        public readonly string MissionSet;
        public readonly string Condition;

        public RouteStarted(string missionSet, string condition)
        {
            MissionSet = missionSet;
            Condition = condition;
        }
    }

    /// <summary>
    /// 설문 시작 시 발행.
    /// </summary>
    public readonly struct SurveyStarted : IDomainEvent
    {
        public readonly string SurveyType; // "post_condition_1", "post_condition_2", "post_experiment"

        public SurveyStarted(string surveyType)
        {
            SurveyType = surveyType;
        }
    }

    /// <summary>
    /// 실험 종료 시 발행.
    /// </summary>
    public readonly struct ExperimentCompleted : IDomainEvent
    {
        public readonly float TotalDurationSeconds;

        public ExperimentCompleted(float totalDurationSeconds)
        {
            TotalDurationSeconds = totalDurationSeconds;
        }
    }
}
