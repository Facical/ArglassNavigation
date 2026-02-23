using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Logging;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 실험 시작 시 앵커 재인식 UI.
    /// 성공/실패를 정확히 표시하고, 부분 실패 시 실험자가 판단할 수 있게 함.
    /// </summary>
    public class RelocalizationUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject relocalizationPanel;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressBar;
        [SerializeField] private TextMeshProUGUI statusDetailText;

        [Header("Result Display")]
        [SerializeField] private TextMeshProUGUI successCountText;
        [SerializeField] private TextMeshProUGUI failedCountText;
        [SerializeField] private Image successBar;

        [Header("Experimenter Controls")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button proceedButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private TextMeshProUGUI warningText;

        private void Start()
        {
            if (proceedButton != null)
                proceedButton.onClick.AddListener(OnProceedClicked);
            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void OnEnable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
            {
                anchorMgr.OnRelocalizationProgress -= OnProgress;
                anchorMgr.OnRelocalizationComplete -= OnComplete;
                anchorMgr.OnRelocalizationDetailedProgress -= OnDetailedProgress;
                anchorMgr.OnRelocalizationCompleteWithRate -= OnCompleteWithRate;

                anchorMgr.OnRelocalizationProgress += OnProgress;
                anchorMgr.OnRelocalizationComplete += OnComplete;
                anchorMgr.OnRelocalizationDetailedProgress += OnDetailedProgress;
                anchorMgr.OnRelocalizationCompleteWithRate += OnCompleteWithRate;
            }
        }

        private void OnDisable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
            {
                anchorMgr.OnRelocalizationProgress -= OnProgress;
                anchorMgr.OnRelocalizationComplete -= OnComplete;
                anchorMgr.OnRelocalizationDetailedProgress -= OnDetailedProgress;
                anchorMgr.OnRelocalizationCompleteWithRate -= OnCompleteWithRate;
            }
        }

        /// <summary>
        /// 재인식 프로세스를 시작합니다.
        /// </summary>
        public void StartRelocalization()
        {
            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(true);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (instructionText != null)
                instructionText.text = "환경을 인식하고 있습니다...\n주변을 천천히 둘러봐주세요.";

            if (progressText != null)
                progressText.text = "준비 중...";

            if (progressBar != null)
                progressBar.fillAmount = 0f;

            if (successBar != null)
                successBar.fillAmount = 0f;

            if (statusDetailText != null)
                statusDetailText.text = "";

            if (successCountText != null)
                successCountText.text = "";

            if (failedCountText != null)
                failedCountText.text = "";

            // 앵커 로드 시작
            var anchorMgr = SpatialAnchorManager.Instance;
            anchorMgr?.LoadAllAnchors();
        }

        /// <summary>호환용 진행 이벤트</summary>
        private void OnProgress(int relocalized, int total)
        {
            if (progressText != null)
                progressText.text = $"처리 진행: {relocalized} / {total}";

            if (progressBar != null)
                progressBar.fillAmount = total > 0 ? (float)relocalized / total : 0f;
        }

        /// <summary>상세 진행 이벤트 — 성공/실패 분리 표시</summary>
        private void OnDetailedProgress(string wpId, AnchorRelocState state,
            int successCount, int timedOutCount, int total)
        {
            // 성공 바
            if (successBar != null)
                successBar.fillAmount = total > 0 ? (float)successCount / total : 0f;

            // 성공/실패 카운트
            int failCount = timedOutCount + (SpatialAnchorManager.Instance?.FailedAnchorCount ?? 0);
            if (successCountText != null)
                successCountText.text = $"성공: {successCount}/{total}";
            if (failedCountText != null)
                failedCountText.text = failCount > 0 ? $"실패: {failCount}/{total}" : "";

            // 상태 디테일
            if (statusDetailText != null)
            {
                string stateStr = state switch
                {
                    AnchorRelocState.Tracking => "인식 성공",
                    AnchorRelocState.TimedOut => "타임아웃",
                    AnchorRelocState.LoadFailed => "로드 실패",
                    _ => "처리 중"
                };
                statusDetailText.text = $"{wpId}: {stateStr}";
            }
        }

        /// <summary>호환용 완료 이벤트 (새 이벤트가 없는 구버전 호환)</summary>
        private void OnComplete()
        {
            // OnCompleteWithRate가 호출되면 거기서 처리하므로 여기는 fallback
        }

        /// <summary>전체 재인식 완료 — 성공률에 따라 분기</summary>
        private void OnCompleteWithRate(float successRate)
        {
            if (successRate >= 1f)
            {
                // 100% 성공 → 자동 전환
                if (instructionText != null)
                    instructionText.text = "환경 인식 완료!";

                if (progressText != null)
                    progressText.text = "모든 앵커가 인식되었습니다.";

                if (progressBar != null)
                    progressBar.fillAmount = 1f;

                StartCoroutine(DelayedProceedToSetup());
            }
            else
            {
                // 부분 실패 → 실험자 판단
                if (instructionText != null)
                    instructionText.text = "환경 인식 완료 (일부 실패)";

                if (progressText != null)
                {
                    var mgr = SpatialAnchorManager.Instance;
                    progressText.text = $"성공: {mgr.SuccessfulAnchorCount} / " +
                                        $"타임아웃: {mgr.TimedOutAnchorCount} / " +
                                        $"실패: {mgr.FailedAnchorCount}";
                }

                ShowResultPanel(successRate);
            }
        }

        private void ShowResultPanel(float successRate)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);

            if (warningText != null)
            {
                var mgr = SpatialAnchorManager.Instance;
                var fallbacks = mgr.FallbackWaypoints;
                string wpList = fallbacks.Count > 0 ? string.Join(", ", fallbacks) : "없음";
                warningText.text = $"성공률: {successRate:P0}\n" +
                                   $"Fallback 사용 웨이포인트: {wpList}\n\n" +
                                   $"계속 진행하면 실패한 앵커는 도면 추정치(fallback)를 사용합니다.\n" +
                                   $"재시도를 선택하면 실패한 앵커만 다시 로드합니다.";
            }
        }

        private void OnProceedClicked()
        {
            var mgr = SpatialAnchorManager.Instance;
            float rate = mgr != null ? mgr.RelocalizationSuccessRate : 0f;
            string fallbacks = mgr != null ? string.Join(",", mgr.FallbackWaypoints) : "";

            EventLogger.Instance?.LogEvent("RELOCALIZATION_PROCEED_PARTIAL",
                extraData: $"{{\"success_rate\":{rate:F2},\"fallback_waypoints\":\"{fallbacks}\"}}");

            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(false);

            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnRetryClicked()
        {
            EventLogger.Instance?.LogEvent("RELOCALIZATION_RETRY");

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (instructionText != null)
                instructionText.text = "실패한 앵커를 다시 인식하고 있습니다...\n주변을 천천히 둘러봐주세요.";

            if (progressText != null)
                progressText.text = "재시도 중...";

            if (statusDetailText != null)
                statusDetailText.text = "";

            SpatialAnchorManager.Instance?.RetryFailedAnchors();
        }

        private IEnumerator DelayedProceedToSetup()
        {
            yield return new WaitForSeconds(1.5f);

            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(false);

            ExperimentManager.Instance?.AdvanceState();
        }
    }
}
