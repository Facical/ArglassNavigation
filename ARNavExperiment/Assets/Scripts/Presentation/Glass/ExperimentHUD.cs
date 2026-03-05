using UnityEngine;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;

namespace ARNavExperiment.Presentation.Glass
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

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            // Idle(모드 선택) 상태에서는 디버그 HUD를 숨김
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (missionText != null && MissionManager.Instance != null)
            {
                var m = MissionManager.Instance.CurrentMission;
                missionText.text = m != null
                    ? string.Format(LocalizationManager.Get("hud.mission"), m.missionId, MissionManager.Instance.CurrentState)
                    : LocalizationManager.Get("hud.no_mission");
            }

            if (waypointText != null && Navigation.WaypointManager.Instance != null)
            {
                var wp = Navigation.WaypointManager.Instance.CurrentWaypoint;
                float dist = Navigation.WaypointManager.Instance.GetDistanceToNext();
                waypointText.text = wp != null
                    ? string.Format(LocalizationManager.Get("hud.waypoint"), wp.waypointId, $"{dist:F1}")
                    : LocalizationManager.Get("hud.no_waypoint");
            }

        }

        private void UpdateStateDisplay(ExperimentState state)
        {
            // Idle(모드 선택) 상태에서는 HUD 숨김, 그 외 상태에서는 표시
            bool shouldShow = state != ExperimentState.Idle;
            if (gameObject.activeSelf != shouldShow)
                gameObject.SetActive(shouldShow);

            if (stateText) stateText.text = string.Format(LocalizationManager.Get("hud.state"), state);
            if (conditionText)
            {
                if (state == ExperimentState.Condition1 || state == ExperimentState.Condition2)
                    conditionText.text = string.Format(LocalizationManager.Get("hud.condition"), ConditionController.Instance?.CurrentCondition);
                else
                    conditionText.text = LocalizationManager.Get("hud.condition_none");
            }
        }

        private void RefreshLocalization(Language lang)
        {
            // 현재 상태를 기반으로 모든 표시 텍스트를 갱신
            if (ExperimentManager.Instance != null)
                UpdateStateDisplay(ExperimentManager.Instance.CurrentState);
        }

        private void OnDestroy()
        {
            if (ExperimentManager.Instance != null)
                ExperimentManager.Instance.OnStateChanged -= UpdateStateDisplay;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }
    }
}
