using System.Collections;
using System.Collections.Generic;
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
    /// 2단계 가이드 방식: WP01(B111) → WP00(B110) 순차 안내 후 자동 진행.
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

        // === Guided relocalization ===
        private enum GuidedStep { WaitingWP01, WaitingWP00, AllDone, Fallback }
        private GuidedStep currentGuidedStep;
        private static readonly string[] GuidedWPIds = { "B_WP01", "B_WP00" };
        private static readonly string[] GuidedRoomNames = { "B111", "B110" };
        private Dictionary<string, AnchorRelocState> guidedCache = new();
        private Coroutine guidedCoroutine;

        // === Image Tracking mode ===
        private bool imageTrackingMode;

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

            var aligner = ImageTrackingAligner.Instance;
            if (aligner != null)
            {
                aligner.OnMarkerDetected -= OnImageMarkerDetected;
                aligner.OnAlignmentComplete -= OnImageAlignmentComplete;
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
            imageTrackingMode = ExperimentManager.Instance != null
                && ExperimentManager.Instance.UseImageTracking;

            // 가이드 상태 리셋
            currentGuidedStep = GuidedStep.WaitingWP01;
            guidedCache.Clear();
            StopGuidedCoroutine();

            if (relocalizationPanel != null)
                relocalizationPanel.SetActive(true);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (successBar != null)
                successBar.fillAmount = 0f;

            if (statusDetailText != null)
                statusDetailText.text = "";

            if (successCountText != null)
                successCountText.text = "";

            if (failedCountText != null)
                failedCountText.text = "";

            if (imageTrackingMode)
            {
                // Image Tracking 모드: 마커 스캔 안내
                if (instructionText != null)
                    instructionText.text = LocalizationManager.Get("reloc.image_scan");

                if (progressText != null)
                    progressText.text = string.Format(LocalizationManager.Get("reloc.image_progress"), 0,
                        ImageTrackingAligner.Instance?.TotalMarkerCount ?? 0);

                if (progressBar != null)
                    progressBar.fillAmount = 0f;

                // ImageTrackingAligner 이벤트 구독
                var aligner = ImageTrackingAligner.Instance;
                if (aligner != null)
                {
                    aligner.OnMarkerDetected -= OnImageMarkerDetected;
                    aligner.OnMarkerDetected += OnImageMarkerDetected;
                    aligner.OnAlignmentComplete -= OnImageAlignmentComplete;
                    aligner.OnAlignmentComplete += OnImageAlignmentComplete;
                }

                Debug.Log("[RelocalizationUI] Image Tracking mode — waiting for markers");
            }
            else
            {
                // Spatial Anchor 모드: 기존 가이드
                if (instructionText != null)
                    instructionText.text = string.Format(
                        LocalizationManager.Get("reloc.guide_face"), GuidedRoomNames[0]);

                if (progressText != null)
                    progressText.text = string.Format(
                        LocalizationManager.Get("reloc.guide_step"), 1, 2);

                if (progressBar != null)
                    progressBar.fillAmount = 0f;

                // 앵커 로드 시작 (9개 병렬)
                var anchorMgr = SpatialAnchorManager.Instance;
                if (routeId != null)
                    anchorMgr?.LoadRouteAnchors(routeId);
                else
                    anchorMgr?.LoadAllAnchors();
            }
        }

        // === Image Tracking 이벤트 핸들러 ===

        private void OnImageMarkerDetected(string markerName, int detectedCount)
        {
            int total = ImageTrackingAligner.Instance?.TotalMarkerCount ?? 0;

            if (instructionText != null)
                instructionText.text = string.Format(
                    LocalizationManager.Get("reloc.image_marker_found"), markerName);

            if (progressText != null)
                progressText.text = string.Format(
                    LocalizationManager.Get("reloc.image_progress"), detectedCount, total);

            if (progressBar != null && total > 0)
                progressBar.fillAmount = (float)detectedCount / total;

            if (successBar != null && total > 0)
                successBar.fillAmount = (float)detectedCount / total;

            if (successCountText != null)
                successCountText.text = string.Format(
                    LocalizationManager.Get("reloc.success_count"), detectedCount, total);
        }

        private void OnImageAlignmentComplete(float successRate)
        {
            Debug.Log($"[RelocalizationUI] Image alignment complete — rate={successRate:F2}");

            if (instructionText != null)
                instructionText.text = LocalizationManager.Get("reloc.image_aligned");

            if (progressBar != null)
                progressBar.fillAmount = 1f;

            // 2초 후 자동 진행
            StopAutoProceed();
            autoProceedCoroutine = StartCoroutine(AutoProceedCountdown(2f));
        }

        /// <summary>호환용 진행 이벤트</summary>
        private void OnProgress(int relocalized, int total)
        {
            // 가이드 모드에서는 progressBar를 가이드 단계로 제어하므로 무시
        }

        /// <summary>상세 진행 이벤트 — 가이드 + 백그라운드 분리 처리</summary>
        private void OnDetailedProgress(string wpId, AnchorRelocState state,
            int successCount, int timedOutCount, int total)
        {
            // 성공 바 업데이트 (전체 앵커 기준)
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

            // 가이드 앵커 처리
            bool isGuidedAnchor = wpId == GuidedWPIds[0] || wpId == GuidedWPIds[1];
            if (isGuidedAnchor && state != AnchorRelocState.Pending)
            {
                guidedCache[wpId] = state;
                ProcessGuidedStep(wpId, state);
            }
            else
            {
                // 비가이드 앵커: 상태 디테일만 표시
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
        }

        private void ProcessGuidedStep(string wpId, AnchorRelocState state)
        {
            switch (currentGuidedStep)
            {
                case GuidedStep.WaitingWP01:
                    if (wpId == GuidedWPIds[0]) // WP01
                    {
                        if (state == AnchorRelocState.Tracking)
                        {
                            ShowGuidedMessage(
                                string.Format(LocalizationManager.Get("reloc.guide_recognized"), GuidedRoomNames[0]),
                                1f, () => TransitionToStep2());
                        }
                        else if (state == AnchorRelocState.TimedOut || state == AnchorRelocState.LoadFailed)
                        {
                            ShowGuidedMessage(
                                string.Format(LocalizationManager.Get("reloc.guide_timeout"), GuidedRoomNames[0]),
                                1.5f, () => TransitionToStep2());
                        }
                    }
                    // WP00이 먼저 도착하면 캐시만 (TransitionToStep2에서 체크)
                    break;

                case GuidedStep.WaitingWP00:
                    if (wpId == GuidedWPIds[1]) // WP00
                    {
                        if (state == AnchorRelocState.Tracking)
                        {
                            ShowGuidedMessage(
                                string.Format(LocalizationManager.Get("reloc.guide_recognized"), GuidedRoomNames[1]),
                                1f, () => GuidedComplete());
                        }
                        else if (state == AnchorRelocState.TimedOut || state == AnchorRelocState.LoadFailed)
                        {
                            ShowGuidedMessage(
                                string.Format(LocalizationManager.Get("reloc.guide_timeout"), GuidedRoomNames[1]),
                                1.5f, () => GuidedComplete());
                        }
                    }
                    break;
            }
        }

        private void TransitionToStep2()
        {
            currentGuidedStep = GuidedStep.WaitingWP00;

            if (instructionText != null)
                instructionText.text = string.Format(
                    LocalizationManager.Get("reloc.guide_face"), GuidedRoomNames[1]);

            if (progressText != null)
                progressText.text = string.Format(
                    LocalizationManager.Get("reloc.guide_step"), 2, 2);

            if (progressBar != null)
                progressBar.fillAmount = 0.5f;

            // 캐시 체크 — WP00이 이미 도착했으면 즉시 처리
            if (guidedCache.TryGetValue(GuidedWPIds[1], out var cachedState))
            {
                ProcessGuidedStep(GuidedWPIds[1], cachedState);
            }
        }

        /// <summary>가이드를 빠르게 완주합니다. 캐시된 결과를 빠르게 표시 후 GuidedComplete.</summary>
        private IEnumerator ForceCompleteGuide()
        {
            // WP01 단계에서 강제 완료된 경우
            if (currentGuidedStep == GuidedStep.WaitingWP01)
            {
                string wp01Msg = guidedCache.ContainsKey(GuidedWPIds[0])
                    ? string.Format(LocalizationManager.Get(
                        guidedCache[GuidedWPIds[0]] == AnchorRelocState.Tracking
                            ? "reloc.guide_recognized" : "reloc.guide_timeout"), GuidedRoomNames[0])
                    : string.Format(LocalizationManager.Get("reloc.guide_timeout"), GuidedRoomNames[0]);
                if (instructionText != null) instructionText.text = wp01Msg;
                yield return new WaitForSeconds(0.5f);

                // WP00 가이드 표시
                currentGuidedStep = GuidedStep.WaitingWP00;
                if (instructionText != null)
                    instructionText.text = string.Format(
                        LocalizationManager.Get("reloc.guide_face"), GuidedRoomNames[1]);
                if (progressText != null)
                    progressText.text = string.Format(
                        LocalizationManager.Get("reloc.guide_step"), 2, 2);
                if (progressBar != null) progressBar.fillAmount = 0.5f;
                yield return new WaitForSeconds(0.3f);
            }

            // WP00 결과 표시
            string wp00Msg = guidedCache.ContainsKey(GuidedWPIds[1])
                ? string.Format(LocalizationManager.Get(
                    guidedCache[GuidedWPIds[1]] == AnchorRelocState.Tracking
                        ? "reloc.guide_recognized" : "reloc.guide_timeout"), GuidedRoomNames[1])
                : string.Format(LocalizationManager.Get("reloc.guide_timeout"), GuidedRoomNames[1]);
            if (instructionText != null) instructionText.text = wp00Msg;
            yield return new WaitForSeconds(0.5f);

            guidedCoroutine = null;
            GuidedComplete();
        }

        private void GuidedComplete()
        {
            currentGuidedStep = GuidedStep.AllDone;

            if (instructionText != null)
                instructionText.text = LocalizationManager.Get("reloc.guide_done");

            if (progressText != null)
                progressText.text = "";

            if (progressBar != null)
                progressBar.fillAmount = 1f;

            // 2초 후 자동 진행
            StopGuidedCoroutine();
            guidedCoroutine = StartCoroutine(GuidedAutoProceed(2f));
        }

        private IEnumerator GuidedAutoProceed(float delay)
        {
            yield return new WaitForSeconds(delay);
            guidedCoroutine = null;
            Debug.Log("[RelocalizationUI] Guided auto-proceed — calling OnProceedClicked");
            OnProceedClicked();
        }

        private void ShowGuidedMessage(string message, float duration, System.Action onComplete)
        {
            StopGuidedCoroutine();
            if (instructionText != null)
                instructionText.text = message;
            guidedCoroutine = StartCoroutine(GuidedMessageDelay(duration, onComplete));
        }

        private IEnumerator GuidedMessageDelay(float duration, System.Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            guidedCoroutine = null;
            onComplete?.Invoke();
        }

        private void StopGuidedCoroutine()
        {
            if (guidedCoroutine != null)
            {
                StopCoroutine(guidedCoroutine);
                guidedCoroutine = null;
            }
        }

        /// <summary>호환용 완료 이벤트 (새 이벤트가 없는 구버전 호환)</summary>
        private void OnComplete()
        {
            // OnCompleteWithRate가 호출되면 거기서 처리하므로 여기는 fallback
        }

        /// <summary>전체 재인식 완료 — 가이드 완료 시 결과 패널 건너뜀</summary>
        private void OnCompleteWithRate(float successRate)
        {
            var mgr = SpatialAnchorManager.Instance;

            Debug.Log($"[RelocalizationUI] OnCompleteWithRate — rate={successRate:F4}, " +
                      $"success={mgr?.SuccessfulAnchorCount}, timedOut={mgr?.TimedOutAnchorCount}, " +
                      $"failed={mgr?.FailedAnchorCount}, total={mgr?.TotalAnchorCount}, " +
                      $"guidedStep={currentGuidedStep}");

            // 가이드가 이미 완료되었거나 곧 완료될 예정이면 결과 패널 표시 안 함
            if (currentGuidedStep == GuidedStep.AllDone)
            {
                Debug.Log("[RelocalizationUI] Guided already done — skipping result panel");
                return;
            }

            // 가이드가 아직 진행 중이면: 캐시된 결과로 빠르게 완주
            if (currentGuidedStep == GuidedStep.WaitingWP01 || currentGuidedStep == GuidedStep.WaitingWP00)
            {
                Debug.Log($"[RelocalizationUI] OnCompleteWithRate arrived during guided step={currentGuidedStep} — fast-completing");
                StopGuidedCoroutine();
                guidedCoroutine = StartCoroutine(ForceCompleteGuide());
                return;
            }

            // Fallback — 가이드가 비활성인 경우 기존 결과 패널 표시
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
                string baseText = LocalizationManager.Get("reloc.nav_ready");

                // 공간 정합성 경고만 예외적으로 표시 (실제 문제이므로 유지)
                if (!spatialOk)
                    baseText += "\n\n<color=#FF4444>" + string.Format(LocalizationManager.Get("reloc.spatial_warning"), spatialWarning) + "</color>";

                warningText.text = baseText;
            }

            // 공간 정합 통과 시 항상 3초 auto-proceed (fallback이 작동하므로 성공률 무관)
            StopAutoProceed();
            if (spatialOk)
            {
                autoProceedCoroutine = StartCoroutine(AutoProceedCountdown(3f));
            }
            // 공간 정합성 실패 → 자동 Proceed 없음, 실험자 판단 필수
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
            StopGuidedCoroutine();
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
            StopGuidedCoroutine();
            Debug.Log("[RelocalizationUI] OnRetryClicked invoked");
            DomainEventBus.Instance?.Publish(new RelocalizationCompleted(0f, "retry"));

            // 가이드 리셋
            currentGuidedStep = GuidedStep.WaitingWP01;
            guidedCache.Clear();

            if (resultPanel != null)
                resultPanel.SetActive(false);

            if (instructionText != null)
                instructionText.text = string.Format(
                    LocalizationManager.Get("reloc.guide_face"), GuidedRoomNames[0]);

            if (progressText != null)
                progressText.text = string.Format(
                    LocalizationManager.Get("reloc.guide_step"), 1, 2);

            if (progressBar != null)
                progressBar.fillAmount = 0f;

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
    }
}
