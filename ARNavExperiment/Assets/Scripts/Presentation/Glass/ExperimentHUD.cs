using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.Glass
{
    /// <summary>
    /// 글래스(ExperimentCanvas)에 표시되는 참가자용 HUD.
    /// Glass Only 조건에서는 도착선언 버튼을 표시하여 참가자가 직접 도착을 선언할 수 있음.
    /// </summary>
    public class ExperimentHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        [Header("Glass ForceArrival")]
        [SerializeField] private Button forceArrivalButton;

        [Header("Hybrid Phone Prompt")]
        [SerializeField] private TextMeshProUGUI phonePromptText;

        private CanvasGroup _canvasGroup;
        private bool isGlassOnly;

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

            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            forceArrivalButton?.onClick.AddListener(OnForceArrivalClicked);

            // 초기 상태: 버튼 숨김
            if (forceArrivalButton != null)
                forceArrivalButton.gameObject.SetActive(false);

            // Idle(모드 선택) 상태에서는 디버그 HUD를 숨김
            gameObject.SetActive(false);
        }

        private void OnConditionChanged(ExperimentCondition condition)
        {
            isGlassOnly = condition == ExperimentCondition.GlassOnly;
            UpdateForceArrivalVisibility();
        }

        private void OnForceArrivalClicked()
        {
            MissionManager.Instance?.ForceArrival();
        }

        private void UpdateForceArrivalVisibility()
        {
            if (forceArrivalButton == null) return;
            bool show = isGlassOnly
                && MissionManager.Instance != null
                && MissionManager.Instance.CurrentState == MissionState.Navigation;
            forceArrivalButton.gameObject.SetActive(show);
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

            bool showPhonePrompt = false;
            if (!hideForPanel && MissionManager.Instance != null)
            {
                var ms = MissionManager.Instance.CurrentState;
                if (!isGlassOnly)
                {
                    // Hybrid: Briefing/Verification/Rating → "폰을 확인하세요" 안내 표시
                    bool needsPhone = ms == MissionState.Briefing
                                   || ms == MissionState.Verification
                                   || ms == MissionState.ConfidenceRating
                                   || ms == MissionState.DifficultyRating;
                    if (needsPhone)
                    {
                        showPhonePrompt = true;
                    }
                    else
                    {
                        // Navigation/Idle/Scored → 기존 HUD 표시
                        hideForPanel = ms != MissionState.Idle && ms != MissionState.Scored
                                    && ms != MissionState.Navigation && ms != MissionState.Arrival;
                    }
                }
                else
                {
                    // GlassOnly: Briefing/Verification/Rating 중만 HUD 숨김 (기존 동작)
                    hideForPanel = ms == MissionState.Briefing
                                || ms == MissionState.Verification
                                || ms == MissionState.ConfidenceRating
                                || ms == MissionState.DifficultyRating;
                }
            }

            // 폰 안내 텍스트 표시/숨김
            if (phonePromptText != null)
            {
                phonePromptText.gameObject.SetActive(showPhonePrompt);
                if (showPhonePrompt)
                    phonePromptText.text = LocalizationManager.Get("hud.see_phone");
            }

            SetHUDVisible(!hideForPanel);
            if (hideForPanel && !showPhonePrompt) return;

            // 도착선언 버튼 표시 상태 매 프레임 갱신
            UpdateForceArrivalVisibility();

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
            // Idle, ComparisonSurvey 상태에서는 HUD 숨김
            bool shouldShow = state != ExperimentState.Idle
                && state != ExperimentState.ComparisonSurvey;
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

            UpdateForceArrivalVisibility();
        }

        private void RefreshLocalization(Language lang)
        {
            if (ExperimentManager.Instance != null)
                UpdateStateDisplay(ExperimentManager.Instance.CurrentState);

            // 도착선언 버튼 텍스트 갱신
            if (forceArrivalButton != null)
            {
                var btnText = forceArrivalButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = LocalizationManager.Get("glass.force_arrival");
            }
        }

        private void OnDestroy()
        {
            forceArrivalButton?.onClick.RemoveListener(OnForceArrivalClicked);

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;

            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }
    }
}
