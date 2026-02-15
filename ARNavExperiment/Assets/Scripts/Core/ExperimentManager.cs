using UnityEngine;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.Core
{
    public enum ExperimentState
    {
        Idle,
        Setup,
        Practice,
        Condition1,
        Survey1,
        Condition2,
        Survey2,
        PostSurvey,
        Complete
    }

    public class ExperimentManager : MonoBehaviour
    {
        public static ExperimentManager Instance { get; private set; }

        [Header("Session")]
        public ParticipantSession session;

        public ExperimentState CurrentState { get; private set; } = ExperimentState.Idle;

        public event System.Action<ExperimentState> OnStateChanged;

        private float experimentStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeSession(string participantId, string orderGroup)
        {
            session = new ParticipantSession(participantId, orderGroup);
            Debug.Log($"[ExperimentManager] Session: {participantId}, Group: {orderGroup}, " +
                      $"Order: {session.FirstCondition}→{session.SecondCondition}, " +
                      $"Routes: {session.firstRoute}→{session.secondRoute}");
        }

        public void StartExperiment()
        {
            experimentStartTime = Time.time;
            string condStr = session.FirstCondition;
            EventLogger.Instance?.StartSession(session.participantId, condStr, session.firstRoute);
            EventLogger.Instance?.LogEvent("EXPERIMENT_START",
                extraData: $"{{\"order_group\":\"{session.orderGroup}\"}}");
            TransitionTo(ExperimentState.Setup);
        }

        public void TransitionTo(ExperimentState newState)
        {
            var prev = CurrentState;
            CurrentState = newState;
            Debug.Log($"[ExperimentManager] {prev} → {newState}");

            switch (newState)
            {
                case ExperimentState.Practice:
                    StartPractice();
                    break;
                case ExperimentState.Condition1:
                    StartCondition(1);
                    break;
                case ExperimentState.Survey1:
                    EventLogger.Instance?.LogEvent("SURVEY_START",
                        extraData: "{\"survey\":\"post_condition_1\"}");
                    break;
                case ExperimentState.Condition2:
                    StartCondition(2);
                    break;
                case ExperimentState.Survey2:
                    EventLogger.Instance?.LogEvent("SURVEY_START",
                        extraData: "{\"survey\":\"post_condition_2\"}");
                    break;
                case ExperimentState.PostSurvey:
                    EventLogger.Instance?.LogEvent("SURVEY_START",
                        extraData: "{\"survey\":\"post_experiment\"}");
                    break;
                case ExperimentState.Complete:
                    CompleteExperiment();
                    break;
            }

            OnStateChanged?.Invoke(newState);
        }

        public void AdvanceState()
        {
            switch (CurrentState)
            {
                case ExperimentState.Idle: TransitionTo(ExperimentState.Setup); break;
                case ExperimentState.Setup: TransitionTo(ExperimentState.Practice); break;
                case ExperimentState.Practice: TransitionTo(ExperimentState.Condition1); break;
                case ExperimentState.Condition1: TransitionTo(ExperimentState.Survey1); break;
                case ExperimentState.Survey1: TransitionTo(ExperimentState.Condition2); break;
                case ExperimentState.Condition2: TransitionTo(ExperimentState.Survey2); break;
                case ExperimentState.Survey2: TransitionTo(ExperimentState.PostSurvey); break;
                case ExperimentState.PostSurvey: TransitionTo(ExperimentState.Complete); break;
            }
        }

        private void StartPractice()
        {
            EventLogger.Instance?.LogEvent("PRACTICE_START");
        }

        private void StartCondition(int conditionNumber)
        {
            string condition = conditionNumber == 1 ? session.FirstCondition : session.SecondCondition;
            string route = conditionNumber == 1 ? session.firstRoute : session.secondRoute;

            var cond = condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            EventLogger.Instance?.SetCondition(condition);
            EventLogger.Instance?.LogEvent("ROUTE_START",
                extraData: $"{{\"route\":\"{route}\",\"condition\":\"{condition}\"}}");

            WaypointManager.Instance?.LoadRoute(route);
        }

        private void CompleteExperiment()
        {
            float totalDuration = Time.time - experimentStartTime;
            EventLogger.Instance?.LogEvent("EXPERIMENT_END",
                extraData: $"{{\"total_duration_s\":{totalDuration:F0}}}");
            EventLogger.Instance?.EndSession();
            Debug.Log("[ExperimentManager] Experiment complete");
        }
    }
}
