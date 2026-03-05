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
        [SerializeField] private Button mappingModeButton;
        [SerializeField] private Button experimentModeButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Language")]
        [SerializeField] private Button languageButton;
        [SerializeField] private TextMeshProUGUI languageButtonText;
        [SerializeField] private GameObject languagePopup;
        [SerializeField] private Button langKoButton;
        [SerializeField] private Button langEnButton;

        [Header("Panels")]
        [SerializeField] private MappingModeUI mappingModeUI;
        [SerializeField] private GameObject sessionSetupPanel;

        [Header("Glass Status")]
        [SerializeField] private GlassModeStatusPanel glassModeStatus;

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private TextMeshProUGUI mappingBtnText;
        private TextMeshProUGUI expBtnText;

        private void Start()
        {
            mappingModeButton?.onClick.AddListener(OnMappingModeSelected);
            experimentModeButton?.onClick.AddListener(OnExperimentModeSelected);
            languageButton?.onClick.AddListener(OnLanguageToggle);
            langKoButton?.onClick.AddListener(OnKoreanSelected);
            langEnButton?.onClick.AddListener(OnEnglishSelected);

            if (mappingModeButton != null)
                mappingBtnText = mappingModeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (experimentModeButton != null)
                expBtnText = experimentModeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (modeSelectorPanel != null)
            {
                foreach (var t in modeSelectorPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.gameObject.name == "ModeTitle") titleText = t;
                    else if (t.gameObject.name == "ModeSubtitle") subtitleText = t;
                }
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

            RefreshLocalization();

            if (glassModeStatus != null)
                glassModeStatus.ShowModeSelection();
        }

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

        private void RefreshLocalization()
        {
            if (titleText != null) titleText.text = LocalizationManager.Get("appmode.title");
            if (subtitleText != null) subtitleText.text = LocalizationManager.Get("appmode.subtitle");
            if (mappingBtnText != null) mappingBtnText.text = LocalizationManager.Get("appmode.mapping_btn");
            if (expBtnText != null) expBtnText.text = LocalizationManager.Get("appmode.experiment_btn");
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
                var routeA = anchorMgr.GetRouteMappings("A");
                var routeB = anchorMgr.GetRouteMappings("B");
                statusText.text = string.Format(LocalizationManager.Get("appmode.mapping_status"),
                    routeA.Count, routeB.Count);
                statusText.color = new Color(0.6f, 1f, 0.6f);
            }
            else
            {
                statusText.text = LocalizationManager.Get("appmode.no_mapping");
                statusText.color = new Color(1f, 0.8f, 0.4f);
            }
        }

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
                overlay.Show(mappingModeUI != null ? mappingModeUI.CurrentRoute : "A");
            }

            var visualizer = FindObjectOfType<MappingAnchorVisualizer>(true);
            if (visualizer != null)
            {
                visualizer.gameObject.SetActive(true);
                visualizer.RefreshFromExistingAnchors();
            }

            Debug.Log("[AppModeSelector] 매핑 모드 진입");
        }

        private void OnExperimentModeSelected()
        {
            if (languagePopup != null) languagePopup.SetActive(false);
            if (glassModeStatus != null) glassModeStatus.Hide();
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(false);

            if (sessionSetupPanel != null)
                sessionSetupPanel.SetActive(true);

            Debug.Log("[AppModeSelector] 실험 모드 — 세션 설정 먼저 표시");
        }

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

        private void OnDestroy()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }
}
