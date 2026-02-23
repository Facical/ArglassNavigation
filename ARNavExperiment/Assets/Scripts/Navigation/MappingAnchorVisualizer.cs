using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ARNavExperiment.Core;

namespace ARNavExperiment.Navigation
{
    /// <summary>
    /// 매핑 완료된 앵커 위치에 3D 마커(구체 + 라벨)를 표시하고
    /// 매핑된 앵커들을 경로선으로 연결하는 시각화 컴포넌트.
    /// </summary>
    public class MappingAnchorVisualizer : MonoBehaviour
    {
        [SerializeField] private Material markerMaterial;

        private Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
        private List<string> markerOrder = new List<string>();
        private LineRenderer pathLine;
        private Material runtimeMaterial;

        private static readonly Color COLOR_MARKER = new Color(0.3f, 1f, 0.3f, 0.8f);
        private static readonly Color COLOR_PATH = new Color(0.3f, 0.8f, 1f, 0.5f);
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

        private void CreateMarker(string waypointId, Vector3 position)
        {
            // 중복 방지
            if (markers.ContainsKey(waypointId))
            {
                Destroy(markers[waypointId]);
                markers.Remove(waypointId);
                markerOrder.Remove(waypointId);
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

            // 머터리얼 적용
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = GetMarkerMaterial();
                renderer.material.color = COLOR_MARKER;
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
            markerOrder.Add(waypointId);

            UpdatePathLine();
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

        private void UpdatePathLine()
        {
            if (markers.Count < 2)
            {
                if (pathLine != null) pathLine.positionCount = 0;
                return;
            }

            if (pathLine == null)
            {
                pathLine = gameObject.AddComponent<LineRenderer>();
                pathLine.startWidth = PATH_WIDTH;
                pathLine.endWidth = PATH_WIDTH;
                pathLine.material = GetMarkerMaterial();
                pathLine.material.color = COLOR_PATH;
                pathLine.useWorldSpace = true;
            }

            pathLine.positionCount = markerOrder.Count;
            for (int i = 0; i < markerOrder.Count; i++)
            {
                if (markers.TryGetValue(markerOrder[i], out var markerGO))
                    pathLine.SetPosition(i, markerGO.transform.position);
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
            markerOrder.Clear();

            if (pathLine != null)
                pathLine.positionCount = 0;
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
