using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Presentation.BeamPro
{
    /// <summary>
    /// BeamProCanvas에서 비교 설문을 진행하는 UI.
    /// 5페이지: 선호 조건(선택), 신뢰 비교(선택), 선호 이유(텍스트), 전환 행동(텍스트), 제안(텍스트).
    /// ComparisonSurveyController가 제어.
    /// </summary>
    public class ComparisonSurveyUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject surveyPanel;

        [Header("Navigation")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI pageText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button submitButton;

        [Header("Page 1 - Preferred Condition")]
        [SerializeField] private GameObject page1Panel;
        [SerializeField] private TextMeshProUGUI page1QuestionText;
        [SerializeField] private Button glassOnlyButton;
        [SerializeField] private Button hybridButton;
        [SerializeField] private Button noDiffButton;

        [Header("Page 2 - Trust Comparison")]
        [SerializeField] private GameObject page2Panel;
        [SerializeField] private TextMeshProUGUI page2QuestionText;
        [SerializeField] private Button glassHigherButton;
        [SerializeField] private Button hybridHigherButton;
        [SerializeField] private Button sameButton;

        [Header("Page 3 - Preference Reason")]
        [SerializeField] private GameObject page3Panel;
        [SerializeField] private TextMeshProUGUI page3QuestionText;
        [SerializeField] private TMP_InputField page3Input;

        [Header("Page 4 - Switching Behavior")]
        [SerializeField] private GameObject page4Panel;
        [SerializeField] private TextMeshProUGUI page4QuestionText;
        [SerializeField] private TMP_InputField page4Input;

        [Header("Page 5 - Suggestions")]
        [SerializeField] private GameObject page5Panel;
        [SerializeField] private TextMeshProUGUI page5QuestionText;
        [SerializeField] private TMP_InputField page5Input;

        private int currentPage;
        private readonly string[] answers = new string[5];
        private readonly string[] questionKeys = {
            "preferred_condition", "trust_comparison",
            "preference_reason", "switching_behavior", "suggestions"
        };
        private readonly string[] responseTypes = {
            "choice", "choice", "text", "text", "text"
        };

        private System.Action<string, string, string> onItemAnswered;
        private System.Action onCompleted;

        private void Awake()
        {
            prevButton?.onClick.AddListener(OnPrev);
            nextButton?.onClick.AddListener(OnNext);
            submitButton?.onClick.AddListener(OnSubmit);

            // Choice buttons
            glassOnlyButton?.onClick.AddListener(() => SetChoiceAnswer(0, "glass_only"));
            hybridButton?.onClick.AddListener(() => SetChoiceAnswer(0, "hybrid"));
            noDiffButton?.onClick.AddListener(() => SetChoiceAnswer(0, "no_difference"));

            glassHigherButton?.onClick.AddListener(() => SetChoiceAnswer(1, "glass_higher"));
            hybridHigherButton?.onClick.AddListener(() => SetChoiceAnswer(1, "hybrid_higher"));
            sameButton?.onClick.AddListener(() => SetChoiceAnswer(1, "same"));
        }

        public void StartSurvey(
            System.Action<string, string, string> itemCallback,
            System.Action completeCallback)
        {
            onItemAnswered = itemCallback;
            onCompleted = completeCallback;
            currentPage = 0;

            for (int i = 0; i < answers.Length; i++)
                answers[i] = "";

            // 텍스트 입력 초기화
            if (page3Input != null) page3Input.text = "";
            if (page4Input != null) page4Input.text = "";
            if (page5Input != null) page5Input.text = "";

            RefreshLocalization();
            ShowPage(0);

            if (surveyPanel != null)
                surveyPanel.SetActive(true);
        }

        private void RefreshLocalization()
        {
            if (titleText != null)
                titleText.text = LocalizationManager.Get("comparison.title");
            if (page1QuestionText != null)
                page1QuestionText.text = LocalizationManager.Get("comparison.preferred");
            if (page2QuestionText != null)
                page2QuestionText.text = LocalizationManager.Get("comparison.trust_compare");
            if (page3QuestionText != null)
                page3QuestionText.text = LocalizationManager.Get("comparison.preference_reason");
            if (page4QuestionText != null)
                page4QuestionText.text = LocalizationManager.Get("comparison.switching_behavior");
            if (page5QuestionText != null)
                page5QuestionText.text = LocalizationManager.Get("comparison.suggestions");

            // Choice button labels
            SetButtonLabel(glassOnlyButton, "comparison.glass_only");
            SetButtonLabel(hybridButton, "comparison.hybrid");
            SetButtonLabel(noDiffButton, "comparison.no_difference");
            SetButtonLabel(glassHigherButton, "comparison.glass_higher");
            SetButtonLabel(hybridHigherButton, "comparison.hybrid_higher");
            SetButtonLabel(sameButton, "comparison.same");

            // Input placeholders
            SetInputPlaceholder(page3Input, "comparison.text_placeholder");
            SetInputPlaceholder(page4Input, "comparison.text_placeholder");
            SetInputPlaceholder(page5Input, "comparison.text_placeholder");

            SetButtonLabel(prevButton, "comparison.prev");
            SetButtonLabel(nextButton, "comparison.next");
            SetButtonLabel(submitButton, "comparison.submit");
        }

        private void ShowPage(int page)
        {
            currentPage = page;

            if (page1Panel != null) page1Panel.SetActive(page == 0);
            if (page2Panel != null) page2Panel.SetActive(page == 1);
            if (page3Panel != null) page3Panel.SetActive(page == 2);
            if (page4Panel != null) page4Panel.SetActive(page == 3);
            if (page5Panel != null) page5Panel.SetActive(page == 4);

            if (pageText != null)
                pageText.text = string.Format(
                    LocalizationManager.Get("comparison.page"), page + 1, 5);

            if (prevButton != null)
                prevButton.gameObject.SetActive(page > 0);
            if (nextButton != null)
                nextButton.gameObject.SetActive(page < 4);
            if (submitButton != null)
                submitButton.gameObject.SetActive(page == 4);

            // 선택형 버튼 강조 갱신
            if (page == 0) UpdateChoiceHighlight(0, glassOnlyButton, hybridButton, noDiffButton,
                "glass_only", "hybrid", "no_difference");
            if (page == 1) UpdateChoiceHighlight(1, glassHigherButton, hybridHigherButton, sameButton,
                "glass_higher", "hybrid_higher", "same");
        }

        private void SetChoiceAnswer(int pageIndex, string answer)
        {
            answers[pageIndex] = answer;

            // 선택 버튼 강조
            if (pageIndex == 0)
                UpdateChoiceHighlight(0, glassOnlyButton, hybridButton, noDiffButton,
                    "glass_only", "hybrid", "no_difference");
            else if (pageIndex == 1)
                UpdateChoiceHighlight(1, glassHigherButton, hybridHigherButton, sameButton,
                    "glass_higher", "hybrid_higher", "same");
        }

        private void UpdateChoiceHighlight(int pageIndex, Button btn1, Button btn2, Button btn3,
            string val1, string val2, string val3)
        {
            string selected = answers[pageIndex];
            HighlightButton(btn1, selected == val1);
            HighlightButton(btn2, selected == val2);
            HighlightButton(btn3, selected == val3);
        }

        private void HighlightButton(Button btn, bool selected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = selected
                    ? new Color(0.2f, 0.5f, 0.8f, 1f)
                    : new Color(0.3f, 0.3f, 0.35f, 1f);
        }

        private void OnPrev()
        {
            CollectTextAnswer();
            if (currentPage > 0)
                ShowPage(currentPage - 1);
        }

        private void OnNext()
        {
            CollectTextAnswer();

            // 선택형 페이지(0, 1)는 응답 필수
            if (currentPage <= 1 && string.IsNullOrEmpty(answers[currentPage]))
                return;

            // 현재 페이지 응답 발행
            PublishCurrentAnswer();

            if (currentPage < 4)
                ShowPage(currentPage + 1);
        }

        private void OnSubmit()
        {
            CollectTextAnswer();

            // 모든 응답 발행
            for (int i = 0; i < answers.Length; i++)
            {
                onItemAnswered?.Invoke(questionKeys[i], answers[i], responseTypes[i]);
            }

            if (surveyPanel != null)
                surveyPanel.SetActive(false);

            onCompleted?.Invoke();
        }

        private void CollectTextAnswer()
        {
            if (currentPage == 2 && page3Input != null)
                answers[2] = page3Input.text;
            else if (currentPage == 3 && page4Input != null)
                answers[3] = page4Input.text;
            else if (currentPage == 4 && page5Input != null)
                answers[4] = page5Input.text;
        }

        private void PublishCurrentAnswer()
        {
            if (currentPage < answers.Length && !string.IsNullOrEmpty(answers[currentPage]))
            {
                onItemAnswered?.Invoke(
                    questionKeys[currentPage],
                    answers[currentPage],
                    responseTypes[currentPage]);
            }
        }

        private static void SetButtonLabel(Button btn, string locKey)
        {
            if (btn == null) return;
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = LocalizationManager.Get(locKey);
        }

        private static void SetInputPlaceholder(TMP_InputField input, string locKey)
        {
            if (input == null) return;
            var ph = input.placeholder as TextMeshProUGUI;
            if (ph != null)
                ph.text = LocalizationManager.Get(locKey);
        }
    }
}
