using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.Glass
{
    /// <summary>
    /// GlassOnly 조건에서 ExperimentCanvas(글래스)에 표시되는 플로우 제어 UI.
    /// Relocalization → Setup → Running → Survey → Complete 패널을 순차 표시.
    /// 핸드트래킹 pinch로 버튼을 조작하여 실험을 진행.
    /// </summary>
    public class GlassFlowUI : MonoBehaviour
    {
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

        [Header("Survey Panel")]
        [SerializeField] private GameObject surveyPanel;
        [SerializeField] private TextMeshProUGUI surveyTitleText;
        [SerializeField] private TextMeshProUGUI surveyInstrText;
        [SerializeField] private Button surveyDoneButton;

        [Header("Completion Panel")]
        [SerializeField] private GameObject completionPanel;
        [SerializeField] private TextMeshProUGUI completionTitleText;
        [SerializeField] private TextMeshProUGUI completionDetailText;

        private bool isActive;
        private float relocCompletionRate;
        private bool relocComplete;
        private float autoProceedTimer;
        private const float AUTO_PROCEED_DELAY = 30f;

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

            // Relocalization 이벤트
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.OnRelocalizationDetailedProgress += OnRelocProgress;
                SpatialAnchorManager.Instance.OnRelocalizationCompleteWithRate += OnRelocComplete;
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            HideAllPanels();
        }

        private void Update()
        {
            if (!isActive || !relocComplete) return;

            // Auto-proceed 카운트다운
            if (autoProceedTimer > 0f)
            {
                autoProceedTimer -= Time.deltaTime;
                if (relocStatusText)
                    relocStatusText.text = string.Format(
                        LocalizationManager.Get("glassflow.reloc_auto"),
                        Mathf.CeilToInt(autoProceedTimer));

                if (autoProceedTimer <= 0f)
                    OnRelocProceed();
            }
        }

        private void OnConditionChanged(ExperimentCondition condition)
        {
            isActive = condition == ExperimentCondition.GlassOnly;
            Debug.Log($"[GlassFlowUI] OnConditionChanged({condition}) — isActive={isActive}");

            if (!isActive)
                HideAllPanels();
        }

        private void OnStateChanged(ExperimentState state)
        {
            if (!isActive) return;

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
                    ShowSurveyPanel();
                    break;
                case ExperimentState.Complete:
                    ShowCompletionPanel();
                    break;
            }
        }

        private void HideAllPanels()
        {
            SetPanelActive(relocPanel, false);
            SetPanelActive(setupPanel, false);
            SetPanelActive(runningStartPanel, false);
            SetPanelActive(surveyPanel, false);
            SetPanelActive(completionPanel, false);
        }

        // === Relocalization ===

        private void ShowRelocPanel()
        {
            relocComplete = false;
            autoProceedTimer = 0f;

            if (relocProgressText)
                relocProgressText.text = string.Format(
                    LocalizationManager.Get("glassflow.reloc_scanning"), 0);
            if (relocProgressBar) relocProgressBar.fillAmount = 0f;
            if (relocStatusText) relocStatusText.text = "";
            if (relocProceedButton) relocProceedButton.gameObject.SetActive(false);

            SetPanelActive(relocPanel, true);

            // 앵커 재인식 실제 시작
            var session = ExperimentManager.Instance?.session;
            string route = session?.route;
            if (!string.IsNullOrEmpty(route))
            {
                SpatialAnchorManager.Instance?.LoadRouteAnchors(route);
                Debug.Log($"[GlassFlowUI] LoadRouteAnchors({route}) 호출");
            }
            else
            {
                Debug.LogError("[GlassFlowUI] session.route가 비어있음 — 앵커 로드 불가");
            }
        }

        private void OnRelocProgress(string waypointId, AnchorRelocState state, int successCount, int timedOutCount, int total)
        {
            if (!isActive || relocPanel == null || !relocPanel.activeSelf) return;

            int processed = successCount + timedOutCount;
            float pct = total > 0 ? (float)processed / total * 100f : 0f;

            if (relocProgressText)
                relocProgressText.text = string.Format(
                    LocalizationManager.Get("glassflow.reloc_scanning"), $"{pct:F0}");
            if (relocProgressBar && total > 0)
                relocProgressBar.fillAmount = (float)processed / total;
        }

        private void OnRelocComplete(float successRate)
        {
            if (!isActive) return;

            relocComplete = true;
            relocCompletionRate = successRate;
            autoProceedTimer = AUTO_PROCEED_DELAY;

            float pct = successRate * 100f;
            if (relocProgressText)
                relocProgressText.text = string.Format(
                    LocalizationManager.Get("glassflow.reloc_complete"), $"{pct:F0}");
            if (relocProgressBar) relocProgressBar.fillAmount = 1f;
            if (relocProceedButton)
            {
                relocProceedButton.gameObject.SetActive(true);
                var btnText = relocProceedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText) btnText.text = LocalizationManager.Get("glassflow.reloc_proceed");
            }
        }

        private void OnRelocProceed()
        {
            autoProceedTimer = 0f;
            relocComplete = false;
            SetPanelActive(relocPanel, false);
            ExperimentManager.Instance?.AdvanceState(); // → Setup
        }

        // === Setup ===

        private void ShowSetupPanel()
        {
            var session = ExperimentManager.Instance?.session;
            if (setupTitleText)
                setupTitleText.text = LocalizationManager.Get("glassflow.setup_title");
            if (setupDetailText)
                setupDetailText.text = string.Format(
                    LocalizationManager.Get("glassflow.setup_detail"),
                    session?.participantId, session?.condition, session?.route);
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
            MissionManager.Instance?.LoadMissions(session?.route);

            SetPanelActive(runningStartPanel, true);
        }

        private void OnRunningStart()
        {
            var mm = MissionManager.Instance;
            if (mm == null || !mm.HasLoadedMissions)
            {
                Debug.LogWarning("[GlassFlowUI] Missions not loaded — retrying...");
                var session = ExperimentManager.Instance?.session;
                mm?.LoadMissions(session?.route);

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

        // === Completion ===

        private void ShowCompletionPanel()
        {
            if (completionTitleText)
                completionTitleText.text = LocalizationManager.Get("glassflow.complete_title");
            if (completionDetailText)
                completionDetailText.text = LocalizationManager.Get("glassflow.complete_text");

            SetPanelActive(completionPanel, true);
        }

        // === Helpers ===

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null) return;

            if (active)
            {
                panel.SetActive(true);
                panel.GetComponent<PanelFader>()?.FadeIn();
            }
            else
            {
                var fader = panel.GetComponent<PanelFader>();
                if (fader != null && panel.activeInHierarchy)
                    fader.FadeOut();
                else
                    panel.SetActive(false);
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
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= OnStateChanged;
            if (ConditionController.Instance != null)
                ConditionController.Instance.OnConditionChanged -= OnConditionChanged;
            if (SpatialAnchorManager.Instance != null)
            {
                SpatialAnchorManager.Instance.OnRelocalizationDetailedProgress -= OnRelocProgress;
                SpatialAnchorManager.Instance.OnRelocalizationCompleteWithRate -= OnRelocComplete;
            }
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}
