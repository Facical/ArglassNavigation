using UnityEngine;
using UnityEngine.UI;

namespace ARNavExperiment.Presentation.Shared
{
    /// <summary>
    /// 도면 기반 맵의 공통 부모 클래스.
    /// 좌표 변환, 현재 위치 마커, 표시/숨김 등 공통 로직 제공.
    /// MappingMiniMap(매핑 모드)과 InteractiveMapController(실험 모드)가 상속.
    /// </summary>
    public abstract class FloorPlanMapBase : MonoBehaviour
    {
        [SerializeField] protected GameObject mapPanel;
        [SerializeField] protected Image floorPlanImage;
        [SerializeField] protected RectTransform markerContainer;

        [Header("Coordinate Mapping")]
        [Tooltip("도면 왼쪽 하단에 해당하는 월드 좌표 (X, Z)")]
        [SerializeField] protected Vector2 worldMin = new Vector2(-30, -9);
        [Tooltip("도면 오른쪽 상단에 해당하는 월드 좌표 (X, Z)")]
        [SerializeField] protected Vector2 worldMax = new Vector2(29, 29);

        [Header("Current Position")]
        [SerializeField] protected RectTransform currentPositionMarker;

        protected bool isVisible;

        /// <summary>월드 좌표(X, Z)를 0~1 정규화 좌표로 변환한다.</summary>
        protected Vector2 WorldToNormalized(Vector2 worldXZ)
        {
            float tx = (worldXZ.x - worldMin.x) / (worldMax.x - worldMin.x);
            float ty = (worldXZ.y - worldMin.y) / (worldMax.y - worldMin.y);
            return new Vector2(tx, ty);
        }

        /// <summary>Camera.main 기반 실시간 위치 추적. 서브클래스에서 override 가능.</summary>
        protected virtual void UpdateCurrentPosition()
        {
            if (currentPositionMarker == null || Camera.main == null) return;

            var camPos = Camera.main.transform.position;
            var worldXZ = new Vector2(camPos.x, camPos.z);
            var normalized = WorldToNormalized(TransformWorldPosition(worldXZ));

            currentPositionMarker.anchorMin = normalized;
            currentPositionMarker.anchorMax = normalized;
            currentPositionMarker.anchoredPosition = Vector2.zero;

            bool inBounds = normalized.x >= 0f && normalized.x <= 1f &&
                            normalized.y >= 0f && normalized.y <= 1f;
            currentPositionMarker.gameObject.SetActive(inBounds);
        }

        /// <summary>
        /// 월드 좌표에 추가 변환을 적용할 때 override.
        /// 기본 구현은 패스스루 (변환 없음).
        /// MappingMiniMap은 SLAM 보정을 적용.
        /// </summary>
        protected virtual Vector2 TransformWorldPosition(Vector2 worldXZ)
        {
            return worldXZ;
        }

        /// <summary>현재 위치 마커를 프로시저럴 생성한다.</summary>
        protected void CreateCurrentPositionMarker()
        {
            if (markerContainer == null) return;

            if (currentPositionMarker != null)
            {
                currentPositionMarker.SetParent(markerContainer, false);
                currentPositionMarker.SetAsLastSibling();
                return;
            }

            var go = new GameObject("CurrentPosition");
            currentPositionMarker = go.AddComponent<RectTransform>();
            currentPositionMarker.SetParent(markerContainer, false);
            float size = GetCurrentPosSize();
            currentPositionMarker.sizeDelta = new Vector2(size, size);
            currentPositionMarker.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = GetCurrentPosColor();

            currentPositionMarker.SetAsLastSibling();
        }

        /// <summary>RectTransform을 정규화 좌표 anchor로 배치한다.</summary>
        protected void PlaceMarkerAtWorld(RectTransform marker, Vector2 worldXZ)
        {
            var normalized = WorldToNormalized(worldXZ);
            marker.anchorMin = normalized;
            marker.anchorMax = normalized;
            marker.anchoredPosition = Vector2.zero;
        }

        public virtual void Show()
        {
            if (mapPanel != null) mapPanel.SetActive(true);
            isVisible = true;
        }

        public virtual void Hide()
        {
            if (mapPanel != null) mapPanel.SetActive(false);
            isVisible = false;
        }

        /// <summary>현재 위치 마커 색상. 서브클래스에서 override 가능.</summary>
        protected virtual Color GetCurrentPosColor()
        {
            return new Color(1f, 0.35f, 0.2f, 1f); // 주황-빨강
        }

        /// <summary>현재 위치 마커 크기. 서브클래스에서 override 가능.</summary>
        protected virtual float GetCurrentPosSize()
        {
            return 10f;
        }
    }
}
