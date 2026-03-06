using UnityEngine;
using ARNavExperiment.Logging;
using ARNavExperiment.Navigation;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Core
{
    public enum ExperimentState
    {
        Idle,
        Relocalization,
        Setup,
        Running,
        Survey,
        Complete
    }

    public class ExperimentManager : MonoBehaviour
    {
        public static ExperimentManager Instance { get; private set; }

        [Header("Session")]
        public ParticipantSession session;

        public ExperimentState CurrentState { get; private set; } = ExperimentState.Idle;
        public string ActiveRoute { get; private set; }
        public string ActiveCondition { get; private set; }

        public event System.Action<ExperimentState> OnStateChanged;

        private float experimentStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeSession(string participantId, string condition, string route)
        {
            session = new ParticipantSession(participantId, condition, route);
            Debug.Log($"[ExperimentManager] Session: {participantId}, Condition: {condition}, Route: {route}");

            experimentStartTime = Time.time;
            EventLogger.Instance?.StartSession(participantId, condition, route);
            DomainEventBus.Instance?.Publish(new SessionInitialized(participantId, condition, route));
        }

        /// <summary>
        /// 매핑 데이터가 없을 때 직접 Setup으로 진입하는 fallback 경로.
        /// </summary>
        public void StartExperiment()
        {
            TransitionTo(ExperimentState.Setup);
        }

        public void TransitionTo(ExperimentState newState)
        {
            var prev = CurrentState;
            CurrentState = newState;
            Debug.Log($"[ExperimentManager] {prev} → {newState}");

            switch (newState)
            {
                case ExperimentState.Relocalization:
                    StartRelocalization();
                    break;
                case ExperimentState.Running:
                    StartCondition();
                    break;
                case ExperimentState.Survey:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("post_condition"));
                    break;
                case ExperimentState.Complete:
                    CompleteExperiment();
                    break;
            }

            DomainEventBus.Instance?.Publish(new ExperimentStateChanged(prev.ToString(), newState.ToString()));
            OnStateChanged?.Invoke(newState);

            // 상태 전환 진단 스냅샷
            WriteTransitionSnapshot(prev, newState);
        }

        public void AdvanceState()
        {
            switch (CurrentState)
            {
                case ExperimentState.Idle: TransitionTo(ExperimentState.Setup); break;
                case ExperimentState.Relocalization: TransitionTo(ExperimentState.Setup); break;
                case ExperimentState.Setup: TransitionTo(ExperimentState.Running); break;
                case ExperimentState.Running: TransitionTo(ExperimentState.Survey); break;
                case ExperimentState.Survey: TransitionTo(ExperimentState.Complete); break;
            }
        }

        private void StartRelocalization()
        {
            // GlassOnly 시 Beam Pro를 즉시 비활성화하기 위해 조건 조기 적용
            var cond = session.condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            DomainEventBus.Instance?.Publish(RelocalizationStarted.Default);
            Debug.Log("[ExperimentManager] Relocalization 시작 — 앵커 재인식 대기 중");
        }

        private void StartCondition()
        {
            Mission.MissionManager.Instance?.ResetState();

            ActiveCondition = session.condition;
            ActiveRoute = session.route;

            var cond = session.condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            DomainEventBus.Instance?.Publish(new RouteStarted(session.route, session.condition));

            WaypointManager.Instance?.LoadRoute(session.route);
        }

        private void WriteTransitionSnapshot(ExperimentState prev, ExperimentState next)
        {
            var sam = SpatialAnchorManager.Instance;
            var wpm = WaypointManager.Instance;
            var mm = Mission.MissionManager.Instance;

            int anchorSuccess = sam != null ? sam.SuccessfulAnchorCount : -1;
            int anchorTotal = sam != null ? sam.TotalAnchorCount : -1;
            int fallbackCount = wpm != null ? wpm.FallbackWaypointCount : -1;
            string missionState = mm != null ? mm.CurrentState.ToString() : "N/A";
            float uptime = Time.realtimeSinceStartup;

            string snapshot = $"StateTransition {prev}→{next} | " +
                $"participant={session?.participantId} | route={ActiveRoute} | condition={ActiveCondition} | " +
                $"anchors={anchorSuccess}/{anchorTotal} | fallback={fallbackCount} | " +
                $"mission={missionState} | uptime={uptime:F0}s";

            Debug.Log($"[ExperimentManager] {snapshot}");
            sam?.WriteDiagnostic(snapshot);
        }

        private void CompleteExperiment()
        {
            float totalDuration = Time.time - experimentStartTime;
            DomainEventBus.Instance?.Publish(new ExperimentCompleted(totalDuration));
            Debug.Log("[ExperimentManager] Experiment complete");
        }
    }
}
