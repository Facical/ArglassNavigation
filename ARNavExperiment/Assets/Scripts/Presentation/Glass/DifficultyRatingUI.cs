using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.Glass
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
            if (promptText) promptText.text = LocalizationManager.Get("difficulty.prompt");
            if (currentRatingText) currentRatingText.text = "";
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
                fader.FadeOut(() => { onRated = null; });
            else
            {
                if (panel) panel.SetActive(false);
                onRated = null;
            }
        }

        private void SelectRating(int rating)
        {
            selectedRating = rating;
            if (currentRatingText) currentRatingText.text = $"{rating}/7";
        }

        private void Confirm()
        {
            if (selectedRating < 1) return;

            // DifficultyRated 이벤트는 MissionManager.OnDifficultyRated에서 발행

            var cb = onRated;
            onRated = null;
            var fader = panel?.GetComponent<PanelFader>();
            if (fader != null)
                fader.FadeOut(() => cb?.Invoke(selectedRating));
            else
            {
                if (panel) panel.SetActive(false);
                cb?.Invoke(selectedRating);
            }
        }
    }
}
