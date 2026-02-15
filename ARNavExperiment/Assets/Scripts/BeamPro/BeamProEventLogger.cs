using UnityEngine;
using ARNavExperiment.Logging;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
{
    public class BeamProEventLogger : MonoBehaviour
    {
        [SerializeField] private InfoCardManager infoCardManager;
        [SerializeField] private InteractiveMapController mapController;

        private MissionData currentMission;

        public void SetCurrentMission(MissionData mission)
        {
            currentMission = mission;
        }

        public void LogPOIViewed(POIData poi, float viewDuration)
        {
            EventLogger.Instance?.LogEvent("BEAM_POI_VIEWED",
                beamContentType: "poi_detail",
                extraData: $"{{\"poi_id\":\"{poi.poiId}\",\"poi_type\":\"{poi.poiType}\",\"view_duration_s\":{viewDuration:F1}}}");
        }

        public void LogComparisonViewed(ComparisonData data)
        {
            string items = "";
            if (data.itemNames != null && data.itemNames.Length > 0)
                items = $"[{string.Join(",", System.Array.ConvertAll(data.itemNames, n => $"\"{n}\""))}]";

            EventLogger.Instance?.LogEvent("BEAM_COMPARISON_VIEWED",
                beamContentType: "comparison",
                extraData: $"{{\"comparison_id\":\"{data.comparisonId}\",\"items_compared\":{items}}}");
        }
    }
}
