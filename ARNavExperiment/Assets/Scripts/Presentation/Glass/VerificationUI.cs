using UnityEngine;
using TMPro;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
namespace ARNavExperiment.Presentation.Glass
{
    public class VerificationUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button[] answerButtons;
        [SerializeField] private TextMeshProUGUI[] answerTexts;

        public bool LastAnswerCorrect { get; private set; }

        private System.Action<int, float> onAnswered;
        private float showTime;
        private int correctIndex;

        private void Awake()
        {
            if (panel == null)
            {
                panel = gameObject;
                Debug.LogWarning("[VerificationUI] panel null — self-wired to gameObject");
            }
            if (questionText == null)
            {
                var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name == "QuestionText") { questionText = t; break; }
                }
            }
            if (answerButtons == null || answerButtons.Length == 0)
            {
                var answersGO = panel.transform.Find("Answers");
                if (answersGO != null)
                {
                    answerButtons = answersGO.GetComponentsInChildren<Button>(true);
                    answerTexts = new TextMeshProUGUI[answerButtons.Length];
                    for (int i = 0; i < answerButtons.Length; i++)
                        answerTexts[i] = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                }
            }
            // 버튼 리스너 (Start에서 이동 — 비활성 오브젝트의 Start()는 Show() 후 다음 프레임에 실행되어 패널을 다시 숨김)
            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    int idx = i;
                    answerButtons[i]?.onClick.AddListener(() => OnAnswerSelected(idx));
                }
            }

            Debug.LogWarning($"[VerificationUI] Awake — panel={panel != null}, questionText={questionText != null}, " +
                $"answerButtons={answerButtons?.Length ?? 0}");
        }

        public void Show(MissionData mission, System.Action<int, float> callback)
        {
            onAnswered = callback;
            correctIndex = mission.correctAnswerIndex;
            showTime = Time.time;
            LastAnswerCorrect = false;

            if (questionText) questionText.text = mission.GetVerificationQuestion();

            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (i < mission.answerOptions.Length)
                {
                    answerButtons[i].gameObject.SetActive(true);
                    if (i < answerTexts.Length && answerTexts[i] != null)
                        answerTexts[i].text = $"{i + 1}. {mission.GetAnswerOption(i)}";
                }
                else
                {
                    answerButtons[i].gameObject.SetActive(false);
                }
            }

            if (panel)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panel) panel.SetActive(false);
            onAnswered = null;
        }

        private void OnAnswerSelected(int index)
        {
            LastAnswerCorrect = index == correctIndex;
            float rt = Time.time - showTime;
            // 응답 시간 정확도 유지: callback 즉시 호출, 페이드는 비동기
            var cb = onAnswered;
            onAnswered = null;
            if (panel) panel.SetActive(false);
            cb?.Invoke(index, rt);
        }
    }
}
