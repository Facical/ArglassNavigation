using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
namespace ARNavExperiment.Presentation.Glass
{
    /// <summary>
    /// GlassOnly 조건에서 ExperimentCanvas(글래스)에 표시되는 플로우 제어 UI.
    /// Relocalization → Setup → Running → Survey → Complete 패널을 순차 표시.
    /// 핸드트래킹 pinch로 버튼을 조작하여 실험을 진행.
    /// </summary>
    public class GlassFlowUI : MonoBehaviour
    {
        /// <summary>GlassFlowUI에 활성 패널이 있는지 여부. ExperimentHUD가 참조하여 HUD를 숨김.</summary>
        public static bool HasActivePanel { get; private set; }
        [Header("Reloc Panel")]
        [SerializeField] private GameObject relocPanel;
        [SerializeField] private TextMeshProUGUI relocProgressText;
        [SerializeField] private Image relocProgressBar;
        [SerializeField] private TextMeshProUGUI relocStatusText;
        [SerializeField] private Button relocProceedButton;

        [Header("Setup Panel")]
        [SerializeField] private GameObject setupPanel;
        [SerializeField] private TextMeshProUGUI setupTitleText;
        [SerializeField] private TextMeshProUGUI setupDetailText;
        [SerializeField] private Button setupContinueButton;

        [Header("Running Start Panel")]
        [SerializeField] private GameObject runningStartPanel;
        [SerializeField] private TextMeshProUGUI runningTitleText;
        [SerializeField] private TextMeshProUGUI runningDetailText;
        [SerializeField] private Button runningStartButton;

        [Header("Survey Panel (legacy — PostConditionSurveyController가 대체)")]
        [SerializeField] private GameObject surveyPanel;
        [SerializeField] private TextMeshProUGUI surveyTitleText;
        [SerializeField] private TextMeshProUGUI surveyInstrText;
        [SerializeField] private Button surveyDoneButton;

        [Header("Comparison Survey Panel")]
        [SerializeField] private GameObject comparisonPanel;
        [SerializeField] private TextMeshProUGUI comparisonTitleText;
        [SerializeField] private TextMeshProUGUI comparisonInstrText;

        [Header("Completion Panel")]
        [SerializeField] private GameObject completionPanel;
        [SerializeField] private TextMeshProUGUI completionTitleText;
        [SerializeField] private TextMeshProUGUI completionDetailText;

        private bool isActive;

        // === Guided relocalization (glass side) ===
        private enum GlassGuidedStep { WaitingWP01, WaitingWP00, Done }
        private GlassGuidedStep glassStep;
        private static readonly string[] GWPIds = { "B_WP01", "B_WP00" };
        private static readonly string[] GRoomNames = { "B111", "B110" };
        private Dictionary<string, AnchorRelocState> glassCache = new();
        private Coroutine glassCoroutine;

        private bool anchorSubscribed;
        private bool imageTrackingMode;

        private void Start()
        {
            // 버튼 리스너
            relocProceedButton?.onClick.AddListener(OnRelocProceed);
            setupContinueButton?.onClick.AddListener(OnSetupContinue);
            runningStartButton?.onClick.AddListener(OnRunningStart);
            surveyDoneButton?.onClick.AddListener(OnSurveyDone);

            // 이벤트 구독
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += OnStateChanged;
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged += OnConditionChanged;

            // Relocalization 이벤트 — Start()에서 null일 수 있으므로 ShowRelocPanel()에서 재시도
            EnsureAnchorSubscription();

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            HideAllPanels();
        }

        /// <summary>SpatialAnchorManager 이벤트 구독을 보장합니다. 중복 구독 방지.</summary>
        private void EnsureAnchorSubscription()
        {
            var mgr = SpatialAnchorManager.Instance;
            if (mgr == null) return;
            if (anchorSubscribed) return;

            mgr.OnRelocalizationDetailedProgress += OnRelocProgress;
            mgr.OnRelocalizationCompleteWithRate += OnRelocComplete;
            anchorSubscribed = true;
            Debug.Log("[GlassFlowUI] Anchor events subscribed");
        }


