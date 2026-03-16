using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
using ARNavExperiment.Navigation;
using ARNavExperiment.Presentation.Shared;
using ARNavExperiment.Domain.Events;
using ARNavExperiment.Application;

namespace ARNavExperiment.Presentation.BeamPro
{
    public class InteractiveMapController : FloorPlanMapBase
    {
        [Header("Destination")]
        [SerializeField] private RectTransform destinationPin;

        [Header("Zoom")]
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 2f;
        [SerializeField] private float zoomSpeed = 0.5f;

        [Header("POI Detail")]
        [SerializeField] private POIDetailPanel poiDetailPanel;
        [SerializeField] private GameObject poiMarkerPrefab;

        private static readonly Color COLOR_POI = new Color(0.2f, 0.85f, 0.4f, 0.9f);
        private static readonly Color COLOR_DESTINATION = new Color(1f, 0.2f, 0.2f, 1f);
        private const float POI_MARKER_SIZE = 16f;
        private const float POI_LABEL_FONT_SIZE = 11f;

        private float currentZoom = 1f;
        public float CurrentZoom => currentZoom;

        private bool isTrackingPosition;
        private readonly List<GameObject> poiMarkerInstances = new();

        // SLAM→FloorPlan 보정 (다중 앵커 최소자승법)
        private bool isCalibrated;
        private float calibCos = 1f, calibSin = 0f;
        private Vector2 calibOffset = Vector2.zero;
        private int lastCalibAnchorCount;

        // ScrollRect 기반 팬/줌
        private ScrollRect scrollRect;
        private Vector2 baseContentSize;
        private bool baseContentSizeReady;

        public void SetFloorPlan(Sprite floorPlan)
        {
            if (floorPlanImage) floorPlanImage.sprite = floorPlan;
            // 도면 설정 시 줌을 1.0(전체 보기)으로 리셋
            ZoomTo(1f);
        }

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();

            // 도면 비율 유지 — AdjustForPreserveAspect()가 마커 좌표 보정 처리
            if (floorPlanImage != null)
                floorPlanImage.preserveAspect = true;
        }

