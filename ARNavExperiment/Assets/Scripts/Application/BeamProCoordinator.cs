using UnityEngine;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Presentation.BeamPro;
using ARNavExperiment.Mission;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.Application
{
    /// <summary>
    /// MissionManager에서 분리된 BeamPro 데이터 조율 서비스.
    /// MissionStarted 이벤트를 구독하여 BeamPro 허브에 미션 데이터를 로드.
    /// MissionManager가 BeamProHubController를 직접 참조하지 않도록 중재.
    /// </summary>
    public class BeamProCoordinator : MonoBehaviour
    {
        [SerializeField] private Sprite floorPlanSprite;

        private void OnEnable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Subscribe<MissionStarted>(OnMissionStarted);
            bus.Subscribe<MissionCompleted>(OnMissionCompleted);
        }

        private void OnDisable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Unsubscribe<MissionStarted>(OnMissionStarted);
            bus.Unsubscribe<MissionCompleted>(OnMissionCompleted);
        }

        private void OnMissionStarted(MissionStarted e)
        {
            var hub = BeamProHubController.Instance;
            if (hub == null) return;

            // MissionManager에서 직접 접근하던 BeamPro 데이터 로드를 여기서 수행
            var missionMgr = MissionManager.Instance;
            if (missionMgr?.CurrentMission == null) return;
            var mission = missionMgr.CurrentMission;

            hub.MissionRef?.LoadMission(mission);
            if (mission.infoCards != null && mission.infoCards.Length > 0)
                hub.InfoCardMgr?.LoadCards(mission.infoCards);

            var mapCtrl = hub.MapController;
            if (mapCtrl != null)
            {
                if (floorPlanSprite != null) mapCtrl.SetFloorPlan(floorPlanSprite);
                if (mission.relevantPOIs != null) mapCtrl.LoadPOIs(mission.relevantPOIs);
                var targetPos = WaypointManager.Instance?.GetWaypointPosition(mission.targetWaypointId);
                if (targetPos.HasValue) mapCtrl.SetDestination(targetPos.Value);
                mapCtrl.StartPositionTracking();
            }
        }

        private void OnMissionCompleted(MissionCompleted e)
        {
            var hub = BeamProHubController.Instance;
            if (hub == null) return;

            hub.MapController?.HideDestination();
            hub.MapController?.StopPositionTracking();
        }
    }
}
