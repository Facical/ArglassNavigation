using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ARNavExperiment.Core;
using ARNavExperiment.Presentation.Shared;

namespace ARNavExperiment.Presentation.Mapping
{
    /// <summary>
    /// 매핑 모드 글래스 오버레이에 표시되는 도면 기반 미니맵.
    /// 웨이포인트 매핑 상태를 색상 마커로 시각화하고, 현재 위치를 실시간 표시.
    /// </summary>
    public class MappingMiniMap : FloorPlanMapBase
    {
        private static readonly Color COLOR_UNMAPPED = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        private static readonly Color COLOR_MAPPED = new Color(0.3f, 1f, 0.3f, 0.9f);
        private static readonly Color COLOR_SELECTED = new Color(0.3f, 0.7f, 1f, 1f);
        private static readonly Color COLOR_INACTIVE_ROUTE = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        private static readonly Color COLOR_REF_UNMAPPED = new Color(0.5f, 0.3f, 0.7f, 0.5f);
        private static readonly Color COLOR_REF_MAPPED = new Color(0.7f, 0.4f, 1f, 0.9f);

        private const float MARKER_SIZE = 8f;
        private const float MARKER_SIZE_SELECTED = 12f;
        private const float REF_MARKER_SIZE = 5f;
        private const float CURRENT_POS_SIZE = 10f;

        private const float ZOOMED_MARKER_SIZE = 20f;
        private const float ZOOMED_MARKER_SIZE_SELECTED = 30f;
        private const float ZOOMED_CURRENT_POS_SIZE = 25f;

        // waypointId → (X, Z) 좌표 (WaypointDataGenerator와 동일)
        private static readonly Dictionary<string, Vector2> waypointMapPositions = new()
        {
            // 실측 기반 좌표 (기둥 간격 9m 기준)
            { "B_WP00", new Vector2(36, 24) },
            { "B_WP01", new Vector2(36, 18) },
            { "B_WP02", new Vector2(36, 33) },
            { "B_WP03", new Vector2(36, 45) },
            { "B_WP04", new Vector2(36, 57) },
            { "B_WP05", new Vector2(36, 66) },
            { "B_WP06", new Vector2(39, 72) },
            { "B_WP07", new Vector2(36, 48) },
            { "B_WP08", new Vector2(36, -7) },
        };

        private readonly Dictionary<string, RectTransform> markerRects = new();
        private readonly Dictionary<string, Image> markerImages = new();
        private string currentRoute;
        private string selectedWaypointId;
        private readonly HashSet<string> mappedWaypoints = new();

        // SLAM→FloorPlan 자동 보정
        private readonly List<Vector2> calibSlamPoints = new();
        private readonly List<Vector2> calibFloorPoints = new();
        private float calibCos = 1f, calibSin = 0f;
        private Vector2 calibOffset = Vector2.zero;
        private bool isCalibrated;

        private bool isZoomed;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private float activeMarkerSize = MARKER_SIZE;
        private float activeMarkerSizeSelected = MARKER_SIZE_SELECTED;
        private float activeCurrentPosSize = CURRENT_POS_SIZE;

        public bool IsZoomed => isZoomed;

        protected override float GetCurrentPosSize() => activeCurrentPosSize;

        private void Update()
        {
            if (!isVisible) return;
            UpdateCurrentPosition();
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

        /// <summary>SLAM 보정 적용 후 좌표 변환.</summary>
        protected override Vector2 TransformWorldPosition(Vector2 worldXZ)
        {
            return isCalibrated ? SlamToFloorPlan(worldXZ) : worldXZ;
        }

        /// <summary>마커를 초기화하고 패널을 표시한다.</summary>
        public void Initialize()
        {
            if (mapPanel != null)
            {
                var panelRect = mapPanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    originalAnchorMin = panelRect.anchorMin;
                    originalAnchorMax = panelRect.anchorMax;
                }
            }

            isZoomed = false;
            activeMarkerSize = MARKER_SIZE;
            activeMarkerSizeSelected = MARKER_SIZE_SELECTED;
            activeCurrentPosSize = CURRENT_POS_SIZE;

            ClearMarkers();
            CreateMarkers();
            CreateReferenceMarkers();
            CreateCurrentPositionMarker();
        }

        /// <summary>현재 경로를 설정하고 마커 색상을 갱신한다.</summary>
        public void SetRoute(string routeId)
        {
            currentRoute = routeId;
            UpdateAllMarkerColors();
        }

        /// <summary>웨이포인트 선택 시 해당 마커를 하이라이트한다.</summary>
        public void SetSelectedWaypoint(string waypointId)
        {
            // 이전 선택 복원
            if (!string.IsNullOrEmpty(selectedWaypointId) && markerRects.ContainsKey(selectedWaypointId))
            {
                markerRects[selectedWaypointId].sizeDelta = new Vector2(activeMarkerSize, activeMarkerSize);
                UpdateMarkerColor(selectedWaypointId);
            }

            selectedWaypointId = waypointId;

            // 새 선택 하이라이트
            if (!string.IsNullOrEmpty(waypointId) && markerRects.ContainsKey(waypointId))
            {
                markerRects[waypointId].sizeDelta = new Vector2(activeMarkerSizeSelected, activeMarkerSizeSelected);
                markerImages[waypointId].color = COLOR_SELECTED;
            }
        }

