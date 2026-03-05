using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.BeamPro
{
    public class POIDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI capacityText;
        [SerializeField] private TextMeshProUGUI equipmentText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button closeButton;

        private float showTime;
        private POIData currentPOI;

        private void Start()
        {
            closeButton?.onClick.AddListener(Hide);
            if (panel) panel.SetActive(false);
        }

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
            if (currentPOI != null && panel != null && panel.activeSelf)
                RefreshContent();
        }

        public void Show(POIData poi)
        {
            currentPOI = poi;
            showTime = Time.time;
            RefreshContent();
            if (panel) panel.SetActive(true);
        }

        private void RefreshContent()
        {
            if (currentPOI == null) return;

            if (titleText) titleText.text = currentPOI.GetDisplayName();
            if (descriptionText) descriptionText.text = currentPOI.GetDescription();
            if (capacityText) capacityText.text = currentPOI.capacity > 0
                ? string.Format(LocalizationManager.Get("poi.capacity"), currentPOI.capacity)
                : "";
            if (equipmentText)
            {
                var equipList = currentPOI.GetEquipmentList();
                equipmentText.text = equipList != null && equipList.Length > 0
                    ? string.Format(LocalizationManager.Get("poi.equipment"), string.Join(", ", equipList))
                    : "";
            }
            if (iconImage && currentPOI.icon) iconImage.sprite = currentPOI.icon;
        }

        public void Hide()
        {
            currentPOI = null;
            if (panel) panel.SetActive(false);
        }
    }
}
