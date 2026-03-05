using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Core;
using ARNavExperiment.Mission;
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

        public void SetFloorPlan(Sprite floorPlan)
        {
            if (floorPlanImage) floorPlanImage.sprite = floorPlan;
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

        /// <summary>월드 좌표로 목적지 핀을 배치한다.</summary>
        public void SetDestination(Vector3 worldPos)
        {
            if (destinationPin == null) return;
            destinationPin.gameObject.SetActive(true);
            var worldXZ = new Vector2(worldPos.x, worldPos.z);
            PlaceMarkerAtWorld(destinationPin, worldXZ);
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
