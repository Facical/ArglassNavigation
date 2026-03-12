using UnityEngine;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.Glass
{
    /// <summary>
    /// 글래스(ExperimentCanvas)에 표시되는 참가자용 HUD.
    /// 표시 전용 — 제어 버튼은 ExperimenterHUD(ExperimenterCanvas)로 이동됨.
    /// </summary>
    public class ExperimentHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += UpdateStateDisplay;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            // Idle(모드 선택) 상태에서는 디버그 HUD를 숨김
            gameObject.SetActive(false);
        }

        private void SetHUDVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void Update()
        {
            // GlassFlowUI 패널 또는 미션 입력 UI 표시 중 HUD 숨김
            bool hideForPanel = GlassFlowUI.HasActivePanel;

            if (!hideForPanel && MissionManager.Instance != null)
            {
                var ms = MissionManager.Instance.CurrentState;
                hideForPanel = ms == MissionState.Briefing
                            || ms == MissionState.Verification
                            || ms == MissionState.ConfidenceRating
                            || ms == MissionState.DifficultyRating;
            }

            SetHUDVisible(!hideForPanel);
            if (hideForPanel) return;

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

                // 진단: 글래스 캔버스 내 활성 패널 수 표시
                int activeCount = 0;
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    foreach (Transform child in canvas.transform)
                    {
                        if (child.gameObject.activeSelf && child.gameObject != gameObject)
                            activeCount++;
                    }
                }
                waypointText.text += $"\n[Panels: {activeCount}]";
            }

        }

        private void UpdateStateDisplay(ExperimentState state)
        {
            // Idle(모드 선택) 상태에서는 HUD 숨김, 그 외 상태에서는 표시
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
            // 현재 상태를 기반으로 모든 표시 텍스트를 갱신
            if (ExperimentManager.Instance != null)
                UpdateStateDisplay(ExperimentManager.Instance.CurrentState);
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }
    }
}
