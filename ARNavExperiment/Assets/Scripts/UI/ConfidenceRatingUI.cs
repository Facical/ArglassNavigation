using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;

namespace ARNavExperiment.UI
{
    public class ConfidenceRatingUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Button[] ratingButtons; // 7 buttons for 1-7
        [SerializeField] private TextMeshProUGUI currentRatingText;
        [SerializeField] private Button confirmButton;

        private int selectedRating = -1;
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

        public void Show(string prompt, System.Action<int> callback)
        {
            onRated = callback;
            selectedRating = -1;
            if (promptText) promptText.text = prompt;
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
            if (panel) panel.SetActive(false);
            onRated?.Invoke(selectedRating);
        }
    }
}
