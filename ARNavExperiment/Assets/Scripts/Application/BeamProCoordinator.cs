using System.Collections;
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

        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        private void Start()
        {
            if (!_subscribed) TrySubscribe();
            if (!_subscribed) StartCoroutine(RetrySubscribe());
        }

        private void TrySubscribe()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Subscribe<MissionStarted>(OnMissionStarted);
            bus.Subscribe<MissionCompleted>(OnMissionCompleted);
            bus.Subscribe<WaypointReached>(OnWaypointReached);

            _subscribed = true;
            Debug.Log($"[{GetType().Name}] Subscribed to DomainEventBus");
        }

        private IEnumerator RetrySubscribe()
        {
            for (int i = 0; i < 10 && !_subscribed; i++)
            {
                yield return new WaitForSeconds(0.1f);
                TrySubscribe();
            }
            if (!_subscribed)
                Debug.LogError($"[{GetType().Name}] Failed to subscribe after 10 retries");
        }

        private void OnDisable()
        {
            var bus = DomainEventBus.Instance;
            if (bus == null) return;

            bus.Unsubscribe<MissionStarted>(OnMissionStarted);
            bus.Unsubscribe<MissionCompleted>(OnMissionCompleted);
            bus.Unsubscribe<WaypointReached>(OnWaypointReached);

            _subscribed = false;
        }

        private void OnMissionStarted(MissionStarted e)
        {
            var hub = BeamProHubController.Instance;
            if (hub == null) { Debug.LogWarning("[BeamProCoordinator] Hub null"); return; }

            // MissionManager에서 직접 접근하던 BeamPro 데이터 로드를 여기서 수행
            var missionMgr = MissionManager.Instance;
            if (missionMgr?.CurrentMission == null) { Debug.LogWarning("[BeamProCoordinator] CurrentMission null"); return; }
            var mission = missionMgr.CurrentMission;

            Debug.Log($"[BeamProCoordinator] Mission={mission.missionId}, infoCards={mission.infoCards?.Length ?? 0}");

            hub.MissionRef?.LoadMission(mission);
            if (mission.infoCards != null && mission.infoCards.Length > 0)
                hub.InfoCardMgr?.LoadCards(mission.infoCards);

            var mapCtrl = hub.MapController;
            if (mapCtrl != null)
            {
                if (floorPlanSprite != null)
                    mapCtrl.SetFloorPlan(floorPlanSprite);
                else
                    Debug.LogWarning("[BeamProCoordinator] floorPlanSprite is null — 도면 이미지를 표시할 수 없습니다. SceneWiringTool을 재실행하세요.");
                if (mission.relevantPOIs != null) mapCtrl.LoadPOIs(mission.relevantPOIs);
                var wm = WaypointManager.Instance;
                var targetPos = wm?.GetWaypointPosition(mission.targetWaypointId);
                if (targetPos.HasValue)
                {
                    if (wm.IsUsingFallback(mission.targetWaypointId))
                        mapCtrl.SetDestinationFloorPlan(new Vector2(targetPos.Value.x, targetPos.Value.z));
                    else
                        mapCtrl.SetDestination(targetPos.Value);
                }
                mapCtrl.StartPositionTracking();

                // Auto-zoom: 현재 WP → 목적지 WP 도면 좌표 기반
                var currentFallback = wm.CurrentWaypoint != null
                    ? new Vector2(wm.CurrentWaypoint.fallbackPosition.x, wm.CurrentWaypoint.fallbackPosition.z)
                    : (Vector2?)null;
                var destFallback = wm.GetWaypointFallbackXZ(mission.targetWaypointId);
                if (currentFallback.HasValue && destFallback.HasValue)
                    mapCtrl.AutoZoomToFit(currentFallback.Value, destFallback.Value);
            }
        }

        private void OnWaypointReached(WaypointReached e)
        {
            var hub = BeamProHubController.Instance;
            if (hub == null) return;

            // 정보 카드 자동 표시 + info_card 탭 전환
            if (hub.InfoCardMgr != null && hub.InfoCardMgr.TryAutoShow(e.WaypointId))
            {
                hub.SwitchTab(1); // info_card 탭
                Debug.Log($"[BeamProCoordinator] Auto-showed info card at {e.WaypointId}");
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
