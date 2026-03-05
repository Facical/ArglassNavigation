using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Presentation.Experimenter
{
    public class SessionSetupUI : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField participantIdInput;
        [SerializeField] private TMP_Dropdown orderGroupDropdown;

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI previewText;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Buttons")]
        [SerializeField] private Button startButton;

        private void Start()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            participantIdInput?.onValueChanged.AddListener(_ => UpdatePreview());
            orderGroupDropdown?.onValueChanged.AddListener(_ => UpdatePreview());

            RefreshDropdownOptions();

            if (participantIdInput != null)
                participantIdInput.text = "P01";

            if (errorText != null)
                errorText.gameObject.SetActive(false);

            UpdatePreview();

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDestroy()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(Language lang)
        {
            RefreshLocalization();
        }

        private void RefreshLocalization()
        {
            RefreshDropdownOptions();
            UpdatePreview();
        }

        private void RefreshDropdownOptions()
        {
            if (orderGroupDropdown != null)
            {
                int prevValue = orderGroupDropdown.value;
                orderGroupDropdown.ClearOptions();
                orderGroupDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    LocalizationManager.Get("session.dropdown_s1"),
                    LocalizationManager.Get("session.dropdown_s2")
                });
                orderGroupDropdown.value = prevValue;
            }
        }

        private void UpdatePreview()
        {
            if (previewText == null) return;

            string pid = participantIdInput?.text ?? "P01";
            string group = orderGroupDropdown != null && orderGroupDropdown.value == 0 ? "S1" : "S2";

            int num = 0;
            if (pid.Length > 1) int.TryParse(pid.Substring(1), out num);
            string firstRoute = (num % 2 == 1)
                ? LocalizationManager.Get("session.route_a")
                : LocalizationManager.Get("session.route_b");
            string secondRoute = (num % 2 == 1)
                ? LocalizationManager.Get("session.route_b")
                : LocalizationManager.Get("session.route_a");

            string cond1 = group == "S1"
                ? LocalizationManager.Get("session.glass_only")
                : LocalizationManager.Get("session.hybrid");
            string cond2 = group == "S1"
                ? LocalizationManager.Get("session.hybrid")
                : LocalizationManager.Get("session.glass_only");

            previewText.text = string.Format(
                LocalizationManager.Get("session.preview"),
                pid, group, cond1, firstRoute, cond2, secondRoute);
        }

        private void OnStartClicked()
        {
            string pid = participantIdInput?.text?.Trim() ?? "";
            if (string.IsNullOrEmpty(pid) || pid.Length < 2)
            {
                ShowError(LocalizationManager.Get("session.error_no_id"));
                return;
            }

            string group = orderGroupDropdown != null && orderGroupDropdown.value == 0 ? "S1" : "S2";

            ExperimentManager.Instance?.InitializeSession(pid, group);

            // 매핑 데이터가 있으면 경로별 Relocalization, 없으면 직접 실험 시작
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && anchorMgr.HasMappingData())
            {
                ExperimentManager.Instance?.TransitionTo(ExperimentState.Relocalization);
            }
            else
            {
                Debug.LogWarning("[SessionSetupUI] 매핑 데이터 없음 — fallback 좌표로 진행");
                ExperimentManager.Instance?.StartExperiment();
            }

            gameObject.SetActive(false);
        }

        private void ShowError(string msg)
        {
            if (errorText == null) return;
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }
    }
}
