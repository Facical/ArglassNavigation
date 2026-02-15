using UnityEngine;

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
    }
}
