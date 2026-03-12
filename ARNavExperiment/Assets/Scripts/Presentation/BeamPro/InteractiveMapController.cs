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
        [SerializeField] private float maxZoom = 3f;
        [SerializeField] private float zoomSpeed = 0.5f;

        [Header("POI Detail")]
        [SerializeField] private POIDetailPanel poiDetailPanel;
        [SerializeField] private GameObject poiMarkerPrefab;

        private static readonly Color COLOR_POI = new Color(0.2f, 0.85f, 0.4f, 0.9f);
        private static readonly Color COLOR_DESTINATION = new Color(1f, 0.2f, 0.2f, 1f);
        private const float POI_MARKER_SIZE = 16f;
        private const float POI_LABEL_FONT_SIZE = 11f;

        private float currentZoom = 1f;
        private bool isTrackingPosition;
        private readonly List<GameObject> poiMarkerInstances = new();

        // SLAM→FloorPlan 보정 (다중 앵커 최소자승법)
        private bool isCalibrated;
        private float calibCos = 1f, calibSin = 0f;
        private Vector2 calibOffset = Vector2.zero;
        private int lastCalibAnchorCount;

        public void SetFloorPlan(Sprite floorPlan)
        {
            if (floorPlanImage) floorPlanImage.sprite = floorPlan;
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
            isCalibrated = false;
            Debug.Log($"[InteractiveMap] Calibration invalidated — new anchor: {waypointId}");
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

        /// <summary>N개 앵커 쌍으로 최적 2D rigid transform (회전+평행이동)을 최소자승법으로 계산.</summary>
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

            // 최적 회전각: atan2(Σ cross, Σ dot)
            float sumCross = 0f, sumDot = 0f;
            for (int i = 0; i < n; i++)
            {
                var ds = pairs[i].slamXZ - centroidS;
                var df = pairs[i].fallbackXZ - centroidF;
                sumCross += ds.x * df.y - ds.y * df.x;
                sumDot += ds.x * df.x + ds.y * df.y;
            }

            float theta = Mathf.Atan2(sumCross, sumDot);
            calibCos = Mathf.Cos(theta);
            calibSin = Mathf.Sin(theta);

            // 평행이동: centroidF - Rotate(centroidS)
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
            if (markerContainer)
                markerContainer.localScale = Vector3.one * currentZoom;

            DomainEventBus.Instance?.Publish(new BeamMapZoomed(currentZoom));
        }

        private void Update()
        {
            if (isTrackingPosition)
                UpdateCurrentPosition();

            // WorldSpace(GlassOnly) 모드에서는 터치 줌 비활성화 (MapZoomButtons가 대체)
            var canvasCtrl = GetComponentInParent<BeamProCanvasController>();
            if (canvasCtrl != null && canvasCtrl.IsWorldSpace) return;

            // pinch-to-zoom gesture handling
            if (Input.touchCount == 2)
            {
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
        }
    }
}
