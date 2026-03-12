using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
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

        private void Awake()
        {
            if (panel == null)
            {
                panel = gameObject;
                Debug.LogWarning("[DifficultyRatingUI] panel null — self-wired to gameObject");
            }
            if (promptText == null || currentRatingText == null)
            {
                var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name == "PromptText" && promptText == null) promptText = t;
                    if (t.gameObject.name == "CurrentRatingText" && currentRatingText == null) currentRatingText = t;
                }
            }
            if (ratingButtons == null || ratingButtons.Length == 0)
            {
                var ratingBar = panel.transform.Find("RatingButtons");
                if (ratingBar != null)
                    ratingButtons = ratingBar.GetComponentsInChildren<Button>(true);
            }
            if (confirmButton == null)
            {
                var allBtns = panel.GetComponentsInChildren<Button>(true);
                foreach (var btn in allBtns)
                {
                    if (btn.gameObject.name == "ConfirmRatingBtn") { confirmButton = btn; break; }
                }
            }
            // 버튼 리스너 (Start에서 이동 — 비활성 오브젝트의 Start()는 Show() 후 다음 프레임에 실행되어 패널을 다시 숨김)
            if (ratingButtons != null)
            {
                for (int i = 0; i < ratingButtons.Length; i++)
                {
                    int rating = i + 1;
                    ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
                }
            }
            confirmButton?.onClick.AddListener(Confirm);

            Debug.LogWarning($"[DifficultyRatingUI] Awake — panel={panel != null}, promptText={promptText != null}, " +
                $"ratingButtons={ratingButtons?.Length ?? 0}, confirmButton={confirmButton != null}");
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
            }
        }

        public void Hide()
        {
            if (panel) panel.SetActive(false);
            onRated = null;
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
            if (panel) panel.SetActive(false);
            cb?.Invoke(selectedRating);
        }
    }
}
