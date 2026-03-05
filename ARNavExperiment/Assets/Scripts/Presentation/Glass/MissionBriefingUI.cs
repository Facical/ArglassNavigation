using UnityEngine;
using TMPro;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.Glass
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
            if (missionIdText) missionIdText.text = string.Format(
                LocalizationManager.Get("briefing.mission"), mission.missionId);
            if (briefingText) briefingText.text = mission.GetBriefingText();
            if (panel)
            {
                panel.SetActive(true);
                panel.GetComponent<PanelFader>()?.FadeIn();
            }
        }

        public void Hide()
        {
            var fader = panel?.GetComponent<PanelFader>();
            if (fader != null)
                fader.FadeOut(() => { onConfirm = null; });
            else
            {
                if (panel) panel.SetActive(false);
                onConfirm = null;
            }
        }

        private void OnConfirmClicked()
        {
            var cb = onConfirm;
            onConfirm = null;
            var fader = panel?.GetComponent<PanelFader>();
            if (fader != null)
                fader.FadeOut(() => cb?.Invoke());
            else
            {
                if (panel) panel.SetActive(false);
                cb?.Invoke();
            }
        }
    }
}
