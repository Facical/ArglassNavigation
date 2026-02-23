using UnityEngine;
using ARNavExperiment.Logging;

namespace ARNavExperiment.Core
{
    public enum ExperimentCondition
    {
        GlassOnly,
        Hybrid
    }

    public class ConditionController : MonoBehaviour
    {
        public static ConditionController Instance { get; private set; }

        public ExperimentCondition CurrentCondition { get; private set; }

        [SerializeField] private GameObject beamProUI;
        [SerializeField] private GameObject lockedScreenUI;

        public event System.Action<ExperimentCondition> OnConditionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 조건 설정 전에는 BeamPro UI 숨기기
            if (beamProUI) beamProUI.SetActive(false);
            if (lockedScreenUI) lockedScreenUI.SetActive(false);
        }

        public void SetCondition(ExperimentCondition condition)
        {
            CurrentCondition = condition;

            switch (condition)
            {
                case ExperimentCondition.GlassOnly:
                    if (beamProUI) beamProUI.SetActive(true);      // 글래스 WorldSpace로 표시
                    if (lockedScreenUI) lockedScreenUI.SetActive(false); // 잠금화면 불필요
                    // DeviceStateTracker 잠금 해제 — 폰 사용 시도 감지용으로 유지
                    break;

                case ExperimentCondition.Hybrid:
                    if (lockedScreenUI) lockedScreenUI.SetActive(false);
                    if (beamProUI) beamProUI.SetActive(true);
                    DeviceStateTracker.Instance?.SetLocked(false);
                    break;
            }

            string condStr = condition == ExperimentCondition.GlassOnly ? "glass_only" : "hybrid";
            EventLogger.Instance?.SetCondition(condStr);
            EventLogger.Instance?.LogEvent("CONDITION_CHANGE",
                extraData: $"{{\"condition\":\"{condStr}\"}}");

            OnConditionChanged?.Invoke(condition);
            Debug.Log($"[ConditionController] Condition set to: {condition}");
        }
    }
}
