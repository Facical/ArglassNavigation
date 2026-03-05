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
        public readonly string OrderGroup;
        public readonly string FirstRoute;
        public readonly string SecondRoute;

        public SessionInitialized(string participantId, string orderGroup,
            string firstRoute, string secondRoute)
        {
            ParticipantId = participantId;
            OrderGroup = orderGroup;
            FirstRoute = firstRoute;
            SecondRoute = secondRoute;
        }
    }

    /// <summary>
    /// 경로(Route) 시작 시 발행.
    /// </summary>
    public readonly struct RouteStarted : IDomainEvent
    {
        public readonly string RouteId;
        public readonly string Condition;

        public RouteStarted(string routeId, string condition)
        {
            RouteId = routeId;
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
