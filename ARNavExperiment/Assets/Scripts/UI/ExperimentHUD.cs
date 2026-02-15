using UnityEngine;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.UI
{
    public class ExperimentHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        [Header("Experimenter Controls")]
        [SerializeField] private UnityEngine.UI.Button advanceButton;
        [SerializeField] private UnityEngine.UI.Button nextMissionButton;

        private void Start()
        {
            advanceButton?.onClick.AddListener(OnAdvanceClicked);
            nextMissionButton?.onClick.AddListener(OnNextMissionClicked);

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
            if (conditionText && ConditionController.Instance != null)
                conditionText.text = $"Condition: {ConditionController.Instance.CurrentCondition}";
        }

        private void OnAdvanceClicked()
        {
            ExperimentManager.Instance?.AdvanceState();
        }

        private void OnNextMissionClicked()
        {
            MissionManager.Instance?.StartNextMission();
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;
        }
    }
}
