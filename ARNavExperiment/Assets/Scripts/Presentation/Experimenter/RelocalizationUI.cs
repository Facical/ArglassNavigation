using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.Experimenter
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

        private Coroutine autoProceedCoroutine;
        private bool firstAnchorTracked;

        private void Start()
        {
            // A. 버튼 참조가 null이면 자식에서 이름으로 자동 탐색
            if (proceedButton == null)
            {
                proceedButton = FindButtonInChildren("ProceedBtn");
                if (proceedButton != null)
                    Debug.Log("[RelocalizationUI] proceedButton auto-found via FindButtonInChildren");
            }
            if (retryButton == null)
            {
                retryButton = FindButtonInChildren("RetryBtn");
                if (retryButton != null)
                    Debug.Log("[RelocalizationUI] retryButton auto-found via FindButtonInChildren");
            }

            if (proceedButton != null)
                proceedButton.onClick.AddListener(OnProceedClicked);
            else
                Debug.LogError("[RelocalizationUI] proceedButton is NULL — Proceed will only work via keyboard (P key)");

            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryClicked);
            else
                Debug.LogError("[RelocalizationUI] retryButton is NULL — Retry will only work via keyboard (R key)");
        }

        private void Update()
        {
            // B. 키보드 fallback — ResultPanel 활성 시 P/R 키로 조작 가능
            if (resultPanel != null && resultPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    Debug.Log("[RelocalizationUI] Proceed triggered via keyboard (P)");
                    OnProceedClicked();
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    Debug.Log("[RelocalizationUI] Retry triggered via keyboard (R)");
                    OnRetryClicked();
                }
            }
        }

        private Button FindButtonInChildren(string name)
        {
            if (resultPanel == null) return null;
            var buttons = resultPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name == name)
                    return btn;
            }
            // resultPanel에 없으면 전체 자식에서 탐색
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn.gameObject.name == name)
                    return btn;
            }
            return null;
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

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;
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

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }

        private void RefreshLocalization(Language lang)
        {
            // 재인식 UI는 진행 중 상태에 따라 동적으로 갱신되므로 별도 전체 갱신 불필요
        }

        /// <summary>
        /// 재인식 프로세스를 시작합니다 (전체 앵커).
        /// </summary>
        public void StartRelocalization()
        {
            StartRelocalization(null);
        }

        /// <summary>
        /// 재인식 프로세스를 시작합니다.
        /// routeId가 null이 아니면 해당 경로 앵커만 로드합니다.
        /// </summary>
        public void StartRelocalization(string routeId)
        {
            firstAnchorTracked = false;

            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(true);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            string routeLabel = routeId != null ? $"Route {routeId}" : "All routes";
            if (instructionText != null)
                instructionText.text = string.Format(LocalizationManager.Get("reloc.scanning"), routeLabel);

            if (progressText != null)
                progressText.text = LocalizationManager.Get("reloc.preparing");

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
            if (routeId != null)
                anchorMgr?.LoadRouteAnchors(routeId);
            else
                anchorMgr?.LoadAllAnchors();
        }

        /// <summary>호환용 진행 이벤트</summary>
        private void OnProgress(int relocalized, int total)
        {
            if (progressText != null)
                progressText.text = $"Progress: {relocalized} / {total}";

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
                successCountText.text = string.Format(LocalizationManager.Get("reloc.success_count"), successCount, total);
            if (failedCountText != null)
                failedCountText.text = failCount > 0
                    ? string.Format(LocalizationManager.Get("reloc.failed_count"), failCount, total)
                    : "";

            // 첫 앵커 추적 성공 시 좌표계 확보 안내
            if (!firstAnchorTracked && state == AnchorRelocState.Tracking)
            {
                firstAnchorTracked = true;
                if (instructionText != null)
                    instructionText.text = LocalizationManager.Get("reloc.coordinate_established");
            }

            // 상태 디테일
            if (statusDetailText != null)
            {
                string stateStr = state switch
                {
                    AnchorRelocState.Tracking => LocalizationManager.Get("reloc.tracked"),
                    AnchorRelocState.TimedOut => LocalizationManager.Get("reloc.timed_out"),
                    AnchorRelocState.LoadFailed => LocalizationManager.Get("reloc.load_failed"),
                    _ => LocalizationManager.Get("reloc.processing")
                };
                statusDetailText.text = $"{wpId}: {stateStr}";
            }
        }

        /// <summary>호환용 완료 이벤트 (새 이벤트가 없는 구버전 호환)</summary>
        private void OnComplete()
        {
            // OnCompleteWithRate가 호출되면 거기서 처리하므로 여기는 fallback
        }

        /// <summary>전체 재인식 완료 — 항상 결과 패널 표시, 실험자 확인 필수</summary>
        private void OnCompleteWithRate(float successRate)
        {
            var mgr = SpatialAnchorManager.Instance;

            Debug.Log($"[RelocalizationUI] OnCompleteWithRate — rate={successRate:F4}, " +
                      $"success={mgr?.SuccessfulAnchorCount}, timedOut={mgr?.TimedOutAnchorCount}, " +
                      $"failed={mgr?.FailedAnchorCount}, total={mgr?.TotalAnchorCount}");

            if (successRate >= 1f)
            {
                if (instructionText != null)
                    instructionText.text = LocalizationManager.Get("reloc.complete");
                if (progressText != null)
                {
                    progressText.text = string.Format(
                        LocalizationManager.Get("reloc.all_recognized_detail"),
                        mgr?.SuccessfulAnchorCount ?? 0,
                        mgr?.TotalAnchorCount ?? 0);
                }
                if (progressBar != null)
                    progressBar.fillAmount = 1f;
            }
            else
            {
                if (instructionText != null)
                    instructionText.text = LocalizationManager.Get("reloc.partial_fail");
                if (progressText != null)
                {
                    progressText.text = string.Format(LocalizationManager.Get("reloc.result_detail"),
                        mgr.SuccessfulAnchorCount, mgr.TimedOutAnchorCount, mgr.FailedAnchorCount);
                }
            }

            // 항상 결과 패널 표시 — 공간 정합성 검증 포함
            ShowResultPanel(successRate);
        }

        private void ShowResultPanel(float successRate)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);

            // D. 버튼 interactable + raycastTarget 명시적 보장
            EnsureButtonInteractable(proceedButton);
            EnsureButtonInteractable(retryButton);

            var mgr = SpatialAnchorManager.Instance;

            // 공간 정합성 검증
            bool spatialOk = true;
            string spatialWarning = "";
            if (mgr != null)
                spatialOk = mgr.VerifySpatialCoherence(out spatialWarning);

            if (warningText != null)
            {
                var fallbacks = mgr?.FallbackWaypoints ?? new System.Collections.Generic.List<string>();
                string wpList = fallbacks.Count > 0 ? string.Join(", ", fallbacks) : "None";

                string baseText = string.Format(LocalizationManager.Get("reloc.warning"), $"{successRate:P0}", wpList);

                // 앵커 위치 요약
                string summary = mgr != null ? mgr.GetAnchorPositionSummary() : "";
                if (!string.IsNullOrEmpty(summary))
                    baseText += "\n\n" + string.Format(LocalizationManager.Get("reloc.result_summary"), summary);

                // 공간 정합성 경고
                if (!spatialOk)
                    baseText += "\n\n<color=#FF4444>" + string.Format(LocalizationManager.Get("reloc.spatial_warning"), spatialWarning) + "</color>";

                warningText.text = baseText;
            }

            // Auto-proceed: 100% + 공간 정합 통과 시에만 30초 카운트다운
            StopAutoProceed();
            if (successRate >= 1f && spatialOk)
            {
                autoProceedCoroutine = StartCoroutine(AutoProceedCountdown(30f));
            }
            // 부분 실패 또는 공간 불일치 → 자동 Proceed 없음, 실험자 판단 필수
        }

        private void EnsureButtonInteractable(Button button)
        {
            if (button == null) return;
            button.interactable = true;
            var img = button.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        private void OnProceedClicked()
        {
            StopAutoProceed();
            Debug.Log("[RelocalizationUI] OnProceedClicked invoked");
            var mgr = SpatialAnchorManager.Instance;
            float rate = mgr != null ? mgr.RelocalizationSuccessRate : 0f;
            string fallbacks = mgr != null ? string.Join(",", mgr.FallbackWaypoints) : "";

            DomainEventBus.Instance?.Publish(new RelocalizationCompleted(rate, "proceed_partial"));

            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(false);

            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnRetryClicked()
        {
            StopAutoProceed();
            Debug.Log("[RelocalizationUI] OnRetryClicked invoked");
            DomainEventBus.Instance?.Publish(new RelocalizationCompleted(0f, "retry"));

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (instructionText != null)
                instructionText.text = LocalizationManager.Get("reloc.retrying");

            if (progressText != null)
                progressText.text = LocalizationManager.Get("reloc.retrying_short");

            if (statusDetailText != null)
                statusDetailText.text = "";

            SpatialAnchorManager.Instance?.RetryFailedAnchors();
        }

        private void StopAutoProceed()
        {
            if (autoProceedCoroutine != null)
            {
                StopCoroutine(autoProceedCoroutine);
                autoProceedCoroutine = null;
            }
        }

        private IEnumerator AutoProceedCountdown(float seconds)
        {
            float remaining = seconds;
            string originalWarning = warningText != null ? warningText.text : "";
            while (remaining > 0f)
            {
                if (warningText != null)
                    warningText.text = originalWarning + string.Format(LocalizationManager.Get("reloc.auto_proceed"), $"{remaining:F0}");
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }
            autoProceedCoroutine = null;
            Debug.Log("[RelocalizationUI] Auto-proceed timer expired — proceeding automatically");
            OnProceedClicked();
        }

        // DelayedProceedToSetup 제거 — 항상 결과 패널에서 실험자 확인 필수
    }
}
