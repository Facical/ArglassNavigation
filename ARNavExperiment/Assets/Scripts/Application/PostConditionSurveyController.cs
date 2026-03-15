using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// NASA-TLX(6항목) + Trust(7항목) = 13항목 순차 진행 오케스트레이터.
    /// ExperimentCanvas에 배치, 글래스에서 핸드트래킹 pinch로 조작.
    /// Survey 상태 진입 시 ExperimentManager가 호출.
    /// </summary>
    public class PostConditionSurveyController : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject surveyPanel;
        [SerializeField] private TextMeshProUGUI sectionHeaderText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI lowLabelText;
        [SerializeField] private TextMeshProUGUI highLabelText;
        [SerializeField] private TextMeshProUGUI currentRatingText;
        [SerializeField] private Button[] ratingButtons; // 7 buttons
        [SerializeField] private Button confirmButton;

        private static readonly SurveyItem[] Items = {
            // NASA-TLX (6)
            new("nasa_tlx", "mental_demand", "survey.nasa_mental_demand"),
            new("nasa_tlx", "physical_demand", "survey.nasa_physical_demand"),
            new("nasa_tlx", "temporal_demand", "survey.nasa_temporal_demand"),
            new("nasa_tlx", "performance", "survey.nasa_performance"),
            new("nasa_tlx", "effort", "survey.nasa_effort"),
            new("nasa_tlx", "frustration", "survey.nasa_frustration"),
            // Trust (7)
            new("trust", "direction", "survey.trust_direction"),
            new("trust", "reliability", "survey.trust_reliability"),
            new("trust", "confidence", "survey.trust_confidence"),
            new("trust", "accuracy", "survey.trust_accuracy"),
            new("trust", "safety", "survey.trust_safety"),
            new("trust", "destination_belief", "survey.trust_destination"),
            new("trust", "willingness_reuse", "survey.trust_reuse"),
        };

        private int currentIndex;
        private int selectedRating = -1;
        private System.Action onComplete;
        private int runNumber;
        private string condition;
        private float startTime;
        private bool isActive;
        private bool buttonsBound;

        private void Awake()
        {
            // ExperimentManager 상태 변화 구독 (항상 활성 GO이므로 Awake 보장)
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;
        }

        /// <summary>버튼 리스너를 한 번만 바인딩합니다. 패널 활성화 후 호출.</summary>
        private void BindButtons()
        {
            if (buttonsBound) return;

            if (ratingButtons != null)
            {
                for (int i = 0; i < ratingButtons.Length; i++)
                {
                    int rating = i + 1;
                    ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
                }
            }
            confirmButton?.onClick.AddListener(OnConfirm);
            buttonsBound = true;
        }

        private void OnStateChanged(ExperimentState state)
        {
            if (state == ExperimentState.Survey)
            {
                var em = ExperimentManager.Instance;
                StartSurvey(
                    em.RunNumber,
                    em.session?.condition ?? "unknown",
                    () => em.AdvanceState()
                );
            }
            else if (isActive && state != ExperimentState.Survey)
            {
                // 예기치 않은 상태 전환 시 설문 중단
                Hide();
            }
        }

        public void StartSurvey(int runNum, string cond, System.Action onCompleteCallback)
        {
            runNumber = runNum;
            condition = cond;
            onComplete = onCompleteCallback;
            currentIndex = 0;
            startTime = Time.time;
            isActive = true;

            if (surveyPanel != null)
                surveyPanel.SetActive(true);

            // 패널 활성화 후 버튼 바인딩 (비활성 패널의 버튼은 Awake 시점에 접근 불가)
            BindButtons();

            ShowCurrentItem();

            Debug.Log($"[PostConditionSurvey] Started — run={runNumber}, condition={condition}");
        }

        public void Hide()
        {
            isActive = false;
            if (surveyPanel != null)
                surveyPanel.SetActive(false);
        }

        private void ShowCurrentItem()
        {
            if (currentIndex >= Items.Length) return;

            var item = Items[currentIndex];
            selectedRating = -1;

            // Section header
            string sectionName = item.SurveyType == "nasa_tlx"
                ? LocalizationManager.Get("survey.nasa_section")
                : LocalizationManager.Get("survey.trust_section");

            // 섹션 내 인덱스 계산
            int sectionStart = item.SurveyType == "nasa_tlx" ? 0 : 6;
            int sectionTotal = item.SurveyType == "nasa_tlx" ? 6 : 7;
            int sectionIndex = currentIndex - sectionStart + 1;

            if (sectionHeaderText != null)
                sectionHeaderText.text = string.Format(
                    LocalizationManager.Get("survey.section_header"),
                    sectionName, sectionIndex, sectionTotal);

            if (progressText != null)
                progressText.text = string.Format(
                    LocalizationManager.Get("survey.progress"),
                    currentIndex + 1, Items.Length);

            if (promptText != null)
                promptText.text = LocalizationManager.Get(item.PromptKey);

            if (lowLabelText != null)
                lowLabelText.text = LocalizationManager.Get("survey.low_label");
            if (highLabelText != null)
                highLabelText.text = LocalizationManager.Get("survey.high_label");

            if (currentRatingText != null)
                currentRatingText.text = "";

            if (confirmButton != null)
            {
                var btnText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = LocalizationManager.Get("survey.confirm");
            }
        }

        private void SelectRating(int rating)
        {
            selectedRating = rating;
            if (currentRatingText != null)
                currentRatingText.text = string.Format(
                    LocalizationManager.Get("survey.rating_label"), rating);
        }

        private void OnConfirm()
        {
            if (selectedRating < 1 || !isActive) return;
            if (currentIndex >= Items.Length) return;

            var item = Items[currentIndex];

            // 도메인 이벤트 발행
            DomainEventBus.Instance?.Publish(new SurveyItemAnswered(
                item.SurveyType, item.ItemKey, selectedRating, runNumber, condition));

            currentIndex++;

            if (currentIndex < Items.Length)
            {
                // 섹션 전환 체크 (nasa_tlx → trust)
                if (currentIndex == 6)
                {
                    float nasaDuration = Time.time - startTime;
                    DomainEventBus.Instance?.Publish(new SurveyCompleted(
                        "nasa_tlx", runNumber, condition, nasaDuration));
                }

                ShowCurrentItem();
            }
            else
            {
                // 전체 완료
                float totalDuration = Time.time - startTime;
                DomainEventBus.Instance?.Publish(new SurveyCompleted(
                    "trust", runNumber, condition, totalDuration - (currentIndex > 6 ? 0 : totalDuration)));
                DomainEventBus.Instance?.Publish(new SurveyCompleted(
                    "post_condition", runNumber, condition, totalDuration));

                Hide();
                Debug.Log($"[PostConditionSurvey] Completed — duration={totalDuration:F0}s");

                var cb = onComplete;
                onComplete = null;
                cb?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private readonly struct SurveyItem
        {
            public readonly string SurveyType;
            public readonly string ItemKey;
            public readonly string PromptKey;

            public SurveyItem(string surveyType, string itemKey, string promptKey)
            {
                SurveyType = surveyType;
                ItemKey = itemKey;
                PromptKey = promptKey;
            }
        }
    }
}
