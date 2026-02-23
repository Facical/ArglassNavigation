using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// Beam Pro에 표시되는 실험자 전용 제어 패널.
    /// ExperimenterCanvas (ScreenSpaceOverlay, sortOrder=10)에 배치.
    /// 실험 상태 표시 + 상태 전환/미션 제어 버튼 제공.
    /// </summary>
    public class ExperimenterHUD : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private TextMeshProUGUI waypointText;

        [Header("Anchor Status")]
        [SerializeField] private TextMeshProUGUI anchorStatusText;

        [Header("Controls")]
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button nextMissionButton;

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

            if (anchorStatusText != null && Navigation.WaypointManager.Instance != null)
            {
                int fallback = Navigation.WaypointManager.Instance.FallbackWaypointCount;
                anchorStatusText.text = fallback > 0
                    ? $"\u26a0 Fallback: {fallback} WP"
                    : "Anchors: OK";
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
