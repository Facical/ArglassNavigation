using UnityEngine;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Presentation.Glass
{
    /// <summary>
    /// 글래스(ExperimentCanvas)에 현재 앱 모드 상태를 표시하는 비대화형 패널.
    /// MappingGlassOverlay 패턴을 따르며, AppModeSelector가 모드 전환 시 호출.
    /// </summary>
    public class GlassModeStatusPanel : MonoBehaviour
    {
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI instructionText;

        private static readonly Color COLOR_TITLE = Color.white;
        private static readonly Color COLOR_STATUS_WAITING = new Color(0.6f, 0.8f, 1f);
        private static readonly Color COLOR_STATUS_ACTIVE = new Color(0.3f, 1f, 0.3f);
        private static readonly Color COLOR_INSTRUCTION = new Color(0.6f, 0.6f, 0.6f);

        private enum PanelState { Hidden, ModeSelection, MappingMode }
        private PanelState currentPanelState = PanelState.Hidden;

        private void OnEnable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(Language lang)
        {
            // Re-show current state with updated language
            switch (currentPanelState)
            {
                case PanelState.ModeSelection:
                    ShowModeSelection();
                    break;
                case PanelState.MappingMode:
                    ShowMappingMode();
                    break;
                // Hidden: nothing to refresh
            }
        }

        /// <summary>모드 선택 대기 상태 표시</summary>
        public void ShowModeSelection()
        {
            currentPanelState = PanelState.ModeSelection;
            SetPanelActive(true);
            SetTitle(LocalizationManager.Get("glass.title"));
            SetStatus(LocalizationManager.Get("glass.waiting"), COLOR_STATUS_WAITING);
            SetInstruction(LocalizationManager.Get("glass.instruction"));
        }

        /// <summary>매핑 모드 진행 중 표시 (MappingGlassOverlay가 별도 처리하므로 숨김)</summary>
        public void ShowMappingMode()
        {
            currentPanelState = PanelState.MappingMode;
            // 매핑 모드에서는 MappingGlassOverlay가 상세 정보를 표시하므로 이 패널은 숨김
            SetPanelActive(false);
        }

        /// <summary>패널 숨김</summary>
        public void Hide()
        {
            currentPanelState = PanelState.Hidden;
            SetPanelActive(false);
        }

        private void SetPanelActive(bool active)
        {
            if (statusPanel != null)
                statusPanel.SetActive(active);
        }

        private void SetTitle(string text)
        {
            if (titleText != null)
            {
                titleText.text = text;
                titleText.color = COLOR_TITLE;
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (statusText != null)
            {
                statusText.text = text;
                statusText.color = color;
            }
        }

        private void SetInstruction(string text)
        {
            if (instructionText != null)
            {
                instructionText.text = text;
                instructionText.color = COLOR_INSTRUCTION;
            }
        }
    }
}
