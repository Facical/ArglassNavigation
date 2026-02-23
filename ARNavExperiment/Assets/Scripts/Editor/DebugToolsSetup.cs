using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;
using ARNavExperiment.DebugTools;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.EditorTools
{
    public class DebugToolsSetup
    {
        [MenuItem("ARNav/Setup Editor Test Mode")]
        public static void SetupTestMode()
        {
            int added = 0;

            // 1. Ensure EventSystem exists (UI 클릭에 필수)
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                added++;
                Debug.Log("[DebugSetup] EventSystem 생성");
            }

            // 2. Add EditorPlayerController to Main Camera
            var cam = Camera.main;
            if (cam != null)
            {
                if (cam.GetComponent<EditorPlayerController>() == null)
                {
                    Undo.AddComponent<EditorPlayerController>(cam.gameObject);
                    added++;
                    Debug.Log("[DebugSetup] EditorPlayerController → Main Camera");
                }
            }
            else
            {
                Debug.LogWarning("[DebugSetup] Main Camera를 찾을 수 없습니다.");
            }

            // 3. Add WaypointGizmoDrawer to WaypointManager
            var wpMgr = Object.FindObjectOfType<WaypointManager>();
            if (wpMgr != null)
            {
                if (wpMgr.GetComponent<WaypointGizmoDrawer>() == null)
                {
                    Undo.AddComponent<WaypointGizmoDrawer>(wpMgr.gameObject);
                    added++;
                    Debug.Log("[DebugSetup] WaypointGizmoDrawer → WaypointManager");
                }
            }

            // 4. Create test floor and waypoint markers
            if (GameObject.Find("--- Test Environment ---") == null)
            {
                var envRoot = new GameObject("--- Test Environment ---");
                Undo.RegisterCreatedObjectUndo(envRoot, "Create Test Environment");

                // Floor plane (50m x 50m)
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "TestFloor";
                floor.transform.SetParent(envRoot.transform);
                floor.transform.localScale = new Vector3(5f, 1f, 5f);
                floor.transform.position = Vector3.zero;
                var floorMat = new Material(Shader.Find("Standard"));
                floorMat.color = new Color(0.15f, 0.15f, 0.2f);
                floor.GetComponent<Renderer>().material = floorMat;

                // Grid lines on floor
                var grid = GameObject.CreatePrimitive(PrimitiveType.Plane);
                grid.name = "GridOverlay";
                grid.transform.SetParent(envRoot.transform);
                grid.transform.localScale = new Vector3(5f, 1f, 5f);
                grid.transform.position = new Vector3(0, 0.01f, 0);
                var gridMat = new Material(Shader.Find("Standard"));
                gridMat.color = new Color(0.2f, 0.2f, 0.3f, 0.5f);
                grid.GetComponent<Renderer>().material = gridMat;

                // Waypoint marker cubes for visual reference
                CreateWPMarker(envRoot.transform, "Start (계단)", Vector3.zero, Color.red);
                // Route A markers
                CreateWPMarker(envRoot.transform, "A-WP01", new Vector3(-7, 0, 0), new Color(0.2f, 0.6f, 1f));
                CreateWPMarker(envRoot.transform, "A-WP03 SW", new Vector3(-20, 0, 0), new Color(0.2f, 0.6f, 1f));
                CreateWPMarker(envRoot.transform, "A-WP06 NW", new Vector3(-20, 0, 25), new Color(0.2f, 0.6f, 1f));
                CreateWPMarker(envRoot.transform, "A-WP08", new Vector3(-3, 0, 25), new Color(0.2f, 0.6f, 1f));
                // Route B markers
                CreateWPMarker(envRoot.transform, "B-WP01", new Vector3(7, 0, 0), new Color(0.9f, 0.5f, 0.1f));
                CreateWPMarker(envRoot.transform, "B-WP03 SE", new Vector3(20, 0, 8), new Color(0.9f, 0.5f, 0.1f));
                CreateWPMarker(envRoot.transform, "B-WP06 NE", new Vector3(20, 0, 25), new Color(0.9f, 0.5f, 0.1f));

                added++;
                Debug.Log("[DebugSetup] 테스트 환경 생성 (바닥 + 웨이포인트 마커)");
            }

            // 5. Fix camera for editor testing
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
                EditorUtility.SetDirty(cam);
            }

            // 6. Fix ExperimentHUD (Glass): layout + position at top-left
            var hud = GameObject.Find("ExperimentHUD");
            if (hud != null)
            {
                var hudRect = hud.GetComponent<RectTransform>();
                if (hudRect != null)
                {
                    hudRect.anchorMin = new Vector2(0, 1);
                    hudRect.anchorMax = new Vector2(0.3f, 1);
                    hudRect.pivot = new Vector2(0, 1);
                    hudRect.anchoredPosition = new Vector2(5, -5);
                    hudRect.sizeDelta = new Vector2(0, 100);
                }

                if (hud.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() == null)
                {
                    var vlg = Undo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(hud);
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 2;
                    vlg.padding = new RectOffset(5, 5, 5, 5);

                    var csf = Undo.AddComponent<UnityEngine.UI.ContentSizeFitter>(hud);
                    csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            // 7. Disable hand ray interactors in editor (마우스 클릭 사용)
            var rightHandRay = GameObject.Find("Right Hand Ray");
            if (rightHandRay != null) rightHandRay.SetActive(false);
            var leftHandRay = GameObject.Find("Left Hand Ray");
            if (leftHandRay != null) leftHandRay.SetActive(false);

            EditorUtility.DisplayDialog("완료",
                $"에디터 테스트 모드 구성 완료!\n\n" +
                $"추가된 컴포넌트: {added}개\n\n" +
                "Play 모드에서:\n" +
                "- WASD: 이동\n" +
                "- 우클릭 드래그: 시점 회전\n" +
                "- Shift: 달리기\n" +
                "- 좌클릭: UI 조작\n\n" +
                "Scene 뷰에서 웨이포인트 경로가\n" +
                "기즈모로 표시됩니다.\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }
        private static void CreateWPMarker(Transform parent, string label, Vector3 pos, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"WP_{label}";
            marker.transform.SetParent(parent);
            marker.transform.position = new Vector3(pos.x, 0.5f, pos.z);
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0);
            marker.GetComponent<Renderer>().material = mat;

            // Remove collider to avoid blocking movement
            var col = marker.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
        }
    }
}
