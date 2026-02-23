using UnityEngine;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 글래스(ExperimentCanvas)에 표시되는 참가자용 HUD.
    /// 표시 전용 — 제어 버튼은 ExperimenterHUD(ExperimenterCanvas)로 이동됨.
    /// </summary>
    public class ExperimentHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        private void Start()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged += UpdateStateDisplay;
        }

        private void Update()
        {
            if (missionText != null && MissionManager.Instance != null)
            {
                var m = MissionManager.Instance.CurrentMission;
                missionText.text = m != null
                    ? $"Mission: {m.missionId} ({MissionManager.Instance.CurrentState})"
                    : "No active mission";
            }

            if (waypointText != null && Navigation.WaypointManager.Instance != null)
            {
                var wp = Navigation.WaypointManager.Instance.CurrentWaypoint;
                float dist = Navigation.WaypointManager.Instance.GetDistanceToNext();
                waypointText.text = wp != null
                    ? $"WP: {wp.waypointId} ({dist:F1}m)"
                    : "No waypoint";
            }
        }

        private void UpdateStateDisplay(ExperimentState state)
        {
            if (stateText) stateText.text = $"State: {state}";
            if (conditionText)
            {
                if (state == ExperimentState.Condition1 || state == ExperimentState.Condition2)
                    conditionText.text = $"Condition: {ConditionController.Instance?.CurrentCondition}";
                else
                    conditionText.text = "Condition: \u2014";
            }
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;
        }
    }
}
