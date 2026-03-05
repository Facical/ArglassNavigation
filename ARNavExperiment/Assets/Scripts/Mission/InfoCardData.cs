using UnityEngine;
using ARNavExperiment.Core;

namespace ARNavExperiment.Mission
{
    [CreateAssetMenu(fileName = "InfoCard", menuName = "Experiment/InfoCardData")]
    public class InfoCardData : ScriptableObject
    {
        public string cardId;
        public string cardType;
        public string title;

        [TextArea(2, 5)]
        public string content;

        public Sprite image;
        public string triggerWaypointId;
        public bool autoShow;

        [Header("Korean Localization")]
        public string titleKo;

        [TextArea(2, 5)]
        public string contentKo;

        public string GetTitle()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(titleKo))
                return titleKo;
            return title;
        }

        public string GetContent()
        {
            if (LocalizationManager.Instance != null && LocalizationManager.Instance.CurrentLanguage == Language.KO
                && !string.IsNullOrEmpty(contentKo))
                return contentKo;
            return content;
        }
    }
}