        private void OnConditionChanged(ExperimentCondition condition)
        {
            isActive = condition == ExperimentCondition.GlassOnly;
            Debug.Log($"[GlassFlowUI] OnConditionChanged({condition}) — isActive={isActive}");

            // 조건 변경 시 앵커 구독 재시도 (Start()에서 null이었을 수 있음)
            if (isActive)
                EnsureAnchorSubscription();

            if (!isActive)
                HideAllPanels();
        }

        private Coroutine deferredSetupCoroutine;

        private void OnStateChanged(ExperimentState state)
        {
            Debug.Log($"[GlassFlowUI] OnStateChanged({state}) — isActive={isActive}, " +
                $"glassStep={glassStep}, " +
                $"relocPanel={relocPanel != null}, setupPanel={setupPanel != null}, " +
                $"runningStartPanel={runningStartPanel != null}, surveyPanel={surveyPanel != null}");

            if (!isActive) return;

            // Relocalization → Setup 전환 시, 가이드가 아직 미완료면 잔여 결과를 보여준 후 전환
            if (state == ExperimentState.Setup && glassStep != GlassGuidedStep.Done)
            {
                Debug.Log($"[GlassFlowUI] Setup arrived but guide not done (step={glassStep}) — deferring");
                StopGlassCoroutine();
                if (deferredSetupCoroutine != null) StopCoroutine(deferredSetupCoroutine);
                deferredSetupCoroutine = StartCoroutine(FinishGuideAndShowSetup());
                return;
            }

            if (deferredSetupCoroutine != null)
            {
                StopCoroutine(deferredSetupCoroutine);
                deferredSetupCoroutine = null;
            }

            HideAllPanels();

            switch (state)
            {
                case ExperimentState.Relocalization:
                    ShowRelocPanel();
                    break;
                case ExperimentState.Setup:
                    ShowSetupPanel();
                    break;
                case ExperimentState.Running:
                    ShowRunningStartPanel();
                    break;
                case ExperimentState.Survey:
                    // Survey 패널은 PostConditionSurveyController가 표시하므로 여기서는 숨김 유지
                    break;
                case ExperimentState.ComparisonSurvey:
                    ShowComparisonPanel();
                    break;
                case ExperimentState.Complete:
                    ShowCompletionPanel();
                    break;
            }
        }

        /// <summary>
        /// 가이드 미완료 상태에서 Setup 전환이 도착했을 때,
        /// 잔여 가이드 결과를 순차 표시한 후 Setup 패널로 전환합니다.
        /// </summary>
        private IEnumerator FinishGuideAndShowSetup()
        {
            // WP01 단계에서 끊긴 경우 — WP01 결과 먼저 표시
            if (glassStep == GlassGuidedStep.WaitingWP01)
            {
                bool wp01Tracked = glassCache.TryGetValue(GWPIds[0], out var s01)
                    && s01 == AnchorRelocState.Tracking;
                string key01 = wp01Tracked
                    ? "glassflow.guide_recognized" : "glassflow.guide_timeout";
                if (relocProgressText)
                    relocProgressText.text = string.Format(
                        LocalizationManager.Get(key01), GRoomNames[0]);
                if (relocProgressBar) relocProgressBar.fillAmount = 0.25f;
                yield return new WaitForSeconds(1f);

                glassStep = GlassGuidedStep.WaitingWP00;
            }

            // WP00 결과 표시
            if (glassStep == GlassGuidedStep.WaitingWP00)
            {
                bool wp00Tracked = glassCache.TryGetValue(GWPIds[1], out var s00)
                    && s00 == AnchorRelocState.Tracking;
                string key00 = wp00Tracked
                    ? "glassflow.guide_recognized" : "glassflow.guide_timeout";
                if (relocProgressText)
                    relocProgressText.text = string.Format(
                        LocalizationManager.Get(key00), GRoomNames[1]);
                if (relocProgressBar) relocProgressBar.fillAmount = 0.75f;
                yield return new WaitForSeconds(1f);
            }

            // "준비 완료!" 표시
            glassStep = GlassGuidedStep.Done;
            if (relocProgressText)
                relocProgressText.text = LocalizationManager.Get("glassflow.guide_ready");
            if (relocProgressBar) relocProgressBar.fillAmount = 1f;
            yield return new WaitForSeconds(1.5f);

            // Setup 패널로 전환
            deferredSetupCoroutine = null;
            HideAllPanels();
            ShowSetupPanel();
        }

