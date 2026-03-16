using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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

        [Header("Heading Calibration")]
        [SerializeField] private Button headingLeftButton;
        [SerializeField] private Button headingRightButton;
        [SerializeField] private TextMeshProUGUI headingOffsetText;

        [Header("Manual Calibration")]
        [SerializeField] private Button manualCalibrateButton;

        private bool captureEventBound;
        private TextMeshProUGUI nextMissionBtnText;

        private void Start()
        {
            advanceButton?.onClick.AddListener(OnAdvanceClicked);
            nextMissionButton?.onClick.AddListener(OnNextMissionClicked);
            captureToggleButton?.onClick.AddListener(OnCaptureToggleClicked);
            headingLeftButton?.onClick.AddListener(() => OnAdjustHeading(-5f));
            headingRightButton?.onClick.AddListener(() => OnAdjustHeading(5f));
            manualCalibrateButton?.onClick.AddListener(OnManualCalibrateClicked);

            if (nextMissionButton != null)
                nextMissionBtnText = nextMissionButton.GetComponentInChildren<TextMeshProUGUI>();

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += UpdateStateDisplay;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            TryBindCaptureEvent();
            UpdateCaptureUI(false);

            // ── 디버그 진단 (레이아웃 문제 추적) ──
            LogLayoutDiagnostics();

            // Idle(모드 선택) 상태에서는 HUD 숨김
            gameObject.SetActive(false);
        }

        private void LogLayoutDiagnostics()
        {
            var canvas = GetComponentInParent<Canvas>();
            var canvasRT = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            var adapter = canvas != null ? canvas.GetComponent<ARNavExperiment.Presentation.Shared.BeamProUIAdapter>() : null;

            Debug.Log($"[ExperimenterHUD] ── Layout Diagnostics ──\n" +
                $"  Screen={Screen.width}×{Screen.height}, DPI={Screen.dpi}\n" +
                $"  Canvas: renderMode={canvas?.renderMode}, sortOrder={canvas?.sortingOrder}\n" +
                $"  CanvasRT: rect={canvasRT?.rect}, sizeDelta={canvasRT?.sizeDelta}\n" +
                $"  Scaler: mode={scaler?.uiScaleMode}, refRes={scaler?.referenceResolution}, match={scaler?.matchWidthOrHeight}\n" +
                $"  BeamProUIAdapter: exists={adapter != null}, enabled={adapter?.enabled}");

            var hudRT = GetComponent<RectTransform>();
            Debug.Log($"[ExperimenterHUD] HUD rect={hudRT?.rect}, " +
                $"anchorMin={hudRT?.anchorMin}, anchorMax={hudRT?.anchorMax}");

            var statusArea = transform.Find("StatusArea");
            if (statusArea != null)
            {
                var statusRT = statusArea.GetComponent<RectTransform>();
                var hlg = statusArea.GetComponent<HorizontalLayoutGroup>();
                var vlg = statusArea.GetComponent<VerticalLayoutGroup>();
                Debug.Log($"[ExperimenterHUD] StatusArea: rect={statusRT?.rect}, " +
                    $"HLG={hlg != null}(enabled={hlg?.enabled}), " +
                    $"VLG={vlg != null}(enabled={vlg?.enabled})");
            }
            else
            {
                Debug.LogWarning("[ExperimenterHUD] StatusArea not found!");
            }

            // Display 정보
            Debug.Log($"[ExperimenterHUD] Display.main: sys={Display.main.systemWidth}×{Display.main.systemHeight}, " +
                $"render={Display.main.renderingWidth}×{Display.main.renderingHeight}");
        }

        private void OnEnable()
        {
            // HUD가 활성화될 때마다 레이아웃 진단 (Multi Resume 등 상태 변경 후)
            Debug.Log("[ExperimenterHUD] ── OnEnable Diagnostics ──");
            LogLayoutDiagnostics();
            StartCoroutine(DelayedLayoutDiagnostics());
        }

        private IEnumerator DelayedLayoutDiagnostics()
        {
            yield return new WaitForSeconds(3f);
            Debug.Log("[ExperimenterHUD] ── Delayed Diagnostics (3s after OnEnable) ──");
            LogLayoutDiagnostics();

            // 각 텍스트 자식의 실제 렌더 크기도 기록
            var statusArea = transform.Find("StatusArea");
            if (statusArea != null)
            {
                for (int i = 0; i < statusArea.childCount; i++)
                {
                    var child = statusArea.GetChild(i);
                    var rt = child.GetComponent<RectTransform>();
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[ExperimenterHUD] StatusChild[{i}] '{child.name}': " +
                        $"rect={rt?.rect}, text='{tmp?.text}', fontSize={tmp?.fontSize}");
                }
            }
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
                    ? string.Format(LocalizationManager.Get("hud.waypoint"), wp.waypointId,
                        dist >= 0 ? $"{dist:F1}" : "?")
                    : LocalizationManager.Get("hud.no_waypoint");
            }

            if (anchorStatusText != null && Navigation.WaypointManager.Instance != null)
            {
                int fallback = Navigation.WaypointManager.Instance.FallbackWaypointCount;
                anchorStatusText.text = fallback > 0
                    ? string.Format(LocalizationManager.Get("exphud.map_assist"), fallback)
                    : LocalizationManager.Get("exphud.anchors_ok");
            }

            if (headingOffsetText != null && Navigation.WaypointManager.Instance != null)
            {
                var wpm = Navigation.WaypointManager.Instance;
                bool weak = wpm.IsWeakCalibration();
                headingOffsetText.text = (weak ? "[!] " : "") +
                    $"H:{wpm.HeadingCalibrationOffset:F0}\u00b0({wpm.HeadingSource}) " +
                    $"C:{wpm.CalibrationSource}";
                headingOffsetText.color = weak ? Color.red : Color.white;
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
            bool shouldShow = state != ExperimentState.Idle;
            if (gameObject.activeSelf != shouldShow)
                gameObject.SetActive(shouldShow);

            if (stateText) stateText.text = string.Format(LocalizationManager.Get("hud.state"), state);
            if (conditionText)
            {
                if (state == ExperimentState.Running)
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

        private void OnAdjustHeading(float delta)
        {
            Navigation.WaypointManager.Instance?.AdjustHeadingOffset(delta);
        }

        private void OnManualCalibrateClicked()
        {
            Navigation.WaypointManager.Instance?.ManualCalibrateAtCurrentWaypoint();
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