        /// <summary>앵커 저장 성공 시 즉시 마커를 초록색으로 변경한다.</summary>
        public void MarkAsMapped(string waypointId)
        {
            mappedWaypoints.Add(waypointId);
            if (markerImages.ContainsKey(waypointId) && waypointId != selectedWaypointId)
                markerImages[waypointId].color = COLOR_MAPPED;
        }

        /// <summary>0.5초 주기로 SpatialAnchorManager에서 매핑 상태를 갱신한다.</summary>
        public void UpdateMappingStatus(string routeId)
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;

            var mappings = anchorMgr.GetRouteMappings(routeId);
            foreach (var mapping in mappings)
            {
                if (!mappedWaypoints.Contains(mapping.waypointId))
                {
                    mappedWaypoints.Add(mapping.waypointId);
                    UpdateMarkerColor(mapping.waypointId);
                }
            }
        }

        public override void Show()
        {
            base.Show();
        }

        public void ToggleZoom()
        {
            if (mapPanel == null) return;
            var panelRect = mapPanel.GetComponent<RectTransform>();
            if (panelRect == null) return;

            isZoomed = !isZoomed;

            if (isZoomed)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                activeMarkerSize = ZOOMED_MARKER_SIZE;
                activeMarkerSizeSelected = ZOOMED_MARKER_SIZE_SELECTED;
                activeCurrentPosSize = ZOOMED_CURRENT_POS_SIZE;
            }
            else
            {
                panelRect.anchorMin = originalAnchorMin;
                panelRect.anchorMax = originalAnchorMax;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                activeMarkerSize = MARKER_SIZE;
                activeMarkerSizeSelected = MARKER_SIZE_SELECTED;
                activeCurrentPosSize = CURRENT_POS_SIZE;
            }