        private void OnEnable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorLateRecovered += OnAnchorLateRecovered;
        }

        private void OnDisable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorLateRecovered -= OnAnchorLateRecovered;
        }

        private void OnAnchorLateRecovered(string waypointId, Transform anchorTransform)
        {
            InvalidateCalibration();
            Debug.Log($"[InteractiveMap] Calibration invalidated — new anchor: {waypointId}");
        }

        /// <summary>보정을 무효화하여 다음 UpdateCurrentPosition 시 재계산을 트리거합니다.</summary>
        public void InvalidateCalibration()
        {
            isCalibrated = false;
        }

        /// <summary>보정 전에는 빨간 점(현재 위치)을 숨긴다.</summary>
        protected override void UpdateCurrentPosition()
        {
            if (!isCalibrated)
            {
                if (currentPositionMarker != null)
                    currentPositionMarker.gameObject.SetActive(false);
                return;
            }
            base.UpdateCurrentPosition();
        }

        public void StartPositionTracking()
        {
            isTrackingPosition = true;
            if (currentPositionMarker == null)
                CreateCurrentPositionMarker();
        }

        public void StopPositionTracking()
        {
            isTrackingPosition = false;
        }

        /// <summary>SLAM 좌표를 도면 좌표로 변환합니다 (다중 앵커 최소자승법).</summary>
        protected override Vector2 TransformWorldPosition(Vector2 worldXZ)
        {
            if (!isCalibrated) TryCalibrate();
            if (!isCalibrated) return worldXZ;

            float rx = worldXZ.x * calibCos - worldXZ.y * calibSin;
            float ry = worldXZ.x * calibSin + worldXZ.y * calibCos;
            return new Vector2(rx + calibOffset.x, ry + calibOffset.y);
        }

        /// <summary>WaypointManager에서 앵커 쌍을 가져와 SLAM→도면 rigid transform 계산.</summary>
        private void TryCalibrate()
        {
            var wm = WaypointManager.Instance;
            if (wm == null) return;

            var pairs = wm.GetAllCalibratedAnchorPairs();
            if (pairs.Count == 0) return;

            if (pairs.Count == 1)
            {
                // 앵커 1개: HeadingCalibrationOffset으로 회전 + 이 점으로 평행이동
                float offsetDeg = wm.HeadingCalibrationOffset;
                float theta = -offsetDeg * Mathf.Deg2Rad;
                calibCos = Mathf.Cos(theta);
                calibSin = Mathf.Sin(theta);

                var (slamXZ, fallbackXZ) = pairs[0];
                float rotatedX = slamXZ.x * calibCos - slamXZ.y * calibSin;
                float rotatedY = slamXZ.x * calibSin + slamXZ.y * calibCos;
                calibOffset = fallbackXZ - new Vector2(rotatedX, rotatedY);
            }
            else
            {
                // 앵커 2개+: 최소자승법으로 최적 회전 + 평행이동
                ComputeLeastSquaresTransform(pairs);
            }

            lastCalibAnchorCount = pairs.Count;
            isCalibrated = true;
            Debug.Log($"[InteractiveMap] Calibrated: cos={calibCos:F3}, sin={calibSin:F3}, " +
                $"offset={calibOffset}, anchors={pairs.Count}");
        }

        /// <summary>N개 앵커 쌍으로 최적 2D similarity transform (회전+스케일+평행이동)을 최소자승법으로 계산.</summary>
        private void ComputeLeastSquaresTransform(List<(Vector2 slamXZ, Vector2 fallbackXZ)> pairs)
        {
            int n = pairs.Count;

            // 중심점
            Vector2 centroidS = Vector2.zero, centroidF = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                centroidS += pairs[i].slamXZ;
                centroidF += pairs[i].fallbackXZ;
            }
            centroidS /= n;
            centroidF /= n;

            float sumCross = 0f, sumDot = 0f, sumSS = 0f;
            for (int i = 0; i < n; i++)
            {
                var ds = pairs[i].slamXZ - centroidS;
                var df = pairs[i].fallbackXZ - centroidF;
                sumCross += ds.x * df.y - ds.y * df.x;
                sumDot += ds.x * df.x + ds.y * df.y;
                sumSS += ds.x * ds.x + ds.y * ds.y;
            }

            if (sumSS < 0.01f)
            {
                // 쌍들이 너무 가깝다 → rigid fallback
                float theta = Mathf.Atan2(sumCross, sumDot);
                calibCos = Mathf.Cos(theta);
                calibSin = Mathf.Sin(theta);
            }
            else
            {
                // Similarity transform: a = sumDot/sumSS, b = sumCross/sumSS
                calibCos = sumDot / sumSS;
                calibSin = sumCross / sumSS;
            }

            // 평행이동: centroidF - Transform(centroidS)
            var rotatedCentroidS = new Vector2(
                centroidS.x * calibCos - centroidS.y * calibSin,
                centroidS.x * calibSin + centroidS.y * calibCos);
            calibOffset = centroidF - rotatedCentroidS;
        }

        /// <summary>월드 좌표로 목적지 핀을 배치한다. SLAM→도면 변환 적용.</summary>
        public void SetDestination(Vector3 worldPos)
        {
            if (destinationPin == null) return;
            destinationPin.gameObject.SetActive(true);
            var worldXZ = new Vector2(worldPos.x, worldPos.z);
            var transformed = TransformWorldPosition(worldXZ);
            var normalized = AdjustForPreserveAspect(WorldToNormalized(transformed));
            destinationPin.anchorMin = normalized;
            destinationPin.anchorMax = normalized;
            destinationPin.anchoredPosition = Vector2.zero;
        }

        /// <summary>도면 좌표(fallback)로 목적지 핀을 배치한다. SLAM 변환 없이 직접 배치.</summary>
        public void SetDestinationFloorPlan(Vector2 floorPlanXZ)
        {
            if (destinationPin == null) return;
            destinationPin.gameObject.SetActive(true);
            var normalized = AdjustForPreserveAspect(WorldToNormalized(floorPlanXZ));
            destinationPin.anchorMin = normalized;
            destinationPin.anchorMax = normalized;
            destinationPin.anchoredPosition = Vector2.zero;
        }

        public void HideDestination()
        {
            if (destinationPin != null)
                destinationPin.gameObject.SetActive(false);
        }

        /// <summary>POI 데이터 배열로 맵에 마커를 배치한다.</summary>
        public void LoadPOIs(POIData[] pois)
        {
            ClearPOIMarkers();
            if (pois == null || markerContainer == null) return;

            foreach (var poi in pois)
            {
                GameObject marker;
                if (poiMarkerPrefab != null)
                {
                    marker = Instantiate(poiMarkerPrefab, markerContainer);
                }
                else
                {
                    marker = CreatePOIMarkerProcedural(poi);
                }

                var rt = marker.GetComponent<RectTransform>();
                if (rt != null)
                    PlaceMarkerAtWorld(rt, poi.mapPosition);

                var btn = marker.GetComponent<Button>();
                if (btn != null)
                {
                    var poiRef = poi;
                    btn.onClick.AddListener(() => OnPOIMarkerTapped(poiRef));
                }

                var label = marker.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = poi.GetDisplayName();

                poiMarkerInstances.Add(marker);
            }
        }

        /// <summary>POI 마커를 프로시저럴 생성한다 (prefab null 대비).</summary>
        private GameObject CreatePOIMarkerProcedural(POIData poi)
        {
            var go = new GameObject($"POI_{poi.poiId}");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(markerContainer, false);
            rect.sizeDelta = new Vector2(POI_MARKER_SIZE, POI_MARKER_SIZE);

            var img = go.AddComponent<Image>();
            img.color = COLOR_POI;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 1f, 0.5f, 1f);
            colors.pressedColor = new Color(0.15f, 0.7f, 0.3f, 1f);
            btn.colors = colors;

            // 라벨
            var labelGO = new GameObject("Label");
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchoredPosition = new Vector2(0, -POI_MARKER_SIZE);
            labelRect.sizeDelta = new Vector2(80, 16);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = poi.GetDisplayName();
            tmp.fontSize = POI_LABEL_FONT_SIZE;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return go;
        }

        public void ClearPOIMarkers()
        {
            foreach (var marker in poiMarkerInstances)
            {
                if (marker != null) Destroy(marker);
            }
            poiMarkerInstances.Clear();
        }

        private void OnPOIMarkerTapped(POIData poi)
        {
            if (poiDetailPanel) poiDetailPanel.Show(poi);

            DomainEventBus.Instance?.Publish(new BeamPOIViewed(poi.poiId));
        }

        public void ZoomTo(float level)
        {
            currentZoom = Mathf.Clamp(level, minZoom, maxZoom);
            ApplyZoomSize();
            DomainEventBus.Instance?.Publish(new BeamMapZoomed(currentZoom));
        }

        /// <summary>줌 레벨에 맞게 MapContainer sizeDelta를 조정. ScrollRect가 팬 범위를 자동 관리.</summary>
        private void ApplyZoomSize()
        {
            if (!markerContainer || !baseContentSizeReady) return;

            Vector2 savedNormPos = (scrollRect != null)
                ? scrollRect.normalizedPosition
                : new Vector2(0.5f, 0.5f);

            markerContainer.sizeDelta = baseContentSize * currentZoom;

            Canvas.ForceUpdateCanvases();

            if (scrollRect != null)
                scrollRect.normalizedPosition = savedNormPos;
        }

        /// <summary>플레이어와 목적지를 모두 포함하는 줌 레벨을 자동 계산하여 적용.</summary>
        public void AutoZoomToFit(Vector2 playerFloorPlanXZ, Vector2 destFloorPlanXZ)
        {
            float span = Mathf.Max(
                Mathf.Abs(destFloorPlanXZ.x - playerFloorPlanXZ.x),
                Mathf.Abs(destFloorPlanXZ.y - playerFloorPlanXZ.y));

            float maxWorldSpan = Mathf.Max(worldMax.x - worldMin.x, worldMax.y - worldMin.y);
            float viewportFraction = span / Mathf.Max(maxWorldSpan, 1f);

            // GlassOnly(WorldSpace)에서는 글래스 화면이 작으므로 maxZoom 1.5 제한
            var canvasCtrl = GetComponentInParent<BeamProCanvasController>();
            bool isGlass = canvasCtrl != null && canvasCtrl.IsWorldSpace;
            float effectiveMax = isGlass ? Mathf.Min(maxZoom, 1.5f) : maxZoom;

            float targetZoom = Mathf.Clamp(0.6f / Mathf.Max(viewportFraction, 0.01f), minZoom, effectiveMax);
            ZoomTo(targetZoom);

            // 줌 후 목적지 중심으로 스크롤 위치 이동
            ScrollToFloorPlanPosition(destFloorPlanXZ);

            Debug.Log($"[InteractiveMap] AutoZoom span={span:F1}m → zoom={targetZoom:F2} (glass={isGlass})");
        }

        /// <summary>도면 좌표를 중심으로 스크롤 위치를 이동합니다.</summary>
        private void ScrollToFloorPlanPosition(Vector2 floorPlanXZ)
        {
            if (scrollRect == null) return;
            var normalized = WorldToNormalized(floorPlanXZ);
            // normalizedPosition은 (0,0)=좌하단, (1,1)=우상단
            // 보정: 뷰포트 중앙에 타겟이 오도록
            scrollRect.normalizedPosition = new Vector2(
                Mathf.Clamp01(normalized.x),
                Mathf.Clamp01(normalized.y));
        }

        private void Update()
        {
            // 뷰포트 크기 캐싱 (레이아웃 완료 후 또는 화면 회전 시 갱신)
            if (scrollRect != null && scrollRect.viewport != null)
            {
                var vpSize = scrollRect.viewport.rect.size;
                if (vpSize.x > 0 && vpSize.y > 0)
                {
                    if (!baseContentSizeReady)
                    {
                        baseContentSize = vpSize;
                        baseContentSizeReady = true;
                        ApplyZoomSize();
                    }
                    else if (Mathf.Abs(vpSize.x - baseContentSize.x) > 1f ||
                             Mathf.Abs(vpSize.y - baseContentSize.y) > 1f)
                    {
                        baseContentSize = vpSize;
                        ApplyZoomSize();
                    }
                }
            }

            if (isTrackingPosition)
                UpdateCurrentPosition();

            // WorldSpace(GlassOnly) 모드에서는 터치 줌/팬 비활성화 (MapZoomButtons가 대체)
            var canvasCtrl = GetComponentInParent<BeamProCanvasController>();
            if (canvasCtrl != null && canvasCtrl.IsWorldSpace) return;

            // pinch-to-zoom gesture handling
            if (Input.touchCount == 2)
            {
                // 핀치 줌 중에는 ScrollRect 드래그를 일시 비활성화
                if (scrollRect != null) scrollRect.enabled = false;

                var t0 = Input.GetTouch(0);
                var t1 = Input.GetTouch(1);

                float prevDist = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
                float curDist = (t0.position - t1.position).magnitude;

                if (prevDist > 0)
                {
                    float delta = (curDist - prevDist) * zoomSpeed * 0.01f;
                    ZoomTo(currentZoom + delta);
                }
            }
            else
            {
                // 핀치 종료 → ScrollRect 재활성화
                if (scrollRect != null && !scrollRect.enabled)
                    scrollRect.enabled = true;
            }
        }
    }
}
