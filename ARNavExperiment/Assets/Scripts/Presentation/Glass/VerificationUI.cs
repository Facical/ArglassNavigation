using UnityEngine;
using TMPro;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Presentation.Shared;

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

        private void Start()
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                int idx = i;
                answerButtons[i]?.onClick.AddListener(() => OnAnswerSelected(idx));
            }
            if (panel) panel.SetActive(false);
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
                panel.GetComponent<PanelFader>()?.FadeIn();
            }
        }

        public void Hide()
        {
            var fader = panel?.GetComponent<PanelFader>();
            if (fader != null)
                fader.FadeOut(() => { onAnswered = null; });
            else
            {
                if (panel) panel.SetActive(false);
                onAnswered = null;
            }
        }

        private void OnAnswerSelected(int index)
        {
            LastAnswerCorrect = index == correctIndex;
            float rt = Time.time - showTime;
            // 응답 시간 정확도 유지: callback 즉시 호출, 페이드는 비동기
            var cb = onAnswered;
            onAnswered = null;
            var fader = panel?.GetComponent<PanelFader>();
            if (fader != null)
                fader.FadeOut();
            else if (panel)
                panel.SetActive(false);
            cb?.Invoke(index, rt);
        }
    }
}
