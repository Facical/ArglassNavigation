using UnityEngine;
using ARNavExperiment.Mission;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.BeamPro
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
            DomainEventBus.Instance?.Publish(new BeamPOIViewed(poi.poiId));
        }

        public void LogComparisonViewed(ComparisonData data)
        {
            DomainEventBus.Instance?.Publish(new BeamComparisonViewed(data.comparisonId));
        }
    }
}
