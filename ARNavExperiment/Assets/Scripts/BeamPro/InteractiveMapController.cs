using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavExperiment.Logging;
using ARNavExperiment.Mission;

namespace ARNavExperiment.BeamPro
{
    public class InteractiveMapController : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private RectTransform mapContainer;
        [SerializeField] private Image floorPlanImage;

        [Header("Markers")]
        [SerializeField] private RectTransform currentPositionMarker;
        [SerializeField] private RectTransform destinationPin;
        [SerializeField] private GameObject poiMarkerPrefab;

        [Header("Zoom")]
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 3f;
        [SerializeField] private float zoomSpeed = 0.5f;

        [Header("POI Detail")]
        [SerializeField] private POIDetailPanel poiDetailPanel;

        private float currentZoom = 1f;
        private POIData[] currentPOIs;

        public void SetFloorPlan(Sprite floorPlan)
        {
            if (floorPlanImage) floorPlanImage.sprite = floorPlan;
        }

        public void UpdateCurrentPosition(Vector2 mapPosition)
        {
            if (currentPositionMarker)
                currentPositionMarker.anchoredPosition = mapPosition;
        }

        public void SetDestination(Vector2 mapPosition)
        {
            if (destinationPin)
            {
                destinationPin.gameObject.SetActive(true);
                destinationPin.anchoredPosition = mapPosition;
            }
        }

        public void LoadPOIs(POIData[] pois)
        {
            currentPOIs = pois;
            ClearPOIMarkers();

            foreach (var poi in pois)
            {
                if (poiMarkerPrefab == null || mapContainer == null) continue;

                var marker = Instantiate(poiMarkerPrefab, mapContainer);
                var rt = marker.GetComponent<RectTransform>();
                if (rt) rt.anchoredPosition = poi.mapPosition;

                var btn = marker.GetComponent<Button>();
                if (btn != null)
                {
                    var poiRef = poi;
                    btn.onClick.AddListener(() => OnPOIMarkerTapped(poiRef));
                }

                var label = marker.GetComponentInChildren<TextMeshProUGUI>();
                if (label) label.text = poi.displayName;
            }
        }

        private void OnPOIMarkerTapped(POIData poi)
        {
            if (poiDetailPanel) poiDetailPanel.Show(poi);

            EventLogger.Instance?.LogEvent("BEAM_POI_VIEWED",
                beamContentType: "poi_detail",
                extraData: $"{{\"poi_id\":\"{poi.poiId}\",\"poi_type\":\"{poi.poiType}\"}}");
        }

        public void ZoomTo(float level)
        {
            currentZoom = Mathf.Clamp(level, minZoom, maxZoom);
            if (mapContainer)
                mapContainer.localScale = Vector3.one * currentZoom;

            EventLogger.Instance?.LogEvent("BEAM_MAP_ZOOMED",
                beamContentType: "map",
                extraData: $"{{\"zoom_level\":{currentZoom:F1}}}");
        }

        private void Update()
        {
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

        private void ClearPOIMarkers()
        {
            if (mapContainer == null) return;
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i);
                if (child != currentPositionMarker && child != destinationPin)
                    Destroy(child.gameObject);
            }
        }
    }
}
