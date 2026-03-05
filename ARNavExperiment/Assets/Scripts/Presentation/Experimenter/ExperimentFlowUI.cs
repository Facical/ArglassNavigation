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
        [SerializeField] private GameObject sessionSetupPanel;
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
            else if (sessionSetupPanel != null)
                sessionSetupPanel.SetActive(true);
        }

        private void OnLanguageChanged(Language lang)
        {
            RefreshLocalization();
        }

        private void RefreshLocalization()
        {
            // Re-apply text for the current state
            if (ExperimentManager.Instance != null)
                OnStateChanged(ExperimentManager.Instance.CurrentState);
        }

        private void HideAllPanels()
        {
            if (appModeSelectorPanel) appModeSelectorPanel.SetActive(false);
            if (relocalizationPanel) relocalizationPanel.SetActive(false);
            if (sessionSetupPanel) sessionSetupPanel.SetActive(false);
            if (conditionTransitionPanel) conditionTransitionPanel.SetActive(false);
            if (surveyPromptPanel) surveyPromptPanel.SetActive(false);
            if (completionPanel) completionPanel.SetActive(false);
        }

        private void OnStateChanged(ExperimentState state)
        {
            HideAllPanels();

            switch (state)
            {
                case ExperimentState.Relocalization:
                    ShowRelocalization();
                    break;

                case ExperimentState.Setup:
                    ShowSetupTransition();
                    break;

                case ExperimentState.Condition1:
                    ShowConditionStart(1);
                    break;

                case ExperimentState.Survey1:
                    ShowSurveyPrompt(1);
                    break;

                case ExperimentState.Condition2:
                    ShowConditionStart(2);
                    break;

                case ExperimentState.Survey2:
                    ShowSurveyPrompt(2);
                    break;

                case ExperimentState.PostSurvey:
                    ShowPostSurvey();
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

            // 세션이 초기화되어 있으면 첫 경로 앵커만 로드
            var session = ExperimentManager.Instance?.session;
            string firstRoute = session?.firstRoute;

            if (relocalizationUI != null)
                relocalizationUI.StartRelocalization(firstRoute);
        }

        private void ShowSetupTransition()
        {
            // 세션은 Relocalization 이전에 항상 초기화됨
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            if (transitionTitleText)
                transitionTitleText.text = LocalizationManager.Get("flow.setup_title");
            if (transitionDetailText)
                transitionDetailText.text = string.Format(
                    LocalizationManager.Get("flow.setup_detail"),
                    session?.participantId, session?.orderGroup);
        }

        private void ShowConditionStart(int conditionNum)
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = conditionNum == 1 ? session?.FirstCondition : session?.SecondCondition;
            string route = conditionNum == 1 ? session?.firstRoute : session?.secondRoute;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");
            string routeLabel = route == "A"
                ? LocalizationManager.Get("session.route_a")
                : LocalizationManager.Get("session.route_b");

            if (transitionTitleText)
                transitionTitleText.text = string.Format(
                    LocalizationManager.Get("flow.condition_title"), conditionNum);
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

        private void ShowSurveyPrompt(int surveyNum)
        {
            if (surveyPromptPanel == null) return;
            surveyPromptPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = surveyNum == 1 ? session?.FirstCondition : session?.SecondCondition;
            string conditionLabel = condition == "glass_only"
                ? LocalizationManager.Get("flow.condition_glass")
                : LocalizationManager.Get("flow.condition_hybrid");

            if (surveyTitleText)
                surveyTitleText.text = string.Format(
                    LocalizationManager.Get("flow.survey_title"), surveyNum, conditionLabel);
            if (surveyInstructionText)
                surveyInstructionText.text = LocalizationManager.Get("flow.survey_instr");
        }

        private void ShowPostSurvey()
        {
            if (surveyPromptPanel == null) return;
            surveyPromptPanel.SetActive(true);

            if (surveyTitleText)
                surveyTitleText.text = LocalizationManager.Get("flow.post_survey_title");
            if (surveyInstructionText)
                surveyInstructionText.text = LocalizationManager.Get("flow.post_survey_instr");
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
                ExperimentManager.Instance?.AdvanceState(); // → Condition1
            }
            else if (state == ExperimentState.Condition1 || state == ExperimentState.Condition2)
            {
                var mm = MissionManager.Instance;
                if (mm == null || !mm.HasLoadedMissions)
                {
                    // 재시도: 경로 정보를 다시 가져와서 LoadMissions 재호출
                    Debug.LogWarning("[ExperimentFlowUI] Missions not loaded — retrying LoadMissions...");
                    var session = ExperimentManager.Instance?.session;
                    int condNum = state == ExperimentState.Condition1 ? 1 : 2;
                    string route = condNum == 1 ? session?.firstRoute : session?.secondRoute;
                    if (mm != null && route != null)
                        mm.LoadMissions(route);

                    // 재시도 후 다시 체크
                    if (mm == null || !mm.HasLoadedMissions)
                    {
                        Debug.LogError("[ExperimentFlowUI] Missions still not loaded after retry");
                        if (transitionDetailText != null)
                        {
                            transitionDetailText.text = LocalizationManager.Get("flow.error_missions")
                                + "\n\n" + LocalizationManager.Get("flow.error_missions_hint");
                        }
                        return; // 패널을 닫지 않음 — 실험자가 다시 시도 가능
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