        private void HideAllPanels()
        {
            SetPanelActive(relocPanel, false);
            SetPanelActive(setupPanel, false);
            SetPanelActive(runningStartPanel, false);
            SetPanelActive(surveyPanel, false);
            SetPanelActive(comparisonPanel, false);
            SetPanelActive(completionPanel, false);
        }

        // === Relocalization ===

        private void ShowRelocPanel()
        {
            Debug.Log($"[GlassFlowUI] ShowRelocPanel — progressText={relocProgressText != null}, " +
                $"progressBar={relocProgressBar != null}, statusText={relocStatusText != null}, " +
                $"proceedBtn={relocProceedButton != null}");

            imageTrackingMode = ExperimentManager.Instance != null
                && ExperimentManager.Instance.UseImageTracking;

            // 가이드 상태 리셋
            glassStep = GlassGuidedStep.WaitingWP01;
            glassCache.Clear();
            StopGlassCoroutine();

            if (imageTrackingMode)
            {
                // Image Tracking 모드: 마커 스캔 안내
                if (relocProgressText)
                    relocProgressText.text = LocalizationManager.Get("glassflow.image_scan");
                if (relocProgressBar) relocProgressBar.fillAmount = 0f;
                if (relocStatusText) relocStatusText.text = "";
                if (relocProceedButton) relocProceedButton.gameObject.SetActive(false);

                // ImageTrackingAligner 이벤트 구독
                var aligner = ImageTrackingAligner.Instance;
                if (aligner != null)
                {
                    aligner.OnMarkerDetected -= OnGlassImageMarkerDetected;
                    aligner.OnMarkerDetected += OnGlassImageMarkerDetected;
                    aligner.OnAlignmentComplete -= OnGlassImageAlignmentComplete;
                    aligner.OnAlignmentComplete += OnGlassImageAlignmentComplete;
                }
            }
            else
            {
                // Spatial Anchor 모드: 기존 가이드
                if (relocProgressText)
                    relocProgressText.text = string.Format(
                        LocalizationManager.Get("glassflow.guide_face"), GRoomNames[0]);
                if (relocProgressBar) relocProgressBar.fillAmount = 0f;
                if (relocStatusText) relocStatusText.text = "";
                if (relocProceedButton) relocProceedButton.gameObject.SetActive(false);

                // ShowRelocPanel 시점에서 앵커 구독 재시도
                EnsureAnchorSubscription();
            }

            SetPanelActive(relocPanel, true);
            Debug.Log($"[GlassFlowUI] ShowRelocPanel — imageTracking={imageTrackingMode}");
        }

        // === Image Tracking 이벤트 핸들러 (Glass side) ===

        private void OnGlassImageMarkerDetected(string markerName, int detectedCount)
        {
            if (!isActive || relocPanel == null || !relocPanel.activeSelf) return;

            if (relocProgressText)
                relocProgressText.text = string.Format(
                    LocalizationManager.Get("glassflow.image_marker_found"), markerName);
            if (relocProgressBar)
            {
                int total = ImageTrackingAligner.Instance?.TotalMarkerCount ?? 1;
                relocProgressBar.fillAmount = (float)detectedCount / total;
            }
        }

        private void OnGlassImageAlignmentComplete(float successRate)
        {
            if (!isActive) return;

            glassStep = GlassGuidedStep.Done;
            if (relocProgressText)
                relocProgressText.text = LocalizationManager.Get("glassflow.guide_ready");
            if (relocProgressBar)
                relocProgressBar.fillAmount = 1f;
        }

