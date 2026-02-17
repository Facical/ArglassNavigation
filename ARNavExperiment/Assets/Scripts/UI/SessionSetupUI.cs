using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.UI
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

            if (orderGroupDropdown != null)
            {
                orderGroupDropdown.ClearOptions();
                orderGroupDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "S1 (Glass Only → Hybrid)",
                    "S2 (Hybrid → Glass Only)"
                });
            }

            if (participantIdInput != null)
                participantIdInput.text = "P01";

            if (errorText != null)
                errorText.gameObject.SetActive(false);

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (previewText == null) return;

            string pid = participantIdInput?.text ?? "P01";
            string group = orderGroupDropdown != null && orderGroupDropdown.value == 0 ? "S1" : "S2";

            int num = 0;
            if (pid.Length > 1) int.TryParse(pid.Substring(1), out num);
            string firstRoute = (num % 2 == 1) ? "A (서쪽-북쪽)" : "B (동쪽-북쪽)";
            string secondRoute = (num % 2 == 1) ? "B (동쪽-북쪽)" : "A (서쪽-북쪽)";

            string cond1 = group == "S1" ? "Glass Only" : "Hybrid";
            string cond2 = group == "S1" ? "Hybrid" : "Glass Only";

            previewText.text =
                $"참가자: {pid}\n" +
                $"그룹: {group}\n" +
                $"1차: {cond1} + Route {firstRoute}\n" +
                $"2차: {cond2} + Route {secondRoute}";
        }

        private void OnStartClicked()
        {
            string pid = participantIdInput?.text?.Trim() ?? "";
            if (string.IsNullOrEmpty(pid) || pid.Length < 2)
            {
                ShowError("참가자 ID를 입력해주세요 (예: P01)");
                return;
            }

            string group = orderGroupDropdown != null && orderGroupDropdown.value == 0 ? "S1" : "S2";

            ExperimentManager.Instance?.InitializeSession(pid, group);
            ExperimentManager.Instance?.StartExperiment();

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
