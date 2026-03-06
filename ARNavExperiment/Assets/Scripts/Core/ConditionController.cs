using UnityEngine;
using TMPro;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;
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
        [SerializeField] private GameObject experimenterCanvas;

        public event System.Action<ExperimentCondition> OnConditionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 조건 설정 전: BeamProCanvas 활성 + LockedScreen 표시
            // ScreenSpaceOverlay가 없으면 XREAL SDK가 글래스 스테레오 뷰를 폰에 미러링함
            if (beamProUI) beamProUI.SetActive(true);
            if (lockedScreenUI) lockedScreenUI.SetActive(true);
        }

        public void SetCondition(ExperimentCondition condition)
        {
            CurrentCondition = condition;

            switch (condition)
            {
                case ExperimentCondition.GlassOnly:
                    // GlassOnly: BeamProCanvas 활성 유지 + LockedScreen으로 폰 화면 가림
                    // ScreenSpaceOverlay 캔버스가 없으면 글래스 스테레오 뷰가 폰에 미러링됨
                    if (beamProUI) beamProUI.SetActive(true);
                    if (lockedScreenUI) lockedScreenUI.SetActive(true);
                    if (experimenterCanvas) experimenterCanvas.SetActive(false);
                    else Debug.LogError("[ConditionCtrl] experimenterCanvas is NULL — Wire Scene References 재실행 필요!");
                    DeviceStateTracker.Instance?.SetLocked(true);
                    HandTrackingManager.Instance?.ActivateHandRays();  // GlassOnly: 핸드트래킹으로 UI 조작
                    break;

                case ExperimentCondition.Hybrid:
                    if (lockedScreenUI) lockedScreenUI.SetActive(false);
                    if (beamProUI) beamProUI.SetActive(true);
                    if (experimenterCanvas) experimenterCanvas.SetActive(true);
                    DeviceStateTracker.Instance?.SetLocked(false);
                    HandTrackingManager.Instance?.DeactivateHandRays(); // Hybrid: 터치로 UI 조작
                    break;
            }

            string condStr = condition == ExperimentCondition.GlassOnly ? "glass_only" : "hybrid";
            DomainEventBus.Instance?.Publish(new ConditionChanged(condStr));

            Debug.Log($"[ConditionCtrl] SetCondition({condition}) — beamProUI.active={beamProUI?.activeSelf}");
            OnConditionChanged?.Invoke(condition);
            Debug.Log($"[ConditionCtrl] OnConditionChanged invoked for {condition}");
        }
    }
}