        private void OnRelocProgress(string waypointId, AnchorRelocState state, int successCount, int timedOutCount, int total)
        {
            if (!isActive || relocPanel == null || !relocPanel.activeSelf) return;

            // 가이드 앵커 처리
            bool isGuidedAnchor = waypointId == GWPIds[0] || waypointId == GWPIds[1];
            if (isGuidedAnchor && state != AnchorRelocState.Pending)
            {
                glassCache[waypointId] = state;
                ProcessGlassGuidedStep(waypointId, state);
            }
        }

        private void ProcessGlassGuidedStep(string wpId, AnchorRelocState state)
        {
            switch (glassStep)
            {
                case GlassGuidedStep.WaitingWP01:
                    if (wpId == GWPIds[0]) // WP01
                    {
                        if (state == AnchorRelocState.Tracking)
                        {
                            ShowGlassGuidedMessage(
                                string.Format(LocalizationManager.Get("glassflow.guide_recognized"), GRoomNames[0]),
                                1f, () => TransitionToWP00());
                        }
                        else if (state == AnchorRelocState.TimedOut || state == AnchorRelocState.LoadFailed)
                        {
                            ShowGlassGuidedMessage(
                                string.Format(LocalizationManager.Get("glassflow.guide_timeout"), GRoomNames[0]),
                                1.5f, () => TransitionToWP00());
                        }
                    }
                    break;

                case GlassGuidedStep.WaitingWP00:
                    if (wpId == GWPIds[1]) // WP00
                    {
                        if (state == AnchorRelocState.Tracking)
                        {
                            ShowGlassGuidedMessage(
                                string.Format(LocalizationManager.Get("glassflow.guide_recognized"), GRoomNames[1]),
                                1f, () => GlassGuidedDone());
                        }
                        else if (state == AnchorRelocState.TimedOut || state == AnchorRelocState.LoadFailed)
                        {
                            ShowGlassGuidedMessage(
                                string.Format(LocalizationManager.Get("glassflow.guide_timeout"), GRoomNames[1]),
                                1.5f, () => GlassGuidedDone());
                        }
                    }
                    break;
            }
        }

        private void TransitionToWP00()
        {
            glassStep = GlassGuidedStep.WaitingWP00;

            if (relocProgressText)
                relocProgressText.text = string.Format(
                    LocalizationManager.Get("glassflow.guide_face"), GRoomNames[1]);
            if (relocProgressBar) relocProgressBar.fillAmount = 0.5f;

            // 캐시 체크 — WP00이 이미 도착했으면 즉시 처리
            if (glassCache.TryGetValue(GWPIds[1], out var cachedState))
            {
                ProcessGlassGuidedStep(GWPIds[1], cachedState);
            }
        }

        private void GlassGuidedDone()
        {
            glassStep = GlassGuidedStep.Done;

            if (relocProgressText)
                relocProgressText.text = LocalizationManager.Get("glassflow.guide_ready");
            if (relocProgressBar) relocProgressBar.fillAmount = 1f;

            // Proceed 버튼 표시하지 않음 — RelocalizationUI가 자동 진행
        }

        private void ShowGlassGuidedMessage(string message, float duration, System.Action onComplete)
        {
            StopGlassCoroutine();
            if (relocProgressText)
                relocProgressText.text = message;
            glassCoroutine = StartCoroutine(GlassGuidedDelay(duration, onComplete));
        }

        private IEnumerator GlassGuidedDelay(float duration, System.Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            glassCoroutine = null;
            onComplete?.Invoke();
        }

        private void StopGlassCoroutine()
        {
            if (glassCoroutine != null)
            {
                StopCoroutine(glassCoroutine);
                glassCoroutine = null;
            }
        }

