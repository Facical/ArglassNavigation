using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.UI
{
    public class ExperimentFlowUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject sessionSetupPanel;
        [SerializeField] private GameObject practicePanel;
        [SerializeField] private GameObject conditionTransitionPanel;
        [SerializeField] private GameObject surveyPromptPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("Practice Panel")]
        [SerializeField] private TextMeshProUGUI practiceInstructionText;
        [SerializeField] private Button practiceStartButton;
        [SerializeField] private Button practiceSkipButton;

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
            practiceStartButton?.onClick.AddListener(OnPracticeStart);
            practiceSkipButton?.onClick.AddListener(OnPracticeSkip);
            transitionContinueButton?.onClick.AddListener(OnTransitionContinue);
            surveyDoneButton?.onClick.AddListener(OnSurveyDone);

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;

            HideAllPanels();
            if (sessionSetupPanel != null)
                sessionSetupPanel.SetActive(true);
        }

        private void HideAllPanels()
        {
            if (sessionSetupPanel) sessionSetupPanel.SetActive(false);
            if (practicePanel) practicePanel.SetActive(false);
            if (conditionTransitionPanel) conditionTransitionPanel.SetActive(false);
            if (surveyPromptPanel) surveyPromptPanel.SetActive(false);
            if (completionPanel) completionPanel.SetActive(false);
        }

        private void OnStateChanged(ExperimentState state)
        {
            HideAllPanels();

            switch (state)
            {
                case ExperimentState.Setup:
                    ShowSetupTransition();
                    break;

                case ExperimentState.Practice:
                    ShowPractice();
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

        private void ShowSetupTransition()
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            if (transitionTitleText)
                transitionTitleText.text = "실험 준비";
            if (transitionDetailText)
                transitionDetailText.text =
                    $"참가자: {session?.participantId}\n" +
                    $"그룹: {session?.orderGroup}\n\n" +
                    "글래스 착용과 기기 연결을 확인한 후\n" +
                    "'계속' 버튼을 눌러주세요.";
        }

        private void ShowPractice()
        {
            if (practicePanel == null) return;
            practicePanel.SetActive(true);

            if (practiceInstructionText)
                practiceInstructionText.text =
                    "연습 안내\n\n" +
                    "1. 글래스에 보이는 화살표를 따라 이동합니다.\n" +
                    "2. 목적지에 도착하면 미션이 나타납니다.\n" +
                    "3. 질문에 답한 후 확신도를 평가합니다.\n\n" +
                    "준비가 되면 '연습 시작' 버튼을 눌러주세요.";
        }

        private void ShowConditionStart(int conditionNum)
        {
            if (conditionTransitionPanel == null) return;
            conditionTransitionPanel.SetActive(true);

            var session = ExperimentManager.Instance?.session;
            string condition = conditionNum == 1 ? session?.FirstCondition : session?.SecondCondition;
            string route = conditionNum == 1 ? session?.firstRoute : session?.secondRoute;
            string conditionKr = condition == "glass_only" ? "Glass Only (글래스만)" : "Hybrid (글래스 + 스마트폰)";
            string routeKr = route == "A" ? "A (서쪽-북쪽)" : "B (동쪽-북쪽)";

            if (transitionTitleText)
                transitionTitleText.text = $"조건 {conditionNum} 시작";
            if (transitionDetailText)
            {
                string deviceInfo = condition == "glass_only"
                    ? "글래스의 AR 화살표만 사용합니다.\n스마트폰은 사용하지 마세요."
                    : "글래스의 AR 화살표와 스마트폰의\n정보 허브를 모두 사용할 수 있습니다.";

                transitionDetailText.text =
                    $"조건: {conditionKr}\n" +
                    $"경로: Route {routeKr}\n\n" +
                    $"{deviceInfo}\n\n" +
                    "시작점(계단)에서 출발합니다.\n" +
                    "준비가 되면 '계속' 버튼을 눌러주세요.";
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
            string conditionKr = condition == "glass_only" ? "Glass Only" : "Hybrid";

            if (surveyTitleText)
                surveyTitleText.text = $"조건 {surveyNum} 설문 ({conditionKr})";
            if (surveyInstructionText)
                surveyInstructionText.text =
                    "이 조건의 경험에 대한 설문을 진행합니다.\n\n" +
                    "연구자가 태블릿/종이 설문지를 전달합니다.\n" +
                    "- NASA-TLX (작업 부하)\n" +
                    "- 시스템 신뢰 척도\n\n" +
                    "설문 완료 후 '설문 완료' 버튼을 눌러주세요.";
        }

        private void ShowPostSurvey()
        {
            if (surveyPromptPanel == null) return;
            surveyPromptPanel.SetActive(true);

            if (surveyTitleText)
                surveyTitleText.text = "사후 설문";
            if (surveyInstructionText)
                surveyInstructionText.text =
                    "전체 실험에 대한 최종 설문을 진행합니다.\n\n" +
                    "- 기기 비교 선호도\n" +
                    "- 개방형 질문\n\n" +
                    "설문 완료 후 '설문 완료' 버튼을 눌러주세요.";
        }

        private void ShowCompletion()
        {
            if (completionPanel == null) return;
            completionPanel.SetActive(true);

            if (completionText)
                completionText.text =
                    "실험이 완료되었습니다!\n\n" +
                    "참여해 주셔서 감사합니다.\n" +
                    "데이터가 자동 저장되었습니다.\n\n" +
                    "연구자에게 알려주세요.";
        }

        // === Button Callbacks ===

        private void OnPracticeStart()
        {
            if (practicePanel) practicePanel.SetActive(false);
            // 연습은 간략하게 진행 후 자동으로 조건 1로 전환
            // 실제 실험에서는 짧은 연습 경로를 걷고 연구자가 AdvanceState 호출
            Debug.Log("[ExperimentFlowUI] 연습 시작 — 연습 완료 후 'N' 키 또는 HUD Advance 버튼으로 다음 단계");
        }

        private void OnPracticeSkip()
        {
            ExperimentManager.Instance?.AdvanceState(); // → Condition1
        }

        private void OnTransitionContinue()
        {
            conditionTransitionPanel?.SetActive(false);
            var state = ExperimentManager.Instance?.CurrentState;
            if (state == ExperimentState.Setup)
            {
                ExperimentManager.Instance?.AdvanceState(); // → Practice
            }
            else if (state == ExperimentState.Condition1 || state == ExperimentState.Condition2)
            {
                // Start first mission
                MissionManager.Instance?.StartNextMission();
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
        }
    }
}
