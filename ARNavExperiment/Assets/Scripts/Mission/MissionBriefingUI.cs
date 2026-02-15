using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ARNavExperiment.Mission
{
    public class MissionBriefingUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI missionIdText;
        [SerializeField] private TextMeshProUGUI briefingText;
        [SerializeField] private Button confirmButton;

        private System.Action onConfirm;

        private void Start()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            if (panel) panel.SetActive(false);
        }

        public void Show(MissionData mission, System.Action onConfirmCallback)
        {
            onConfirm = onConfirmCallback;
            if (missionIdText) missionIdText.text = $"Mission {mission.missionId}";
            if (briefingText) briefingText.text = mission.briefingText;
            if (panel) panel.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            if (panel) panel.SetActive(false);
            onConfirm?.Invoke();
        }
    }
}