        private void OnRelocComplete(float successRate)
        {
            if (!isActive) return;

            // 가이드가 완료된 경우 Proceed 버튼 표시 안 함 (RelocalizationUI가 자동 진행)
            if (glassStep == GlassGuidedStep.Done)
            {
                Debug.Log("[GlassFlowUI] OnRelocComplete — glass guided done, skipping proceed button");
                return;
            }

            // 가이드가 아직 진행 중이면 빠르게 완주 — 캐시된 결과 표시 후 "준비 완료!" 1초
            if (glassStep == GlassGuidedStep.WaitingWP01 || glassStep == GlassGuidedStep.WaitingWP00)
            {
                Debug.Log($"[GlassFlowUI] OnRelocComplete during guide step={glassStep} — fast-completing");
                StopGlassCoroutine();
                ForceCompleteGuide();
                return;
            }

            // Fallback — 가이드가 비활성인 경우
            if (relocProgressText)
                relocProgressText.text = LocalizationManager.Get("glassflow.reloc_done");
            if (relocProgressBar) relocProgressBar.fillAmount = 1f;
            if (relocProceedButton)
            {
                relocProceedButton.gameObject.SetActive(true);
                var btnText = relocProceedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = LocalizationManager.Get("glassflow.reloc_proceed");
            }
        }

        /// <summary>가이드를 빠르게 완주합니다. 캐시된 결과를 빠르게 표시 후 Done.</summary>
        private void ForceCompleteGuide()
        {
            // WP01 단계였으면 WP01 결과 표시 → WP00 결과 표시 → Done
            // WP00 단계였으면 WP00 결과 표시 → Done
            glassCoroutine = StartCoroutine(ForceCompleteSequence());
        }

