using UnityEngine;
using UnityEditor;
using ARNavExperiment.Navigation;

namespace ARNavExperiment.EditorTools
{
    public class WaypointDataGenerator
    {
        [MenuItem("ARNav/Configure Waypoint Routes")]
        public static void ConfigureRoutes()
        {
            var wpMgr = Object.FindObjectOfType<WaypointManager>();
            if (wpMgr == null)
            {
                EditorUtility.DisplayDialog("에러", "WaypointManager를 찾을 수 없습니다.\n먼저 Setup Main Scene을 실행하세요.", "확인");
                return;
            }

            var so = new SerializedObject(wpMgr);

            // === Route B (동쪽복도 B111 시작 → 북상 → NE코너 U턴 → 남하) ===
            // Route B 단일 경로 사용 (Set1/Set2 미션 세트로 분리)
            var routeB = so.FindProperty("routeB");
            routeB.FindPropertyRelative("routeId").stringValue = "B";
            var wpB = routeB.FindPropertyRelative("waypoints");
            wpB.arraySize = 9;

            SetWaypoint(wpB, 0, "WP00", new Vector3(36f, 0f, 24f), 2.5f, "Near B110 (Calibration anchor)");
            SetWaypoint(wpB, 1, "WP01", new Vector3(36f, 0f, 18f), 2.5f, "Near B111 (Start, East corridor mid)");
            SetWaypoint(wpB, 2, "WP02", new Vector3(36f, 0f, 33f), 2.5f, "Near B107 Computational Intelligence Lab (East corridor)");
            SetWaypoint(wpB, 3, "WP03", new Vector3(36f, 0f, 45f), 3f, "Near B105 Prof. Song's Office (T2 trigger)");
            SetWaypoint(wpB, 4, "WP04", new Vector3(36f, 0f, 57f), 2.5f, "Near B103 (East corridor north)");
            SetWaypoint(wpB, 5, "WP05", new Vector3(36f, 0f, 69f), 2.5f, "Near B101 Prof. Lee's Office (East corridor end)");
            SetWaypoint(wpB, 6, "WP06", new Vector3(39f, 0f, 72f), 3f, "NE Corner U-turn (T3 trigger)");
            SetWaypoint(wpB, 7, "WP07", new Vector3(36f, 0f, 48f), 2.5f, "Near B104/B105 (Return, C1 comparison)");
            SetWaypoint(wpB, 8, "WP08", new Vector3(36f, 0f, -7f), 2.5f, "Near B121 Computer Lab (South corridor end)");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(wpMgr);

            Debug.Log("[WaypointDataGenerator] Route B 웨이포인트 설정 완료!");
            EditorUtility.DisplayDialog("완료",
                "웨이포인트 경로 설정 완료!\n\n" +
                "Route B: 9개 WP (WP00 보정앵커→B111→북상→NE U턴→남하→남쪽복도)\n\n" +
                "※ 좌표는 평면도 기반 추정치입니다.\n" +
                "현장 답사 후 Inspector에서 직접 조정하세요.\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        private static void SetWaypoint(SerializedProperty list, int index,
            string id, Vector3 pos, float radius, string locationName)
        {
            var wp = list.GetArrayElementAtIndex(index);
            wp.FindPropertyRelative("waypointId").stringValue = id;
            wp.FindPropertyRelative("fallbackPosition").vector3Value = pos;
            wp.FindPropertyRelative("radius").floatValue = radius;
            wp.FindPropertyRelative("locationName").stringValue = locationName;
        }
    }
}
