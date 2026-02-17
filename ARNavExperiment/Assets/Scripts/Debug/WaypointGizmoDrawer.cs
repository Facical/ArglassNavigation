using UnityEngine;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.DebugTools
{
    /// <summary>
    /// Scene/Game 뷰에서 웨이포인트 위치와 경로를 시각적으로 표시.
    /// WaypointManager와 같은 GameObject에 추가.
    /// </summary>
    [RequireComponent(typeof(WaypointManager))]
    public class WaypointGizmoDrawer : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool showRouteA = true;
        [SerializeField] private bool showRouteB = true;
        [SerializeField] private bool showLabels = true;

        [Header("Colors")]
        [SerializeField] private Color routeAColor = new Color(0.2f, 0.6f, 1f, 0.8f);
        [SerializeField] private Color routeBColor = new Color(0.9f, 0.5f, 0.1f, 0.8f);
        [SerializeField] private Color activeColor = Color.green;

        private void OnDrawGizmos()
        {
            var wpMgr = GetComponent<WaypointManager>();
            if (wpMgr == null) return;

            // Route A/B are private SerializeField, use SerializedObject in editor
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(wpMgr);

            if (showRouteA)
                DrawRoute(so.FindProperty("routeA"), routeAColor, "A");
            if (showRouteB)
                DrawRoute(so.FindProperty("routeB"), routeBColor, "B");
#endif
        }

#if UNITY_EDITOR
        private void DrawRoute(UnityEditor.SerializedProperty routeProp, Color color, string label)
        {
            if (routeProp == null) return;

            var waypoints = routeProp.FindPropertyRelative("waypoints");
            if (waypoints == null || waypoints.arraySize == 0) return;

            Gizmos.color = color;

            for (int i = 0; i < waypoints.arraySize; i++)
            {
                var wp = waypoints.GetArrayElementAtIndex(i);
                var pos = wp.FindPropertyRelative("position").vector3Value;
                var radius = wp.FindPropertyRelative("radius").floatValue;
                var wpId = wp.FindPropertyRelative("waypointId").stringValue;
                var locName = wp.FindPropertyRelative("locationName").stringValue;

                // Draw waypoint sphere
                Gizmos.DrawWireSphere(pos + Vector3.up * 0.1f, radius);

                // Draw center marker
                Gizmos.DrawSphere(pos + Vector3.up * 0.1f, 0.15f);

                // Draw path line to next waypoint
                if (i < waypoints.arraySize - 1)
                {
                    var nextPos = waypoints.GetArrayElementAtIndex(i + 1)
                        .FindPropertyRelative("position").vector3Value;
                    Gizmos.DrawLine(pos + Vector3.up * 0.1f, nextPos + Vector3.up * 0.1f);
                }

                // Draw label
                if (showLabels)
                {
                    var style = new GUIStyle
                    {
                        normal = { textColor = color },
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    UnityEditor.Handles.Label(pos + Vector3.up * 1.5f,
                        $"[Route {label}] {wpId}\n{locName}", style);
                }
            }

            // Draw start marker
            if (waypoints.arraySize > 0)
            {
                var startPos = waypoints.GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
                Gizmos.color = Color.red;
                Gizmos.DrawCube(startPos + Vector3.up * 0.5f, new Vector3(0.3f, 1f, 0.3f));
            }
        }
#endif
    }
}
