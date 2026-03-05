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

        public void InitializeSession(string participantId, string orderGroup)
        {
            session = new ParticipantSession(participantId, orderGroup);
            Debug.Log($"[ExperimentManager] Session: {participantId}, Group: {orderGroup}, " +
                      $"Order: {session.FirstCondition}→{session.SecondCondition}, " +
                      $"Routes: {session.firstRoute}→{session.secondRoute}");

            // 로거 시작 — Relocalization 경로에서도 기록되도록 여기서 초기화
            experimentStartTime = Time.time;
            string condStr = session.FirstCondition;
            EventLogger.Instance?.StartSession(session.participantId, condStr, session.firstRoute);
            DomainEventBus.Instance?.Publish(new SessionInitialized(
                session.participantId, session.orderGroup, session.firstRoute, session.secondRoute));
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
                case ExperimentState.Condition1:
                    StartCondition(1);
                    break;
                case ExperimentState.Survey1:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("post_condition_1"));
                    break;
                case ExperimentState.Condition2:
                    StartCondition(2);
                    break;
                case ExperimentState.Survey2:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("post_condition_2"));
                    break;
                case ExperimentState.PostSurvey:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("post_experiment"));
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
                case ExperimentState.Setup: TransitionTo(ExperimentState.Condition1); break;
                case ExperimentState.Condition1: TransitionTo(ExperimentState.Survey1); break;
                case ExperimentState.Survey1: TransitionTo(ExperimentState.Condition2); break;
                case ExperimentState.Condition2: TransitionTo(ExperimentState.Survey2); break;
                case ExperimentState.Survey2: TransitionTo(ExperimentState.PostSurvey); break;
                case ExperimentState.PostSurvey: TransitionTo(ExperimentState.Complete); break;
            }
        }

        private void StartRelocalization()
        {
            // SpatialAnchorManager의 앵커 로드 시작
            // RelocalizationUI가 OnStateChanged 이벤트를 받아 UI를 표시하고
            // SpatialAnchorManager.LoadAllAnchors()를 호출
            DomainEventBus.Instance?.Publish(RelocalizationStarted.Default);
            Debug.Log("[ExperimentManager] Relocalization 시작 — 앵커 재인식 대기 중");
        }

        private void StartCondition(int conditionNumber)
        {
            // 이전 Condition의 미션 상태 초기화 (방어적 코딩)
            Mission.MissionManager.Instance?.ResetState();

            string condition = conditionNumber == 1 ? session.FirstCondition : session.SecondCondition;
            string route = conditionNumber == 1 ? session.firstRoute : session.secondRoute;

            ActiveCondition = condition;
            ActiveRoute = route;

            var cond = condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            // Condition2: 추가 경로 앵커 로드 (WaypointManager.LoadRoute 전에 호출)
            if (conditionNumber == 2)
            {
                SpatialAnchorManager.Instance?.LoadAdditionalRouteAnchors(route);
            }

            // SetCondition은 ObservationService.OnConditionChanged에서 처리
            DomainEventBus.Instance?.Publish(new RouteStarted(route, condition));

            WaypointManager.Instance?.LoadRoute(route);
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