            RescaleMarkers();
        }

        public override void Hide()
        {
            base.Hide();
        }

        private readonly Dictionary<string, Image> refMarkerImages = new();

        private void CreateMarkers()
        {
            if (markerContainer == null) return;

            foreach (var kvp in waypointMapPositions)
            {
                var markerGO = new GameObject(kvp.Key);
                var rect = markerGO.AddComponent<RectTransform>();
                rect.SetParent(markerContainer, false);
                rect.sizeDelta = new Vector2(activeMarkerSize, activeMarkerSize);

                PlaceMarkerAtWorld(rect, kvp.Value);

                var img = markerGO.AddComponent<Image>();
                img.color = COLOR_UNMAPPED;

                markerRects[kvp.Key] = rect;
                markerImages[kvp.Key] = img;
            }
        }

        private void CreateReferenceMarkers()
        {
            if (markerContainer == null) return;
            refMarkerImages.Clear();

            var anchorMgr = Core.SpatialAnchorManager.Instance;
            var mappedRefs = new HashSet<string>();
            if (anchorMgr != null)
            {
                foreach (var r in anchorMgr.GetReferenceMappings())
                    mappedRefs.Add(r.roomId);
            }

            foreach (var pt in Navigation.ReferencePointRegistry.AllPoints)
            {
                var markerGO = new GameObject(pt.RoomId);
                var rect = markerGO.AddComponent<RectTransform>();
                rect.SetParent(markerContainer, false);
                rect.sizeDelta = new Vector2(REF_MARKER_SIZE, REF_MARKER_SIZE);
                PlaceMarkerAtWorld(rect, pt.FloorPlanXZ);

                var img = markerGO.AddComponent<Image>();
                img.color = mappedRefs.Contains(pt.RoomId) ? COLOR_REF_MAPPED : COLOR_REF_UNMAPPED;
                refMarkerImages[pt.RoomId] = img;
            }
        }

        /// <summary>보정 앵커 매핑 상태 갱신</summary>
        public void UpdateReferenceMappingStatus()
        {
            var anchorMgr = Core.SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;

            var mappedRefs = new HashSet<string>();
            foreach (var r in anchorMgr.GetReferenceMappings())
                mappedRefs.Add(r.roomId);

            foreach (var kvp in refMarkerImages)
                kvp.Value.color = mappedRefs.Contains(kvp.Key) ? COLOR_REF_MAPPED : COLOR_REF_UNMAPPED;
        }

        private void RescaleMarkers()
        {
            foreach (var kvp in markerRects)
            {
                float size = (kvp.Key == selectedWaypointId) ? activeMarkerSizeSelected : activeMarkerSize;
                kvp.Value.sizeDelta = new Vector2(size, size);
            }

            if (currentPositionMarker != null)
                currentPositionMarker.sizeDelta = new Vector2(activeCurrentPosSize, activeCurrentPosSize);
        }

        private void ClearMarkers()
        {
            if (markerContainer != null)
            {
                for (int i = markerContainer.childCount - 1; i >= 0; i--)
                    Destroy(markerContainer.GetChild(i).gameObject);
            }
            markerRects.Clear();
            markerImages.Clear();
            mappedWaypoints.Clear();
            selectedWaypointId = null;
            currentPositionMarker = null;
            calibSlamPoints.Clear();
            calibFloorPoints.Clear();
            calibCos = 1f;
            calibSin = 0f;
            calibOffset = Vector2.zero;
            isCalibrated = false;
        }

        // =====================================================
        // SLAM→FloorPlan 자동 보정
        // =====================================================

        /// <summary>
        /// 앵커 생성 시 호출. SLAM 위치와 도면 위치 쌍을 기록하고, 2개 이상이면 보정을 계산한다.
        /// </summary>
        public void AddCalibrationPoint(string waypointId, Vector3 slamPosition)
        {
            Vector2 floorPlanPos;
            if (waypointMapPositions.TryGetValue(waypointId, out floorPlanPos))
            {
                // WP 좌표에서 찾음
            }
            else
            {
                // Reference 앵커에서 찾기
                var refPos = Navigation.ReferencePointRegistry.GetFloorPlanPosition(waypointId);
                if (!refPos.HasValue) return;
                floorPlanPos = refPos.Value;
            }

            var slamXZ = new Vector2(slamPosition.x, slamPosition.z);

            // 중복 방지 (같은 웨이포인트 재매핑 시 이전 데이터 교체)
            for (int i = calibSlamPoints.Count - 1; i >= 0; i--)
            {
                if (calibFloorPoints[i] == floorPlanPos)
                {
                    calibSlamPoints.RemoveAt(i);
                    calibFloorPoints.RemoveAt(i);
                }
            }

            calibSlamPoints.Add(slamXZ);
            calibFloorPoints.Add(floorPlanPos);

            Debug.Log($"[MappingMiniMap] Calibration point added: {waypointId} SLAM({slamXZ}) → Floor({floorPlanPos}), total={calibSlamPoints.Count}");

            if (calibSlamPoints.Count >= 2)
                ComputeCalibration();
        }

        /// <summary>
        /// 2D rigid body 변환 (회전 + 평행이동)을 최소자승법으로 계산한다.
        /// </summary>
        private void ComputeCalibration()
        {
            int n = calibSlamPoints.Count;

            // 중심점 계산
            Vector2 centroidS = Vector2.zero, centroidF = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                centroidS += calibSlamPoints[i];
                centroidF += calibFloorPoints[i];
            }
            centroidS /= n;
            centroidF /= n;

            // 중심점 기준 정규화 후 회전각 계산
            float sumCross = 0f, sumDot = 0f;
            for (int i = 0; i < n; i++)
            {
                var ds = calibSlamPoints[i] - centroidS;
                var df = calibFloorPoints[i] - centroidF;
                sumCross += ds.x * df.y - ds.y * df.x;
                sumDot += ds.x * df.x + ds.y * df.y;
            }

            float theta = Mathf.Atan2(sumCross, sumDot);
            calibCos = Mathf.Cos(theta);
            calibSin = Mathf.Sin(theta);

            // 평행이동: offset = centroidF - Rotate(centroidS)
            var rotatedCentroidS = new Vector2(
                centroidS.x * calibCos - centroidS.y * calibSin,
                centroidS.x * calibSin + centroidS.y * calibCos);
            calibOffset = centroidF - rotatedCentroidS;

            isCalibrated = true;
            Debug.Log($"[MappingMiniMap] Calibration computed: rotation={theta * Mathf.Rad2Deg:F1}°, offset={calibOffset}");
        }

        /// <summary>SLAM 좌표를 도면 좌표로 변환한다.</summary>
        private Vector2 SlamToFloorPlan(Vector2 slamXZ)
        {
            float rx = slamXZ.x * calibCos - slamXZ.y * calibSin;
            float ry = slamXZ.x * calibSin + slamXZ.y * calibCos;
            return new Vector2(rx + calibOffset.x, ry + calibOffset.y);
        }

        private void UpdateAllMarkerColors()
        {
            foreach (var kvp in markerImages)
                UpdateMarkerColor(kvp.Key);
        }

        private void UpdateMarkerColor(string waypointId)
        {
            if (!markerImages.ContainsKey(waypointId)) return;
            if (waypointId == selectedWaypointId)
            {
                markerImages[waypointId].color = COLOR_SELECTED;
                return;
            }

            bool isCurrentRoute = !string.IsNullOrEmpty(currentRoute) &&
                                  waypointId.StartsWith(currentRoute + "_");
            if (!isCurrentRoute)
            {
                markerImages[waypointId].color = COLOR_INACTIVE_ROUTE;
                return;
            }

            markerImages[waypointId].color = mappedWaypoints.Contains(waypointId)
                ? COLOR_MAPPED
                : COLOR_UNMAPPED;
        }
    }
}
