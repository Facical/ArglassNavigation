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

            // === Route A (서쪽-북쪽 루프) ===
            // 계단 → 남쪽복도 서진 → SW교차로 → 서쪽복도 북상 → NW교차로 → 북쪽복도 동진
            var routeA = so.FindProperty("routeA");
            routeA.FindPropertyRelative("routeId").stringValue = "A";
            var wpA = routeA.FindPropertyRelative("waypoints");
            wpA.arraySize = 8;

            SetWaypoint(wpA, 0, "WP01", new Vector3(-7f, 0f, 0f), 2.5f, "Near B123 Classroom (South corridor)");
            SetWaypoint(wpA, 1, "WP02", new Vector3(-20f, 0f, 4f), 2.5f, "Near B125 Main Lecture Hall (West)");
            SetWaypoint(wpA, 2, "WP03", new Vector3(-20f, 0f, 0f), 3f, "SW Junction (T1 trigger)");
            SetWaypoint(wpA, 3, "WP04", new Vector3(-20f, 0f, 11f), 2.5f, "Near B129 Graduate Classroom (West corridor)");
            SetWaypoint(wpA, 4, "WP05", new Vector3(-20f, 0f, 14f), 2.5f, "Near B130 Graduate Classroom (West corridor)");
            SetWaypoint(wpA, 5, "WP06", new Vector3(-20f, 0f, 25f), 3f, "NW Junction (T4 trigger)");
            SetWaypoint(wpA, 6, "WP07", new Vector3(-11f, 0f, 25f), 2.5f, "Near B132 Classroom (North corridor)");
            SetWaypoint(wpA, 7, "WP08", new Vector3(-3f, 0f, 25f), 2.5f, "Near B133 Seminar Room (North corridor)");

            // === Route B (동쪽-북쪽 루프) ===
            // 계단 → 남쪽복도 동진 → SE교차로 → 동쪽복도 북상 → NE교차로 → 북쪽복도 서진
            var routeB = so.FindProperty("routeB");
            routeB.FindPropertyRelative("routeId").stringValue = "B";
            var wpB = routeB.FindPropertyRelative("waypoints");
            wpB.arraySize = 8;

            SetWaypoint(wpB, 0, "WP01", new Vector3(7f, 0f, 0f), 2.5f, "Near B121 Computer Lab (South corridor)");
            SetWaypoint(wpB, 1, "WP02", new Vector3(20f, 0f, 4f), 2.5f, "Near B116 (East corridor south end)");
            SetWaypoint(wpB, 2, "WP03", new Vector3(20f, 0f, 8f), 3f, "E-T Junction (T2 trigger)");
            SetWaypoint(wpB, 3, "WP04", new Vector3(20f, 0f, 11f), 2.5f, "Near B110 (East corridor)");
            SetWaypoint(wpB, 4, "WP05", new Vector3(20f, 0f, 15f), 2.5f, "Near B107 Computational Intelligence Lab (East corridor)");
            SetWaypoint(wpB, 5, "WP06", new Vector3(20f, 0f, 25f), 3f, "NE Junction (T3 trigger)");
            SetWaypoint(wpB, 6, "WP07", new Vector3(11f, 0f, 25f), 2.5f, "Near B104 Prof. Choi's Office (North corridor)");
            SetWaypoint(wpB, 7, "WP08", new Vector3(3f, 0f, 25f), 2.5f, "Near B105 Prof. Song's Office (North corridor)");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(wpMgr);

            Debug.Log("[WaypointDataGenerator] Route A/B 웨이포인트 설정 완료!");
            EditorUtility.DisplayDialog("완료",
                "웨이포인트 경로 설정 완료!\n\n" +
                "Route A: 8개 WP (계단→서→SW→서북→NW→북동)\n" +
                "Route B: 8개 WP (계단→동→SE→동북→NE→북서)\n\n" +
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
