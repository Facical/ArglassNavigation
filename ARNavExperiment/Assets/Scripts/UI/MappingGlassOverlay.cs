using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.UI
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
            }

            if (flashText != null)
            {
                flashText.alpha = 0f;
                flashText.text = "";
            }

            if (waypointText != null)
            {
                waypointText.text = "웨이포인트를 선택하세요";
                waypointText.color = COLOR_HINT;
            }
        }

        private void OnDisable()
        {
            if (mappingModeUI != null)
            {
                mappingModeUI.OnWaypointSelectedEvent -= HandleWaypointSelected;
                mappingModeUI.OnAnchorCreateResult -= HandleAnchorResult;
            }
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
        }

        private void HandleAnchorResult(string waypointId, bool success)
        {
            if (flashText == null) return;

            if (success)
            {
                flashText.text = "\u2713 앵커 생성 완료!";
                flashText.color = COLOR_FLASH_SUCCESS;
            }
            else
            {
                flashText.text = "\u2717 생성 실패";
                flashText.color = COLOR_FLASH_FAIL;
            }

            flashText.alpha = 1f;
            flashTimer = FLASH_DURATION;
        }

        private void UpdateQualityDisplay()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;

            int quality = anchorMgr.GetCurrentAnchorQuality();
            string[] qualityNames = { "부족", "보통", "좋음" };
            Color[] qualityColors = { COLOR_QUALITY_BAD, COLOR_QUALITY_OK, COLOR_QUALITY_GOOD };

            if (qualityText != null)
            {
                qualityText.text = quality == 0
                    ? "품질: 부족 — 주변을 둘러보세요"
                    : $"품질: {qualityNames[quality]}";
                qualityText.color = qualityColors[quality];
            }

            if (qualityIcon != null)
                qualityIcon.color = qualityColors[quality];
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

            progressText.text = $"{mapped}/{total} 완료";
        }

        /// <summary>매핑 모드 진입 시 호출. 패널 활성화 + 경로 표시.</summary>
        public void Show(string routeId)
        {
            if (overlayPanel != null) overlayPanel.SetActive(true);
            if (headerText != null) headerText.text = "매핑 모드";
            if (routeText != null) routeText.text = $"Route {routeId}";
            if (waypointText != null)
            {
                waypointText.text = "웨이포인트를 선택하세요";
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
                }
            }
        }

        /// <summary>매핑 모드 퇴장 시 호출. 패널 비활성화.</summary>
        public void Hide()
        {
            if (overlayPanel != null) overlayPanel.SetActive(false);
        }
    }
}
