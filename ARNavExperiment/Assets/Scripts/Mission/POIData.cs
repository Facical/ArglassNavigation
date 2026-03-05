using UnityEngine;
using ARNavExperiment.Core;

namespace ARNavExperiment.Mission
{
    [CreateAssetMenu(fileName = "POI", menuName = "Experiment/POIData")]
    public class POIData : ScriptableObject
    {
        public string poiId;
        public string poiType;
        public string displayName;

        [TextArea(1, 3)]
        public string description;

        public int capacity;
        public string[] equipment;
        public Vector2 mapPosition;
        public Sprite icon;

        [Header("Korean Localization")]
        public string displayNameKo;

        [TextArea(1, 3)]
        public string descriptionKo;

        public string[] equipmentKo;

        public string GetDisplayName()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(displayNameKo))
                return displayNameKo;
            return displayName;
        }

        public string GetDescription()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(descriptionKo))
                return descriptionKo;
            return description;
        }

        public string[] GetEquipmentList()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && equipmentKo != null && equipmentKo.Length > 0)
                return equipmentKo;
            return equipment;
        }
    }
}
