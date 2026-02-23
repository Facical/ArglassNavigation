using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ARNavExperiment.Mission
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

            if (questionText) questionText.text = mission.verificationQuestion;

            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (i < mission.answerOptions.Length)
                {
                    answerButtons[i].gameObject.SetActive(true);
                    if (i < answerTexts.Length && answerTexts[i] != null)
                        answerTexts[i].text = $"{i + 1}. {mission.answerOptions[i]}";
                }
                else
                {
                    answerButtons[i].gameObject.SetActive(false);
                }
            }

            if (panel) panel.SetActive(true);
        }

        private void OnAnswerSelected(int index)
        {
            LastAnswerCorrect = index == correctIndex;
            float rt = Time.time - showTime;
            if (panel) panel.SetActive(false);
            onAnswered?.Invoke(index, rt);
        }
    }
}