        private IEnumerator ForceCompleteSequence()
        {
            // WP01 단계에서 강제 완료된 경우: 먼저 WP01 메시지 표시
            if (glassStep == GlassGuidedStep.WaitingWP01)
            {
                string wp01Msg = glassCache.ContainsKey(GWPIds[0])
                    ? string.Format(LocalizationManager.Get(
                        glassCache[GWPIds[0]] == AnchorRelocState.Tracking
                            ? "glassflow.guide_recognized" : "glassflow.guide_timeout"), GRoomNames[0])
                    : string.Format(LocalizationManager.Get("glassflow.guide_timeout"), GRoomNames[0]);
                if (relocProgressText) relocProgressText.text = wp01Msg;
                yield return new WaitForSeconds(1f);

                // WP00 가이드 표시
                glassStep = GlassGuidedStep.WaitingWP00;
                if (relocProgressText)
                    relocProgressText.text = string.Format(
                        LocalizationManager.Get("glassflow.guide_face"), GRoomNames[1]);
                if (relocProgressBar) relocProgressBar.fillAmount = 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            // WP00 결과 표시
            string wp00Msg = glassCache.ContainsKey(GWPIds[1])
                ? string.Format(LocalizationManager.Get(
                    glassCache[GWPIds[1]] == AnchorRelocState.Tracking
                        ? "glassflow.guide_recognized" : "glassflow.guide_timeout"), GRoomNames[1])
                : string.Format(LocalizationManager.Get("glassflow.guide_timeout"), GRoomNames[1]);
            if (relocProgressText) relocProgressText.text = wp00Msg;
            yield return new WaitForSeconds(1f);

            // "준비 완료!" 표시
            glassCoroutine = null;
            GlassGuidedDone();
        }

        private void OnRelocProceed()
        {
            SetPanelActive(relocPanel, false);
            ExperimentManager.Instance?.AdvanceState(); // → Setup
        }

        // === Setup ===

        private void ShowSetupPanel()
        {
            Debug.Log($"[GlassFlowUI] ShowSetupPanel — titleText={setupTitleText != null}, " +
                $"detailText={setupDetailText != null}, button={setupContinueButton != null}");
            var session = ExperimentManager.Instance?.session;
            if (setupTitleText)
                setupTitleText.text = LocalizationManager.Get("glassflow.setup_title");
            if (setupDetailText)
                setupDetailText.text = string.Format(
                    LocalizationManager.Get("glassflow.setup_detail"),
                    session?.participantId, session?.condition, session?.missionSet);
            if (setupContinueButton)
            {
                var btnText = setupContinueButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = LocalizationManager.Get("glassflow.continue");
            }

            SetPanelActive(setupPanel, true);
        }

        private void OnSetupContinue()
        {
            SetPanelActive(setupPanel, false);
            ExperimentManager.Instance?.AdvanceState(); // → Running
        }

        // === Running Start ===

        private void ShowRunningStartPanel()
        {
            Debug.Log($"[GlassFlowUI] ShowRunningStartPanel — titleText={runningTitleText != null}, " +
                $"detailText={runningDetailText != null}, button={runningStartButton != null}");
            var session = ExperimentManager.Instance?.session;

            if (runningTitleText)
                runningTitleText.text = LocalizationManager.Get("glassflow.condition_title");
            if (runningDetailText)
                runningDetailText.text = LocalizationManager.Get("glassflow.condition_detail_glass");
            if (runningStartButton)
            {
                var btnText = runningStartButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = LocalizationManager.Get("glassflow.start");
            }

            // 미션 로드
            MissionManager.Instance?.LoadMissions(session?.missionSet);

            SetPanelActive(runningStartPanel, true);
        }

        private void OnRunningStart()
        {
            var mm = MissionManager.Instance;
            if (mm == null || !mm.HasLoadedMissions)
            {
                Debug.LogWarning("[GlassFlowUI] Missions not loaded — retrying...");
                var session2 = ExperimentManager.Instance?.session;
                mm?.LoadMissions(session2?.missionSet);

                if (mm == null || !mm.HasLoadedMissions)
                {
                    Debug.LogError("[GlassFlowUI] Missions still not loaded after retry");
                    return;
                }
            }

            SetPanelActive(runningStartPanel, false);
            mm.StartNextMission();
        }

        // === Survey ===

        private void ShowSurveyPanel()
        {
            Debug.Log($"[GlassFlowUI] ShowSurveyPanel — titleText={surveyTitleText != null}, " +
                $"instrText={surveyInstrText != null}, button={surveyDoneButton != null}");
            if (surveyTitleText)
                surveyTitleText.text = LocalizationManager.Get("glassflow.survey_title");
            if (surveyInstrText)
                surveyInstrText.text = LocalizationManager.Get("glassflow.survey_instr");
            if (surveyDoneButton)
            {
                var btnText = surveyDoneButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = LocalizationManager.Get("glassflow.survey_done");
            }

            SetPanelActive(surveyPanel, true);
        }

        private void OnSurveyDone()
        {
            SetPanelActive(surveyPanel, false);
            ExperimentManager.Instance?.AdvanceState(); // → Complete
        }

        // === Comparison Survey ===

        private void ShowComparisonPanel()
        {
            if (comparisonTitleText != null)
                comparisonTitleText.text = LocalizationManager.Get("glassflow.comparison_title");
            if (comparisonInstrText != null)
                comparisonInstrText.text = LocalizationManager.Get("glassflow.comparison_instr");

            SetPanelActive(comparisonPanel, true);
        }

        // === Completion ===

        private void ShowCompletionPanel()
        {
            Debug.Log($"[GlassFlowUI] ShowCompletionPanel — titleText={completionTitleText != null}, " +
                $"detailText={completionDetailText != null}");
            if (completionTitleText)
                completionTitleText.text = LocalizationManager.Get("glassflow.complete_title");
            if (completionDetailText)
                completionDetailText.text = LocalizationManager.Get("glassflow.complete_text");

            SetPanelActive(completionPanel, true);
        }

        // === Helpers ===

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null)
            {
                Debug.LogWarning($"[GlassFlowUI] SetPanelActive — panel reference is NULL!");
                return;
            }

            panel.SetActive(active);

