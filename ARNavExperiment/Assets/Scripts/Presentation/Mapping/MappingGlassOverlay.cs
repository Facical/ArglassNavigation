using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Presentation.Mapping
{
    /// <summary>
    /// 글래스(ExperimentCanvas)에 표시되는 매핑 모드 전용 HUD.
    /// 선택된 웨이포인트, 앵커 품질, 진행도, 생성 결과 플래시를 표시.
    /// </summary>
    public class MappingGlassOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI routeText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI waypointText;
        [SerializeField] private TextMeshProUGUI qualityText;
        [SerializeField] private Image qualityIcon;
        [SerializeField] private TextMeshProUGUI flashText;
        [SerializeField] private TextMeshProUGUI guidanceText;
        [SerializeField] private TextMeshProUGUI slamStatusText;
        [SerializeField] private MappingMiniMap miniMap;

        private MappingModeUI mappingModeUI;
        private float qualityPollTimer;
        private float flashTimer;
        private const float QUALITY_POLL_INTERVAL = 0.5f;
        private const float FLASH_DURATION = 2f;

        private static readonly Color COLOR_QUALITY_BAD = new Color(1f, 0.267f, 0.267f);    // #FF4444
        private static readonly Color COLOR_QUALITY_OK = new Color(1f, 0.667f, 0f);          // #FFAA00
        private static readonly Color COLOR_QUALITY_GOOD = new Color(0.267f, 1f, 0.267f);    // #44FF44
        private static readonly Color COLOR_FLASH_SUCCESS = new Color(0.3f, 1f, 0.3f);
        private static readonly Color COLOR_FLASH_FAIL = new Color(1f, 0.3f, 0.3f);
        private static readonly Color COLOR_HINT = new Color(0.6f, 0.6f, 0.6f);

        private void OnEnable()
        {
            mappingModeUI = FindObjectOfType<MappingModeUI>(true);
            if (mappingModeUI != null)
            {
                mappingModeUI.OnWaypointSelectedEvent += HandleWaypointSelected;
                mappingModeUI.OnAnchorCreateResult += HandleAnchorResult;
                mappingModeUI.OnMapZoomToggle += ToggleMapZoom;
                mappingModeUI.OnReferenceAnchorCreateResult += HandleReferenceAnchorResult;
            }

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnMappingQualityUpdate += HandleQualityObservation;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += RefreshLocalization;

            if (flashText != null)
            {
                flashText.alpha = 0f;
                flashText.text = "";
            }

            if (guidanceText != null)
            {
                guidanceText.alpha = 0f;
                guidanceText.text = "";
            }

            if (slamStatusText != null)
            {
                slamStatusText.text = LocalizationManager.Get("overlay.checking_slam");
                slamStatusText.color = COLOR_HINT;
            }

            if (waypointText != null)
            {
                waypointText.text = LocalizationManager.Get("overlay.select_wp");
                waypointText.color = COLOR_HINT;
            }
        }

        private void OnDisable()
        {
            if (mappingModeUI != null)
            {
                mappingModeUI.OnWaypointSelectedEvent -= HandleWaypointSelected;
                mappingModeUI.OnAnchorCreateResult -= HandleAnchorResult;
                mappingModeUI.OnMapZoomToggle -= ToggleMapZoom;
                mappingModeUI.OnReferenceAnchorCreateResult -= HandleReferenceAnchorResult;
            }

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnMappingQualityUpdate -= HandleQualityObservation;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= RefreshLocalization;
        }

        private void Update()
        {
            qualityPollTimer += Time.deltaTime;
            if (qualityPollTimer >= QUALITY_POLL_INTERVAL)
            {
                qualityPollTimer = 0f;
                UpdateQualityDisplay();
                UpdateProgress();
            }

            // 플래시 메시지 페이드아웃
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashText != null)
                {
                    float alpha = Mathf.Clamp01(flashTimer / 0.5f); // 마지막 0.5초에 페이드
                    flashText.alpha = flashTimer > 0.5f ? 1f : alpha;
                }
            }
        }

        private void HandleWaypointSelected(string waypointId, string locationName)
        {
            if (waypointText != null)
            {
                waypointText.text = $"\u25b6 {waypointId}: {locationName}";
                waypointText.color = Color.white;
            }
            miniMap?.SetSelectedWaypoint(waypointId);
        }

        private void HandleAnchorResult(string waypointId, bool success)
        {
            if (guidanceText != null)
                guidanceText.alpha = 0f;

            if (flashText == null) return;

            if (success)
            {
                flashText.text = LocalizationManager.Get("overlay.anchor_created");
                flashText.color = COLOR_FLASH_SUCCESS;
                miniMap?.MarkAsMapped(waypointId);

                // SLAM→FloorPlan 보정 데이터 전달
                var anchorPos = SpatialAnchorManager.Instance?.GetAnchorPosition(waypointId);
                if (anchorPos.HasValue)
                    miniMap?.AddCalibrationPoint(waypointId, anchorPos.Value);
            }
            else
            {
                flashText.text = LocalizationManager.Get("overlay.creation_failed");
                flashText.color = COLOR_FLASH_FAIL;
            }

            flashText.alpha = 1f;
            flashTimer = FLASH_DURATION;
        }

        private void HandleReferenceAnchorResult(string roomId, bool success)
        {
            if (flashText == null) return;

            if (success)
            {
                flashText.text = $"\u2713 {roomId} " + LocalizationManager.Get("overlay.anchor_created");
                flashText.color = COLOR_FLASH_SUCCESS;
                miniMap?.UpdateReferenceMappingStatus();

                // SLAM→FloorPlan 보정 데이터 전달
                var anchorPos = SpatialAnchorManager.Instance?.GetAnchorPosition(roomId);
                if (anchorPos.HasValue)
                    miniMap?.AddCalibrationPoint(roomId, anchorPos.Value);
            }
            else
            {
                flashText.text = $"\u2717 {roomId} " + LocalizationManager.Get("overlay.creation_failed");
                flashText.color = COLOR_FLASH_FAIL;
            }

            flashText.alpha = 1f;
            flashTimer = FLASH_DURATION;
        }

        private void HandleQualityObservation(int quality, string guidance)
        {
            Color[] qualityColors = { COLOR_QUALITY_BAD, COLOR_QUALITY_OK, COLOR_QUALITY_GOOD };

            if (guidanceText != null)
            {
                guidanceText.text = guidance;
                guidanceText.color = qualityColors[quality];
                guidanceText.alpha = 1f;
            }

            // 품질 관찰 중에는 기존 품질 표시도 업데이트
            if (qualityText != null)
            {
                string[] qualityKeys = { "overlay.quality_poor", "overlay.quality_fair", "overlay.quality_good" };
                string qualityName = LocalizationManager.Get(qualityKeys[quality]);
                qualityText.text = $"Quality: {qualityName}";
                qualityText.color = qualityColors[quality];
            }

            if (qualityIcon != null)
                qualityIcon.color = qualityColors[quality];
        }

        private void UpdateQualityDisplay()
        {
            // SLAM tracking status
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null && slamStatusText != null)
            {
                var slamStatus = anchorMgr.GetSlamTrackingStatus();
                slamStatusText.text = slamStatus.userGuidance;
                slamStatusText.color = slamStatus.isReady ? COLOR_QUALITY_GOOD : COLOR_QUALITY_BAD;
            }

            // 대기 상태 표시 — 앵커 생성 중 실시간 품질은 HandleQualityObservation()이 처리
            if (qualityText != null)
            {
                qualityText.text = LocalizationManager.Get("overlay.quality_waiting");
                qualityText.color = COLOR_HINT;
            }

            if (qualityIcon != null)
                qualityIcon.color = COLOR_HINT;
        }

        private void UpdateProgress()
        {
            if (progressText == null || mappingModeUI == null) return;

            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;

            string route = mappingModeUI.CurrentRoute;
            var mappings = anchorMgr.GetRouteMappings(route);
            int total = mappingModeUI.TotalWaypointCount;
            int mapped = mappings.Count;

            progressText.text = string.Format(LocalizationManager.Get("overlay.mapped_count"), mapped, total);

            miniMap?.UpdateMappingStatus(route);
        }

        /// <summary>매핑 모드 진입 시 호출. 패널 활성화 + 경로 표시.</summary>
        public void Show(string routeId)
        {
            if (overlayPanel != null) overlayPanel.SetActive(true);
            if (headerText != null) headerText.text = LocalizationManager.Get("overlay.header");
            if (routeText != null) routeText.text = LocalizationManager.Get("mapping.title");
            if (waypointText != null)
            {
                waypointText.text = LocalizationManager.Get("overlay.select_wp");
                waypointText.color = COLOR_HINT;
            }
            if (flashText != null) flashText.alpha = 0f;
            flashTimer = 0f;

            // 이벤트 재연결 (Show 시점에 MappingModeUI가 활성화되어 있을 수 있음)
            if (mappingModeUI == null)
            {
                mappingModeUI = FindObjectOfType<MappingModeUI>(true);
                if (mappingModeUI != null)
                {
                    mappingModeUI.OnWaypointSelectedEvent += HandleWaypointSelected;
                    mappingModeUI.OnAnchorCreateResult += HandleAnchorResult;
                    mappingModeUI.OnMapZoomToggle += ToggleMapZoom;
                    mappingModeUI.OnReferenceAnchorCreateResult += HandleReferenceAnchorResult;
                }
            }

            // 미니맵 초기화 및 표시
            if (miniMap != null)
            {
                miniMap.Initialize();
                miniMap.SetRoute(routeId);
                miniMap.Show();
            }
        }

        /// <summary>미니맵 줌 토글. 확대 시 오버레이 텍스트 요소를 숨기고, 축소 시 복원.</summary>
        public void ToggleMapZoom()
        {
            if (miniMap == null) return;

            miniMap.ToggleZoom();
            bool zoomed = miniMap.IsZoomed;

            if (headerText != null) headerText.gameObject.SetActive(!zoomed);
            if (routeText != null) routeText.gameObject.SetActive(!zoomed);
            if (progressText != null) progressText.gameObject.SetActive(!zoomed);
            if (waypointText != null) waypointText.gameObject.SetActive(!zoomed);
            if (qualityText != null) qualityText.gameObject.SetActive(!zoomed);
            if (qualityIcon != null) qualityIcon.gameObject.SetActive(!zoomed);
            if (flashText != null) flashText.gameObject.SetActive(!zoomed);
            if (guidanceText != null) guidanceText.gameObject.SetActive(!zoomed);
            if (slamStatusText != null) slamStatusText.gameObject.SetActive(!zoomed);
        }

        /// <summary>매핑 모드 퇴장 시 호출. 패널 비활성화.</summary>
        public void Hide()
        {
            if (overlayPanel != null) overlayPanel.SetActive(false);
            miniMap?.Hide();
        }

        private void RefreshLocalization(Language lang)
        {
            if (headerText != null)
                headerText.text = LocalizationManager.Get("overlay.header");
            if (waypointText != null && waypointText.color == COLOR_HINT)
                waypointText.text = LocalizationManager.Get("overlay.select_wp");
            UpdateQualityDisplay();
            UpdateProgress();
        }
    }
}
