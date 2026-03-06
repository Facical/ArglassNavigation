using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.Experimenter
{
    public class ExperimentFlowUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject appModeSelectorPanel;
        [SerializeField] private GameObject relocalizationPanel;
        [SerializeField] private GameObject conditionTransitionPanel;
        [SerializeField] private GameObject surveyPromptPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("Relocalization")]
        [SerializeField] private RelocalizationUI relocalizationUI;

        [Header("Condition Transition")]
        [SerializeField] private TextMeshProUGUI transitionTitleText;
        [SerializeField] private TextMeshProUGUI transitionDetailText;

        [SerializeField] private Button transitionContinueButton;

        [Header("Survey Prompt")]
        [SerializeField] private TextMeshProUGUI surveyTitleText;
        [SerializeField] private TextMeshProUGUI surveyInstructionText;
        [SerializeField] private Button surveyDoneButton;

        [Header("Completion")]
        [SerializeField] private TextMeshProUGUI completionText;

        private void Start()
        {
            transitionContinueButton?.onClick.AddListener(OnTransitionContinue);
            surveyDoneButton?.onClick.AddListener(OnSurveyDone);

            if (ExperimentManager.Instance != null)
            {
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            HideAllPanels();

            // 앱 시작 시 모드 선택 화면 표시
            if (appModeSelectorPanel != null)
                appModeSelectorPanel.SetActive(true);
        }

        private void OnLanguageChanged(Language lang)
        {
            RefreshLocalization();
        }

        private void RefreshLocalization()
        {
            if (ExperimentManager.Instance != null)
                OnStateChanged(ExperimentManager.Instance.CurrentState);
        }

        private void HideAllPanels()
        {
            if (appModeSelectorPanel) appModeSelectorPanel.SetActive(false);
            if (relocalizationPanel) relocalizationPanel.SetActive(false);
            if (conditionTransitionPanel) conditionTransitionPanel.SetActive(false);
            if (surveyPromptPanel) surveyPromptPanel.SetActive(false);
            if (completionPanel) completionPanel.SetActive(false);
        }

        private void OnStateChanged(ExperimentState state)
        {
            // GlassOnly 조건에서는 ExperimenterCanvas 비활성 — GlassFlowUI가 전담
            if (ConditionController.Instance != null &&
                ConditionController.Instance.CurrentCondition == ExperimentCondition.GlassOnly)
            {
                HideAllPanels();
                return;
            }

            HideAllPanels();

            switch (state)
            {
                case ExperimentState.Relocalization:
                    ShowRelocalization();
                    break;

                case ExperimentState.Setup:
                    ShowSetupTransition();
                    break;

                case ExperimentState.Running:
                    ShowConditionStart();
                    break;

                case ExperimentState.Survey:
                    ShowSurveyPrompt();
                    break;

                case ExperimentState.Complete:
                    ShowCompletion();
                    break;
            }
        }

        private void ShowRelocalization()
        {
            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string route = session?.route;

            if (relocalizationUI != null)
                relocalizationUI.StartRelocalization(route);
        }

        private void ShowSetupTransition()
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            if (transitionTitleText)
                transitionTitleText.text = LocalizationManager.Get("flow.setup_title");
            if (transitionDetailText)
                transitionDetailText.text = string.Format(
                    LocalizationManager.Get("flow.setup_detail"),
                    session?.participantId, session?.condition, session?.route);
        }

        private void ShowConditionStart()
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = session?.condition;
            string route = session?.route;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");
            string routeLabel = route == "A"
                ? LocalizationManager.Get("session.route_a")
                : LocalizationManager.Get("session.route_b");

            if (transitionTitleText)
                transitionTitleText.text = LocalizationManager.Get("flow.condition_title");
            if (transitionDetailText)
            {
                string deviceInfo = condition == "glass_only"
                    ? LocalizationManager.Get("flow.condition_detail_glass")
                    : LocalizationManager.Get("flow.condition_detail_hybrid");

                transitionDetailText.text = string.Format(
                    LocalizationManager.Get("flow.condition_info"),
                    conditionLabel, routeLabel, deviceInfo);
            }

            // Load missions for this route
            MissionManager.Instance?.LoadMissions(route);
        }

        private void ShowSurveyPrompt()
        {
            if (surveyPromptPanel == null) return;
            surveyPromptPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = session?.condition;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");

            if (surveyTitleText)
                surveyTitleText.text = string.Format(
                    LocalizationManager.Get("flow.survey_title_single"), conditionLabel);
            if (surveyInstructionText)
                surveyInstructionText.text = LocalizationManager.Get("flow.survey_instr");
        }

        private void ShowCompletion()
        {
            if (completionPanel == null) return;
            completionPanel.SetActive(true);

            if (completionText)
                completionText.text = LocalizationManager.Get("flow.complete_text");
        }

        // === Button Callbacks ===

        private void OnTransitionContinue()
        {
            var state = ExperimentManager.Instance?.CurrentState;
            if (state == ExperimentState.Setup)
            {
                conditionTransitionPanel?.SetActive(false);
                ExperimentManager.Instance?.AdvanceState(); // → Running
            }
            else if (state == ExperimentState.Running)
            {
                var mm = MissionManager.Instance;
                if (mm == null || !mm.HasLoadedMissions)
                {
                    Debug.LogWarning("[ExperimentFlowUI] Missions not loaded — retrying LoadMissions...");
                    var session = ExperimentManager.Instance?.session;
                    string route = session?.route;
                    if (mm != null && route != null)
                        mm.LoadMissions(route);

                    if (mm == null || !mm.HasLoadedMissions)
                    {
                        Debug.LogError("[ExperimentFlowUI] Missions still not loaded after retry");
                        if (transitionDetailText != null)
                        {
                            transitionDetailText.text = LocalizationManager.Get("flow.error_missions")
                                + "\n\n" + LocalizationManager.Get("flow.error_missions_hint");
                        }
                        return;
                    }
                }
                conditionTransitionPanel?.SetActive(false);
                mm.StartNextMission();
            }
        }

        private void OnSurveyDone()
        {
            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}
