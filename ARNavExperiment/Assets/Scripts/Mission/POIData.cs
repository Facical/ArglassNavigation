using UnityEngine;

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
    }
}
