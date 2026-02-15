using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
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

        private void Start()
        {
            closeButton?.onClick.AddListener(Hide);
            if (panel) panel.SetActive(false);
        }

        public void Show(POIData poi)
        {
            showTime = Time.time;

            if (titleText) titleText.text = poi.displayName;
            if (descriptionText) descriptionText.text = poi.description;
            if (capacityText) capacityText.text = poi.capacity > 0 ? $"수용인원: {poi.capacity}명" : "";
            if (equipmentText) equipmentText.text = poi.equipment != null && poi.equipment.Length > 0
                ? $"장비: {string.Join(", ", poi.equipment)}" : "";
            if (iconImage && poi.icon) iconImage.sprite = poi.icon;

            if (panel) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel) panel.SetActive(false);
        }
    }
}
