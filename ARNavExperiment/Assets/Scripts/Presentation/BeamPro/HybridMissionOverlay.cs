using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.BeamPro
{
    /// <summary>
    /// Hybrid 모드에서 BeamProCanvas(ScreenSpaceOverlay)에 미션 상호작용 UI를 표시합니다.
    /// Hybrid 모드에서는 핸드트래킹이 비활성화되어 글래스 WorldSpace UI 터치가 불가하므로,
    /// BeamPro 폰 터치스크린에 Briefing/Verification/Rating UI를 표시합니다.
    /// </summary>
    public class HybridMissionOverlay : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject overlayPanel;

        [Header("Briefing")]
        [SerializeField] private GameObject briefingContent;
        [SerializeField] private TextMeshProUGUI missionIdText;
        [SerializeField] private TextMeshProUGUI briefingText;
        [SerializeField] private Button confirmButton;

        [Header("Verification")]
        [SerializeField] private GameObject verificationContent;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button[] answerButtons;

        [Header("Rating (Confidence / Difficulty shared)")]
        [SerializeField] private GameObject ratingContent;
        [SerializeField] private TextMeshProUGUI ratingTitleText;
        [SerializeField] private TextMeshProUGUI ratingPromptText;
        [SerializeField] private Button[] ratingButtons; // 7 buttons for 1-7
        [SerializeField] private TextMeshProUGUI currentRatingText;
        [SerializeField] private Button confirmRatingButton;

        [SerializeField] private float autoAdvanceTimeout = 15f;

        public bool LastAnswerCorrect { get; private set; }

        private System.Action onBriefingConfirm;
        private System.Action<int, float> onVerificationAnswered;
        private System.Action<int> onRatingConfirm;
        private int correctAnswerIndex;
        private float verificationShowTime;
        private int selectedRating = -1;
        private Coroutine autoAdvanceCoroutine;
        private string originalBriefingText;

        private GameObject tabBar;
        private GameObject hubRoot;

        private void Awake()
        {
            if (overlayPanel == null)
                overlayPanel = gameObject;

            // 버튼 리스너 등록
            confirmButton?.onClick.AddListener(OnBriefingConfirm);
            confirmRatingButton?.onClick.AddListener(OnRatingConfirm);

            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    int idx = i;
                    answerButtons[i]?.onClick.AddListener(() => OnAnswerSelected(idx));
                }
            }

            if (ratingButtons != null)
            {
                for (int i = 0; i < ratingButtons.Length; i++)
                {
                    int rating = i + 1;
                    ratingButtons[i]?.onClick.AddListener(() => SelectRating(rating));
                }
            }

            // NOTE: overlayPanel.SetActive(false) 제거!
            // SceneSetupTool이 비활성 상태로 생성하므로 불필요하며,
            // ShowOverlay()에서 SetActive(true) 직후 최초 Awake가 호출될 때
            // 재비활성화되어 오버레이가 영원히 표시되지 않는 버그 발생.
        }

        private void CacheHubReferences()
        {
            if (hubRoot != null) return;
            var hub = BeamProHubController.Instance;
            if (hub == null) return;
            hubRoot = hub.gameObject;
            tabBar = hubRoot.transform.Find("TabBar")?.gameObject;
        }

        // ===== Briefing =====

        public void ShowBriefing(MissionData mission, System.Action onConfirm)
        {
            CacheHubReferences();
            onBriefingConfirm = onConfirm;

            if (missionIdText)
                missionIdText.text = string.Format(LocalizationManager.Get("briefing.mission"), mission.missionId);
            if (briefingText)
                briefingText.text = mission.GetBriefingText();

            ShowOverlay();
            SetContentActive(briefingContent);

            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterTimeout());

            Debug.Log($"[HybridMissionOverlay] ShowBriefing({mission.missionId})");
        }

        private IEnumerator AutoAdvanceAfterTimeout()
        {
            originalBriefingText = briefingText ? briefingText.text : "";
            float remaining = autoAdvanceTimeout;
            while (remaining > 0f)
            {
                if (briefingText)
                {
                    string autoText = string.Format(
                        LocalizationManager.Get("briefing.auto_advance"), Mathf.CeilToInt(remaining));
                    briefingText.text = $"{originalBriefingText}\n\n<color=#FFCC00><size=80%>{autoText}</size></color>";
                }
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }
            autoAdvanceCoroutine = null;
            OnBriefingConfirm();
        }

        private void StopAutoAdvance()
        {
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }

        private void OnBriefingConfirm()
        {
            StopAutoAdvance();
            var cb = onBriefingConfirm;
            onBriefingConfirm = null;
            Hide();
            cb?.Invoke();
        }

        // ===== Verification =====

        public void ShowVerification(MissionData mission, System.Action<int, float> onAnswered)
        {
            CacheHubReferences();
            onVerificationAnswered = onAnswered;
            correctAnswerIndex = mission.correctAnswerIndex;
            verificationShowTime = Time.time;
            LastAnswerCorrect = false;

            if (questionText)
                questionText.text = mission.GetVerificationQuestion();

            if (answerButtons != null)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    if (i < mission.answerOptions.Length)
                    {
                        answerButtons[i].gameObject.SetActive(true);
                        var txt = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null)
                            txt.text = $"{i + 1}. {mission.GetAnswerOption(i)}";
                    }
                    else
                    {
                        answerButtons[i].gameObject.SetActive(false);
                    }
                }
            }

            ShowOverlay();
            SetContentActive(verificationContent);

            Debug.Log($"[HybridMissionOverlay] ShowVerification({mission.missionId})");
        }

        private void OnAnswerSelected(int index)
        {
            LastAnswerCorrect = index == correctAnswerIndex;
            float rt = Time.time - verificationShowTime;
            var cb = onVerificationAnswered;
            onVerificationAnswered = null;
            Hide();
            cb?.Invoke(index, rt);
        }

        // ===== Confidence Rating =====

        public void ShowConfidenceRating(string prompt, System.Action<int> onRated)
        {
            CacheHubReferences();
            onRatingConfirm = onRated;
            selectedRating = -1;

            if (ratingTitleText)
                ratingTitleText.text = LocalizationManager.Get("hybrid.confidence_title");
            if (ratingPromptText)
                ratingPromptText.text = prompt;
            if (currentRatingText)
                currentRatingText.text = "";

            ShowOverlay();
            SetContentActive(ratingContent);

            Debug.Log("[HybridMissionOverlay] ShowConfidenceRating");
        }

        // ===== Difficulty Rating =====

        public void ShowDifficultyRating(string missionId, System.Action<int> onRated)
        {
            CacheHubReferences();
            onRatingConfirm = onRated;
            selectedRating = -1;

            if (ratingTitleText)
                ratingTitleText.text = LocalizationManager.Get("hybrid.difficulty_title");
            if (ratingPromptText)
                ratingPromptText.text = LocalizationManager.Get("difficulty.prompt");
            if (currentRatingText)
                currentRatingText.text = "";

            ShowOverlay();
            SetContentActive(ratingContent);

            Debug.Log($"[HybridMissionOverlay] ShowDifficultyRating({missionId})");
        }

        private void SelectRating(int rating)
        {
            selectedRating = rating;
            if (currentRatingText)
                currentRatingText.text = $"{rating}/7";
        }

        private void OnRatingConfirm()
        {
            if (selectedRating < 1) return;
            var cb = onRatingConfirm;
            onRatingConfirm = null;
            Hide();
            cb?.Invoke(selectedRating);
        }

        // ===== Show / Hide =====

        private void ShowOverlay()
        {
            // 허브 탭/탭바 숨기기
            BeamProHubController.Instance?.HideAllTabs();
            if (tabBar) tabBar.SetActive(false);

            overlayPanel.SetActive(true);
        }

        private void SetContentActive(GameObject content)
        {
            if (briefingContent) briefingContent.SetActive(briefingContent == content);
            if (verificationContent) verificationContent.SetActive(verificationContent == content);
            if (ratingContent) ratingContent.SetActive(ratingContent == content);
        }

        public void Hide()
        {
            StopAutoAdvance();
            onBriefingConfirm = null;
            onVerificationAnswered = null;
            onRatingConfirm = null;

            overlayPanel.SetActive(false);

            // 허브 탭바 + 맵 탭 복원
            if (tabBar) tabBar.SetActive(true);
            BeamProHubController.Instance?.SwitchTab(0);

            Debug.Log("[HybridMissionOverlay] Hide — hub restored");
        }
    }
}
