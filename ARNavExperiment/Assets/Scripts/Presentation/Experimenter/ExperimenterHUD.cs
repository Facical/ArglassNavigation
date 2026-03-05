using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Logging;

namespace ARNavExperiment.Presentation.Experimenter
{
    /// <summary>
    /// Beam Pro에 표시되는 실험자 전용 제어 패널.
    /// ExperimenterCanvas (ScreenSpaceOverlay, sortOrder=10)에 배치.
    /// 실험 상태 표시 + 상태 전환/미션 제어 버튼 제공.
    /// </summary>
    public class ExperimenterHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        [Header("Anchor Status")]
        [SerializeField] private TextMeshProUGUI anchorStatusText;

        [Header("Glass Capture")]
        [SerializeField] private Button captureToggleButton;
        [SerializeField] private TextMeshProUGUI captureStatusText;

        [Header("Controls")]
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button nextMissionButton;

        private bool captureEventBound;
        private TextMeshProUGUI nextMissionBtnText;

        private void Start()
        {
            advanceButton?.onClick.AddListener(OnAdvanceClicked);
            nextMissionButton?.onClick.AddListener(OnNextMissionClicked);
            captureToggleButton?.onClick.AddListener(OnCaptureToggleClicked);

            if (nextMissionButton != null)
                nextMissionBtnText = nextMissionButton.GetComponentInChildren<TextMeshProUGUI>();

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += UpdateStateDisplay;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            TryBindCaptureEvent();
            UpdateCaptureUI(false);
        }

        private void Update()
        {
            if (missionText != null && MissionManager.Instance != null)
            {
                var m = MissionManager.Instance.CurrentMission;
                missionText.text = m != null
                    ? string.Format(LocalizationManager.Get("hud.mission"), m.missionId, MissionManager.Instance.CurrentState)
                    : LocalizationManager.Get("hud.no_mission");
            }

            if (waypointText != null && Navigation.WaypointManager.Instance != null)
            {
                var wp = Navigation.WaypointManager.Instance.CurrentWaypoint;
                float dist = Navigation.WaypointManager.Instance.GetDistanceToNext();
                waypointText.text = wp != null
                    ? string.Format(LocalizationManager.Get("hud.waypoint"), wp.waypointId, $"{dist:F1}")
                    : LocalizationManager.Get("hud.no_waypoint");
            }

            if (anchorStatusText != null && Navigation.WaypointManager.Instance != null)
            {
                int fallback = Navigation.WaypointManager.Instance.FallbackWaypointCount;
                anchorStatusText.text = fallback > 0
                    ? string.Format(LocalizationManager.Get("exphud.fallback"), fallback)
                    : LocalizationManager.Get("exphud.anchors_ok");
            }

            // 미션 버튼 라벨 동적 업데이트
            if (nextMissionBtnText != null && MissionManager.Instance != null)
            {
                string labelKey = MissionManager.Instance.CurrentState switch
                {
                    MissionState.Briefing => "exphud.skip_briefing",
                    MissionState.Navigation => "exphud.force_arrival",
                    MissionState.Arrival or MissionState.Verification
                        or MissionState.ConfidenceRating or MissionState.DifficultyRating => "exphud.skip_mission",
                    _ => "exphud.next_mission",
                };
                nextMissionBtnText.text = LocalizationManager.Get(labelKey);
            }

            // GlassViewCapture 지연 바인딩 (초기화 순서 무관)
            if (!captureEventBound)
                TryBindCaptureEvent();

            if (captureStatusText != null)
            {
                bool recording = GlassViewCapture.Instance != null
                    && GlassViewCapture.Instance.IsRecording;
                captureStatusText.text = recording ? LocalizationManager.Get("exphud.rec") : "";
                captureStatusText.color = Color.red;
            }
        }

        private void UpdateStateDisplay(ExperimentState state)
        {
            if (stateText) stateText.text = string.Format(LocalizationManager.Get("hud.state"), state);
            if (conditionText)
            {
                if (state == ExperimentState.Condition1 || state == ExperimentState.Condition2)
                    conditionText.text = string.Format(LocalizationManager.Get("hud.condition"), ConditionController.Instance?.CurrentCondition);
                else
                    conditionText.text = LocalizationManager.Get("hud.condition_none");
            }
        }

        private void RefreshLocalization(Language lang)
        {
            if (ExperimentManager.Instance != null)
                UpdateStateDisplay(ExperimentManager.Instance.CurrentState);
        }

        private void OnAdvanceClicked()
        {
            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnNextMissionClicked()
        {
            var mm = MissionManager.Instance;
            if (mm == null) return;

            switch (mm.CurrentState)
            {
                case MissionState.Briefing:
                    mm.AdvanceBriefing();
                    break;
                case MissionState.Navigation:
                    mm.ForceArrival();
                    break;
                case MissionState.Arrival:
                case MissionState.Verification:
                case MissionState.ConfidenceRating:
                case MissionState.DifficultyRating:
                    mm.ForceSkipMission();
                    break;
                default:
                    mm.StartNextMission();
                    break;
            }
        }

        private void OnCaptureToggleClicked()
        {
            if (GlassViewCapture.Instance != null)
                GlassViewCapture.Instance.ToggleCapture();
            else
                Debug.LogWarning("[ExperimenterHUD] GlassViewCapture가 씬에 없습니다!");
        }

        private void TryBindCaptureEvent()
        {
            if (captureEventBound) return;
            if (GlassViewCapture.Instance != null)
            {
                GlassViewCapture.Instance.OnRecordingStateChanged += UpdateCaptureUI;
                captureEventBound = true;
            }
        }

        private void UpdateCaptureUI(bool isRecording)
        {
            if (captureToggleButton != null)
            {
                var btnText = captureToggleButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = isRecording
                        ? LocalizationManager.Get("exphud.stop_capture")
                        : LocalizationManager.Get("exphud.capture");

                var btnImage = captureToggleButton.GetComponent<UnityEngine.UI.Image>();
                if (btnImage != null)
                    btnImage.color = isRecording
                        ? new Color(0.7f, 0.15f, 0.15f, 1f)
                        : new Color(0.5f, 0.3f, 0.1f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;

            if (GlassViewCapture.Instance != null)
                GlassViewCapture.Instance.OnRecordingStateChanged -= UpdateCaptureUI;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }
    }
}
