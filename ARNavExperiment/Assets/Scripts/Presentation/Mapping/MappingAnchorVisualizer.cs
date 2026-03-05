using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Presentation.Mapping
{
    /// <summary>
    /// 매핑 완료된 앵커 위치에 3D 마커(구체 + 라벨)를 표시하고
    /// 매핑된 앵커들을 경로별로 분리하여 경로선으로 연결하는 시각화 컴포넌트.
    /// </summary>
    public class MappingAnchorVisualizer : MonoBehaviour
    {
        [SerializeField] private Material markerMaterial;

        private Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
        private Dictionary<string, List<string>> routeMarkerOrders = new Dictionary<string, List<string>>();
        private Dictionary<string, LineRenderer> pathLines = new Dictionary<string, LineRenderer>();
        private Material runtimeMaterial;

        // Route A: 초록 마커, 파란 경로선
        private static readonly Color COLOR_MARKER_A = new Color(0.3f, 1f, 0.3f, 0.8f);
        private static readonly Color COLOR_PATH_A = new Color(0.3f, 0.8f, 1f, 0.5f);
        // Route B: 노란주황 마커, 주황 경로선
        private static readonly Color COLOR_MARKER_B = new Color(1f, 0.7f, 0.2f, 0.8f);
        private static readonly Color COLOR_PATH_B = new Color(1f, 0.5f, 0.1f, 0.5f);

        private const float MARKER_SIZE = 0.15f;
        private const float LABEL_OFFSET_Y = 0.3f;
        private const float PATH_WIDTH = 0.02f;

        private void OnEnable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorSaved += HandleAnchorSaved;

            // 이미 생성된 앵커들 복원
            RefreshFromExistingAnchors();
        }

        private void OnDisable()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr != null)
                anchorMgr.OnAnchorSaved -= HandleAnchorSaved;
        }

        private void HandleAnchorSaved(string waypointId, bool success)
        {
            if (!success) return;

            Vector3 position;

#if UNITY_EDITOR
            // 에디터에서는 SpatialAnchorManager가 anchorTransform을 저장하지 않으므로
            // 카메라 위치를 기반으로 마커 생성
            position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
#else
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;
            var t = anchorMgr.GetAnchorTransform(waypointId);
            if (t == null) return;
            position = t.position;
#endif

            CreateMarker(waypointId, position);
        }

        /// <summary>waypointId에서 경로 ID를 추출합니다. "A_WP01" → "A", "B_WP03" → "B"</summary>
        private string GetRouteId(string waypointId)
        {
            if (string.IsNullOrEmpty(waypointId)) return "unknown";
            int underscoreIdx = waypointId.IndexOf('_');
            if (underscoreIdx > 0)
                return waypointId.Substring(0, underscoreIdx);
            return "unknown";
        }

        private void GetRouteColors(string routeId, out Color markerColor, out Color pathColor)
        {
            if (routeId == "B")
            {
                markerColor = COLOR_MARKER_B;
                pathColor = COLOR_PATH_B;
            }
            else
            {
                markerColor = COLOR_MARKER_A;
                pathColor = COLOR_PATH_A;
            }
        }

        private void CreateMarker(string waypointId, Vector3 position)
        {
            string routeId = GetRouteId(waypointId);

            // 중복 방지
            if (markers.ContainsKey(waypointId))
            {
                Destroy(markers[waypointId]);
                markers.Remove(waypointId);
                if (routeMarkerOrders.TryGetValue(routeId, out var existingList))
                    existingList.Remove(waypointId);
            }

            // 루트 오브젝트
            var root = new GameObject($"AnchorMarker_{waypointId}");
            root.transform.SetParent(transform);
            root.transform.position = position;

            // 구체 마커
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(root.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * MARKER_SIZE;

            // Collider 제거 (상호작용 불필요)
            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 경로별 색상 적용
            GetRouteColors(routeId, out Color markerColor, out _);
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = GetMarkerMaterial();
                renderer.material.color = markerColor;
            }

            // 라벨 (WorldSpace TMP)
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(root.transform);
            labelGO.transform.localPosition = new Vector3(0, LABEL_OFFSET_Y, 0);

            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text = waypointId;
            tmp.fontSize = 2f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            // 라벨이 카메라를 향하도록
            labelGO.AddComponent<LabelBillboard>();

            markers[waypointId] = root;

            // 경로별 마커 순서 관리
            if (!routeMarkerOrders.ContainsKey(routeId))
                routeMarkerOrders[routeId] = new List<string>();
            routeMarkerOrders[routeId].Add(waypointId);

            UpdatePathLine(routeId);
        }

        private Material GetMarkerMaterial()
        {
            if (markerMaterial != null) return new Material(markerMaterial);

            if (runtimeMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard");
                runtimeMaterial = new Material(shader);
                runtimeMaterial.SetFloat("_Surface", 1); // Transparent
                runtimeMaterial.SetFloat("_Blend", 0);
                runtimeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                runtimeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                runtimeMaterial.SetInt("_ZWrite", 0);
                runtimeMaterial.renderQueue = 3000;
            }

            return new Material(runtimeMaterial);
        }

        private void UpdatePathLine(string routeId)
        {
            if (!routeMarkerOrders.TryGetValue(routeId, out var order) || order.Count < 2)
            {
                if (pathLines.TryGetValue(routeId, out var existingLine) && existingLine != null)
                    existingLine.positionCount = 0;
                return;
            }

            if (!pathLines.TryGetValue(routeId, out var line) || line == null)
            {
                // 경로별 자식 GameObject에 LineRenderer 배치
                var lineGO = new GameObject($"PathLine_{routeId}");
                lineGO.transform.SetParent(transform);
                line = lineGO.AddComponent<LineRenderer>();
                line.startWidth = PATH_WIDTH;
                line.endWidth = PATH_WIDTH;
                GetRouteColors(routeId, out _, out Color pathColor);
                line.material = GetMarkerMaterial();
                line.material.color = pathColor;
                line.useWorldSpace = true;
                pathLines[routeId] = line;
            }

            // WP 번호 순으로 정렬하여 연결 (A_WP01 < A_WP02 < ...)
            var sorted = order.OrderBy(id => id).ToList();

            line.positionCount = sorted.Count;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (markers.TryGetValue(sorted[i], out var markerGO))
                    line.SetPosition(i, markerGO.transform.position);
            }
        }

        /// <summary>기존 앵커 매핑에서 마커를 복원합니다.</summary>
        public void RefreshFromExistingAnchors()
        {
            var anchorMgr = SpatialAnchorManager.Instance;
            if (anchorMgr == null) return;

            foreach (string routeId in new[] { "A", "B" })
            {
                var mappings = anchorMgr.GetRouteMappings(routeId);
                foreach (var mapping in mappings)
                {
                    if (markers.ContainsKey(mapping.waypointId)) continue;

                    var t = anchorMgr.GetAnchorTransform(mapping.waypointId);
                    if (t != null)
                        CreateMarker(mapping.waypointId, t.position);
                }
            }
        }

        /// <summary>모든 마커와 경로선을 삭제합니다.</summary>
        public void ClearAllMarkers()
        {
            foreach (var kvp in markers)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            markers.Clear();
            routeMarkerOrders.Clear();

            foreach (var kvp in pathLines)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            pathLines.Clear();
        }
    }

    /// <summary>라벨이 항상 카메라를 향하도록 하는 빌보드 컴포넌트.</summary>
    internal class LabelBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);
        }
    }
}