            // 활성 패널 상태 갱신
            HasActivePanel = (relocPanel != null && relocPanel.activeSelf)
                          || (setupPanel != null && setupPanel.activeSelf)
                          || (runningStartPanel != null && runningStartPanel.activeSelf)
                          || (surveyPanel != null && surveyPanel.activeSelf)
                          || (comparisonPanel != null && comparisonPanel.activeSelf)
                          || (completionPanel != null && completionPanel.activeSelf);

            // 패널 검증 로그 비활성화 — logcat 디버깅 시 ~5-8회/전환 반복
            // if (active)
            // {
            //     Debug.Log($"[GlassFlowUI] SetPanelActive({panel.name}, true) — " +
            //         $"activeInHierarchy={panel.activeInHierarchy}");
            //     StartCoroutine(VerifyPanelVisibility(panel));
            // }
        }

        private IEnumerator VerifyPanelVisibility(GameObject panel)
        {
            yield return new WaitForSeconds(0.5f);
            if (panel == null || !panel.activeInHierarchy) yield break;

            var cg = panel.GetComponent<CanvasGroup>();
            var rt = panel.GetComponent<RectTransform>();
            var canvas = panel.GetComponentInParent<Canvas>();
            var parentCg = panel.transform.parent?.GetComponent<CanvasGroup>();
            var canvasRt = canvas?.GetComponent<RectTransform>();

            // 조상 CanvasGroup 체크
            float ancestorAlpha = 1f;
            var t = panel.transform.parent;
            while (t != null)
            {
                var acg = t.GetComponent<CanvasGroup>();
                if (acg != null && acg.alpha < 1f)
                {
                    ancestorAlpha = acg.alpha;
                    Debug.LogError($"[GlassFlowUI] ANCESTOR CanvasGroup alpha={acg.alpha} on {t.name}!");
                    break;
                }
                t = t.parent;
            }

            Debug.Log($"[GlassFlowUI] VERIFY {panel.name} @0.5s — " +
                $"active={panel.activeInHierarchy}, cg.alpha={cg?.alpha}, " +
                $"ancestorAlpha={ancestorAlpha}, " +
                $"rect={rt?.rect}, worldCorners=[see next], " +
                $"canvas.renderMode={canvas?.renderMode}, " +
                $"canvas.worldCam={canvas?.worldCamera?.name ?? "NULL"}, " +
                $"canvasRect={canvasRt?.rect}, canvasScale={canvas?.transform.localScale}, " +
                $"canvasParent={canvas?.transform.parent?.name ?? "ROOT"}");

            // TMP 텍스트 진단
            var tmps = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmps)
            {
                Debug.Log($"[GlassFlowUI] VERIFY TMP '{tmp.gameObject.name}' — " +
                    $"text='{(tmp.text?.Length > 30 ? tmp.text.Substring(0, 30) : tmp.text)}', " +
                    $"font={tmp.font?.name ?? "NULL"}, fontSize={tmp.fontSize}, " +
                    $"color={tmp.color}, enabled={tmp.enabled}, " +
                    $"rectSize={tmp.rectTransform.rect.size}");
            }
        }

        private void OnLanguageChanged(Language lang)
        {
            if (!isActive) return;
            // 현재 상태에 맞게 텍스트 갱신
            if (ExperimentManager.Instance != null)
                OnStateChanged(ExperimentManager.Instance.CurrentState);
        }

        private void OnDestroy()
        {
            StopGlassCoroutine();
            if (deferredSetupCoroutine != null)
            {
                StopCoroutine(deferredSetupCoroutine);
                deferredSetupCoroutine = null;
            }

            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;
            if (anchorSubscribed && SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.OnRelocalizationDetailedProgress -= OnRelocProgress;
                SpatialAnchorManager.Instance.OnRelocalizationCompleteWithRate -= OnRelocComplete;
            }
            var aligner = ImageTrackingAligner.Instance;
            if (aligner != null)
            {
                aligner.OnMarkerDetected -= OnGlassImageMarkerDetected;
                aligner.OnAlignmentComplete -= OnGlassImageAlignmentComplete;
            }
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}
