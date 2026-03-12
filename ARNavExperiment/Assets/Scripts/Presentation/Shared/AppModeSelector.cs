using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Presentation.Mapping;
using ARNavExperiment.Presentation.Glass;

namespace ARNavExperiment.Presentation.Shared
{
    public class AppModeSelector : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject modeSelectorPanel;
        [SerializeField] private TMP_InputField participantIdInput;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Mission Set Selection")]
        [SerializeField] private Button set1Button;
        [SerializeField] private Button set2Button;

        [Header("Condition Buttons")]
        [SerializeField] private Button glassOnlyButton;
        [SerializeField] private Button hybridButton;

        [Header("Bottom Buttons")]
        [SerializeField] private Button mappingButton;

        [Header("Language")]
        [SerializeField] private Button languageButton;
        [SerializeField] private TextMeshProUGUI languageButtonText;
        [SerializeField] private GameObject languagePopup;
        [SerializeField] private Button langKoButton;
        [SerializeField] private Button langEnButton;

        [Header("Panels")]
        [SerializeField] private MappingModeUI mappingModeUI;

        [Header("Glass Status")]
        [SerializeField] private GlassModeStatusPanel glassModeStatus;

        private string selectedMissionSet = "Set1";
        private TextMeshProUGUI titleText;

        private void Start()
        {
            glassOnlyButton?.onClick.AddListener(OnGlassOnlySelected);
            hybridButton?.onClick.AddListener(OnHybridSelected);
            set1Button?.onClick.AddListener(OnSet1Selected);
            set2Button?.onClick.AddListener(OnSet2Selected);
            mappingButton?.onClick.AddListener(OnMappingModeSelected);
            languageButton?.onClick.AddListener(OnLanguageToggle);
            langKoButton?.onClick.AddListener(OnKoreanSelected);
            langEnButton?.onClick.AddListener(OnEnglishSelected);

            if (modeSelectorPanel != null)
            {
                foreach (var t in modeSelectorPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.gameObject.name == "ModeTitle") titleText = t;
                }
            }

            if (errorText != null)
                errorText.gameObject.SetActive(false);

            if (participantIdInput != null)
                participantIdInput.text = "P01";

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            RefreshLocalization();
            UpdateSetButtonColors();

            if (glassModeStatus != null)
                glassModeStatus.ShowModeSelection();
        }

        // === Condition Selection ===

        private void OnGlassOnlySelected()
        {
            StartWithCondition("glass_only");
        }

        private void OnHybridSelected()
        {
            StartWithCondition("hybrid");
        }

        private void StartWithCondition(string condition)
        {
            string pid = participantIdInput?.text?.Trim() ?? "";
            if (string.IsNullOrEmpty(pid) || pid.Length < 2)
            {
                ShowError(LocalizationManager.Get("session.error_no_id"));
                return;
            }

            if (errorText != null)
                errorText.gameObject.SetActive(false);

            ExperimentManager.Instance?.InitializeSession(pid, condition, selectedMissionSet);

            if (glassModeStatus != null) glassModeStatus.Hide();

            // 매핑 데이터가 있으면 Relocalization, 없으면 직접 실험 시작
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && anchorMgr.HasMappingData())
            {
                ExperimentManager.Instance?.TransitionTo(ExperimentState.Relocalization);
            }
            else
            {
                Debug.LogWarning("[AppModeSelector] 매핑 데이터 없음 — fallback 좌표로 진행");
                ExperimentManager.Instance?.StartExperiment();
            }

            if (modeSelectorPanel != null)
                modeSelectorPanel.SetActive(false);

            Debug.Log($"[AppModeSelector] 실험 시작: PID={pid}, Condition={condition}, MissionSet={selectedMissionSet}");
        }

        // === Mission Set Selection ===

        private void OnSet1Selected()
        {
            selectedMissionSet = "Set1";
            UpdateSetButtonColors();
        }

        private void OnSet2Selected()
        {
            selectedMissionSet = "Set2";
            UpdateSetButtonColors();
        }

        private void UpdateSetButtonColors()
        {
            if (set1Button != null)
            {
                var img = set1Button.GetComponent<Image>();
                if (img != null)
                    img.color = selectedMissionSet == "Set1"
                        ? new Color(0.2f, 0.5f, 0.8f, 1f)
                        : new Color(0.3f, 0.3f, 0.35f, 1f);
            }
            if (set2Button != null)
            {
                var img = set2Button.GetComponent<Image>();
                if (img != null)
                    img.color = selectedMissionSet == "Set2"
                        ? new Color(0.2f, 0.5f, 0.8f, 1f)
                        : new Color(0.3f, 0.3f, 0.35f, 1f);
            }
        }

        // === Mapping Mode ===

        private void OnMappingModeSelected()
        {
            if (languagePopup != null) languagePopup.SetActive(false);
            if (glassModeStatus != null) glassModeStatus.ShowMappingMode();
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(false);

            if (mappingModeUI != null)
            {
                mappingModeUI.gameObject.SetActive(true);
                mappingModeUI.Initialize();
            }

            var overlay = FindObjectOfType<MappingGlassOverlay>(true);
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                overlay.Show("B");
            }

            var visualizer = FindObjectOfType<MappingAnchorVisualizer>(true);
            if (visualizer != null)
            {
                visualizer.gameObject.SetActive(true);
                visualizer.RefreshFromExistingAnchors();
            }

            Debug.Log("[AppModeSelector] 매핑 모드 진입");
        }

        // === Language ===

        private void OnLanguageToggle()
        {
            if (languagePopup != null)
                languagePopup.SetActive(!languagePopup.activeSelf);
        }

        private void OnKoreanSelected()
        {
            LocalizationManager.Instance?.SetLanguage(Language.KO);
            if (languagePopup != null) languagePopup.SetActive(false);
        }

        private void OnEnglishSelected()
        {
            LocalizationManager.Instance?.SetLanguage(Language.EN);
            if (languagePopup != null) languagePopup.SetActive(false);
        }

        private void OnLanguageChanged(Language lang)
        {
            RefreshLocalization();
        }

        // === Localization ===

        private void RefreshLocalization()
        {
            if (titleText != null) titleText.text = LocalizationManager.Get("appmode.title");
            UpdateLanguageButtonText();
            UpdateStatus();
        }

        private void UpdateLanguageButtonText()
        {
            if (languageButtonText == null) return;
            languageButtonText.text = "Language";
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && anchorMgr.HasMappingData())
            {
                var routeB = anchorMgr.GetRouteMappings("B");
                statusText.text = string.Format(LocalizationManager.Get("appmode.mapping_status"),
                    routeB.Count);
                statusText.color = new Color(0.6f, 1f, 0.6f);
            }
            else
            {
                statusText.text = LocalizationManager.Get("appmode.no_mapping");
                statusText.color = new Color(1f, 0.8f, 0.4f);
            }
        }

        // === Utility ===

        public void ReturnToModeSelector()
        {
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(true);
            if (mappingModeUI != null) mappingModeUI.gameObject.SetActive(false);

            var overlay = FindObjectOfType<MappingGlassOverlay>(true);
            if (overlay != null) overlay.Hide();

            var visualizer = FindObjectOfType<MappingAnchorVisualizer>(true);
            if (visualizer != null)
            {
                visualizer.ClearAllMarkers();
                visualizer.gameObject.SetActive(false);
            }

            if (glassModeStatus != null)
                glassModeStatus.ShowModeSelection();

            RefreshLocalization();
        }

        private void ShowError(string msg)
        {
            if (errorText == null) return;
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}
