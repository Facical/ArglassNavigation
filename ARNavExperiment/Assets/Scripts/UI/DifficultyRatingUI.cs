using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;

namespace ARNavExperiment.UI
{
    public class DifficultyRatingUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Button[] ratingButtons;
        [SerializeField] private TextMeshProUGUI currentRatingText;
        [SerializeField] private Button confirmButton;

        private int selectedRating = -1;
        private string missionId;
        private System.Action<int> onRated;

        private void Start()
        {
            for (int i = 0; i < ratingButtons.Length; i++)
            {
                int rating = i + 1;
                ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
            }
            confirmButton?.onClick.AddListener(Confirm);
            if (panel) panel.SetActive(false);
        }

        public void Show(string missionId, System.Action<int> callback)
        {
            this.missionId = missionId;
            onRated = callback;
            selectedRating = -1;
            if (promptText) promptText.text = "이 미션의 난이도를 평가해주세요";
            if (currentRatingText) currentRatingText.text = "";
            if (panel) panel.SetActive(true);
        }

        private void SelectRating(int rating)
        {
            selectedRating = rating;
            if (currentRatingText) currentRatingText.text = $"{rating}/7";
        }

        private void Confirm()
        {
            if (selectedRating < 1) return;

            EventLogger.Instance?.LogEvent("DIFFICULTY_RATED",
                difficultyRating: selectedRating,
                extraData: $"{{\"mission_id\":\"{missionId}\",\"rating\":{selectedRating}}}");

            if (panel) panel.SetActive(false);
            onRated?.Invoke(selectedRating);
        }
    }
}
