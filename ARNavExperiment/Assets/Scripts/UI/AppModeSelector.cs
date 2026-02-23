using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 앱 시작 시 모드 선택 화면.
    /// [매핑 모드] / [실험 모드] 선택.
    /// </summary>
    public class AppModeSelector : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject modeSelectorPanel;
        [SerializeField] private Button mappingModeButton;
        [SerializeField] private Button experimentModeButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Panels")]
        [SerializeField] private MappingModeUI mappingModeUI;
        [SerializeField] private GameObject sessionSetupPanel;

        private void Start()
        {
            mappingModeButton?.onClick.AddListener(OnMappingModeSelected);
            experimentModeButton?.onClick.AddListener(OnExperimentModeSelected);

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && anchorMgr.HasMappingData())
            {
                var routeA = anchorMgr.GetRouteMappings("A");
                var routeB = anchorMgr.GetRouteMappings("B");
                statusText.text = $"매핑 상태: Route A {routeA.Count}개, Route B {routeB.Count}개 앵커";
                statusText.color = new Color(0.6f, 1f, 0.6f);
            }
            else
            {
                statusText.text = "매핑 데이터 없음 — 먼저 매핑 모드를 실행하세요";
                statusText.color = new Color(1f, 0.8f, 0.4f);
            }
        }

        private void OnMappingModeSelected()
        {
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(false);

            if (mappingModeUI != null)
            {
                mappingModeUI.gameObject.SetActive(true);
                mappingModeUI.Initialize();
            }

            // 글래스 매핑 오버레이 활성화
            var overlay = FindObjectOfType<MappingGlassOverlay>(true);
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                overlay.Show(mappingModeUI != null ? mappingModeUI.CurrentRoute : "A");
            }

            // 3D 앵커 시각화 활성화
            var visualizer = FindObjectOfType<MappingAnchorVisualizer>(true);
            if (visualizer != null)
            {
                visualizer.gameObject.SetActive(true);
                visualizer.RefreshFromExistingAnchors();
            }

            Debug.Log("[AppModeSelector] 매핑 모드 진입");
        }

        private void OnExperimentModeSelected()
        {
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(false);

            // 앵커 로드 → 재인식 → 실험 플로우 시작
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && anchorMgr.HasMappingData())
            {
                // Relocalization 시작 (ExperimentManager가 처리)
                ExperimentManager.Instance?.TransitionTo(ExperimentState.Relocalization);
            }
            else
            {
                // 매핑 없이 실험 (fallback 좌표 사용)
                Debug.LogWarning("[AppModeSelector] 매핑 데이터 없음 — fallback 좌표로 진행");
                if (sessionSetupPanel != null) sessionSetupPanel.SetActive(true);
            }

            Debug.Log("[AppModeSelector] 실험 모드 진입");
        }

        /// <summary>
        /// 모드 선택 화면으로 돌아갑니다.
        /// </summary>
        public void ReturnToModeSelector()
        {
            if (modeSelectorPanel != null) modeSelectorPanel.SetActive(true);
            if (mappingModeUI != null) mappingModeUI.gameObject.SetActive(false);

            // 글래스 매핑 오버레이 비활성화
            var overlay = FindObjectOfType<MappingGlassOverlay>(true);
            if (overlay != null) overlay.Hide();

            // 3D 앵커 시각화 비활성화 + 마커 정리
            var visualizer = FindObjectOfType<MappingAnchorVisualizer>(true);
            if (visualizer != null)
            {
                visualizer.ClearAllMarkers();
                visualizer.gameObject.SetActive(false);
            }

            UpdateStatus();
        }
    }
}
