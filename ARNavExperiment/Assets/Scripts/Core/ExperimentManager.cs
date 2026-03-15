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
        ComparisonSurvey,
        Complete
    }

    public class ExperimentManager : MonoBehaviour
    {
        public static ExperimentManager Instance { get; private set; }

        [Header("Session")]
        public ParticipantSession session;

        [Header("Localization Mode")]
        [Tooltip("true: Image Tracking 기반 정렬, false: Spatial Anchor 기반 재인식")]
        [SerializeField] private bool useImageTracking = false;
        public bool UseImageTracking => useImageTracking;

        public ExperimentState CurrentState { get; private set; } = ExperimentState.Idle;
        public string ActiveMissionSet { get; private set; }
        public string ActiveCondition { get; private set; }

        /// <summary>현재 실행 번호 (1: 1차 조건, 2: 2차 조건)</summary>
        public int RunNumber { get; private set; }
        /// <summary>1차 조건 문자열 (2차 실행 시 비활성화용)</summary>
        public string FirstCondition { get; private set; }
        /// <summary>1차 미션 세트 (2차 실행 시 반대 세트 자동 선택용)</summary>
        public string FirstMissionSet { get; private set; }

        public event System.Action<ExperimentState> OnStateChanged;

        private float experimentStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeSession(string participantId, string condition, string missionSet)
        {
            session = new ParticipantSession(participantId, condition, missionSet);

            // runNumber 설정: FirstCondition이 비어있으면 1차, 아니면 2차
            if (string.IsNullOrEmpty(FirstCondition))
            {
                RunNumber = 1;
                FirstCondition = condition;
                FirstMissionSet = missionSet;
            }
            else
            {
                RunNumber = 2;
            }

            Debug.Log($"[ExperimentManager] Session: {participantId}, Condition: {condition}, MissionSet: {missionSet}, Run: {RunNumber}");

            experimentStartTime = Time.time;
            EventLogger.Instance?.StartSession(participantId, condition, missionSet);
            DomainEventBus.Instance?.Publish(new SessionInitialized(participantId, condition, missionSet));
        }

        /// <summary>
        /// 매핑 데이터가 없을 때 직접 Setup으로 진입하는 fallback 경로.
        /// Relocalization 건너뛰므로 조건을 여기서 적용.
        /// </summary>
        public void StartExperiment()
        {
            if (session != null)
            {
                var cond = session.condition == "glass_only"
                    ? ExperimentCondition.GlassOnly
                    : ExperimentCondition.Hybrid;
                ConditionController.Instance?.SetCondition(cond);
            }
            TransitionTo(ExperimentState.Setup);
        }

        public void TransitionTo(ExperimentState newState)
        {
            var prev = CurrentState;
            CurrentState = newState;
            Debug.Log($"[ExperimentManager] {prev} → {newState}");

            switch (newState)
            {
                case ExperimentState.Idle:
                    ReturnToIdle();
                    break;
                case ExperimentState.Relocalization:
                    StartRelocalization();
                    break;
                case ExperimentState.Running:
                    StartCondition();
                    break;
                case ExperimentState.Survey:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("post_condition"));
                    break;
                case ExperimentState.ComparisonSurvey:
                    DomainEventBus.Instance?.Publish(new SurveyStarted("comparison"));
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
                case ExperimentState.Survey:
                    if (RunNumber <= 1)
                        TransitionTo(ExperimentState.Idle); // 1차 완료 → AppModeSelector 복귀
                    else
                        TransitionTo(ExperimentState.ComparisonSurvey); // 2차 완료 → 비교 설문
                    break;
                case ExperimentState.ComparisonSurvey: TransitionTo(ExperimentState.Complete); break;
            }
        }

        /// <summary>
        /// 1차 조건 완료 후 Idle로 복귀. 세션 종료 + 매니저 리셋.
        /// AppModeSelector가 Idle 상태를 감지하여 모드 선택 화면을 표시.
        /// </summary>
        private void ReturnToIdle()
        {
            EventLogger.Instance?.EndSession();
            Mission.MissionManager.Instance?.ResetState();
            Navigation.WaypointManager.Instance?.ResetRoute();
            Debug.Log("[ExperimentManager] ReturnToIdle — 1차 조건 완료, AppModeSelector 복귀");
        }

        private void StartRelocalization()
        {
            // GlassOnly 시 Beam Pro를 즉시 비활성화하기 위해 조건 조기 적용
            var cond = session.condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            DomainEventBus.Instance?.Publish(RelocalizationStarted.Default);

            // Spatial Anchor relocalization은 SpatialAnchorManager가 항상 수행
            Debug.Log("[ExperimentManager] Spatial Anchor relocalization 시작 — 앵커 재인식 대기 중");

            if (useImageTracking)
            {
                // Image Tracking 병행: 마커 감지로 추가 보정 쌍 주입
                ImageTrackingAligner.Instance?.StartTracking();
                Debug.Log("[ExperimentManager] Image Tracking 보정 병행 시작");
            }
        }

        private void StartCondition()
        {
            Mission.MissionManager.Instance?.ResetState();

            ActiveCondition = session.condition;
            ActiveMissionSet = session.missionSet;

            var cond = session.condition == "glass_only" ? ExperimentCondition.GlassOnly : ExperimentCondition.Hybrid;
            ConditionController.Instance?.SetCondition(cond);

            DomainEventBus.Instance?.Publish(new RouteStarted(session.missionSet, session.condition));

            // 단일 경로 로드
            WaypointManager.Instance?.LoadRoute("B");
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
                $"participant={session?.participantId} | missionSet={ActiveMissionSet} | condition={ActiveCondition} | " +
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
