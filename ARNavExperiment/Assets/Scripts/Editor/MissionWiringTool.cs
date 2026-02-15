using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ARNavExperiment.Mission;

namespace ARNavExperiment.EditorTools
{
    public class MissionWiringTool
    {
        [MenuItem("ARNav/Wire Mission Data to Scene")]
        public static void WireMissionData()
        {
            var missionMgr = Object.FindObjectOfType<MissionManager>();
            if (missionMgr == null)
            {
                EditorUtility.DisplayDialog("에러", "MissionManager를 찾을 수 없습니다.", "확인");
                return;
            }

            var so = new SerializedObject(missionMgr);

            // Route A missions (순서: A1, B1, A2, B2, C1)
            var routeA = so.FindProperty("routeAMissions");
            var aMissions = new[] {
                LoadMission("RouteA_A1"),
                LoadMission("RouteA_B1"),
                LoadMission("RouteA_A2"),
                LoadMission("RouteA_B2"),
                LoadMission("RouteA_C1")
            };
            SetMissionList(routeA, aMissions);

            // Route B missions (순서: A1, B1, A2, B2, C1)
            var routeB = so.FindProperty("routeBMissions");
            var bMissions = new[] {
                LoadMission("RouteB_A1"),
                LoadMission("RouteB_B1"),
                LoadMission("RouteB_A2"),
                LoadMission("RouteB_B2"),
                LoadMission("RouteB_C1")
            };
            SetMissionList(routeB, bMissions);

            so.ApplyModifiedProperties();

            // Wire POIs to missions
            WirePOIsToMissions();

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(missionMgr);

            Debug.Log("[MissionWiring] 미션 데이터 연결 완료!");
            EditorUtility.DisplayDialog("완료",
                $"MissionManager에 연결 완료:\n" +
                $"- Route A: {aMissions.Length}개 미션\n" +
                $"- Route B: {bMissions.Length}개 미션\n" +
                $"- POI 연결 완료\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        private static void WirePOIsToMissions()
        {
            // Route A POI assignments
            AssignPOIs("RouteA_A1", new[] { "RouteA/room_b123", "RouteA/room_b125" });
            AssignPOIs("RouteA_B1", new[] { "RouteA/room_b127", "RouteA/room_b125" });
            AssignPOIs("RouteA_A2", new[] { "RouteA/room_b129", "RouteA/room_b130" });
            AssignPOIs("RouteA_B2", new[] { "RouteA/room_b133", "RouteA/room_b132" });
            AssignPOIs("RouteA_C1", new[] { "RouteA/room_b132", "RouteA/room_b133" });

            // Route B POI assignments
            AssignPOIs("RouteB_A1", new[] { "RouteB/room_b121", "RouteB/room_b116" });
            AssignPOIs("RouteB_B1", new[] { "RouteB/room_b110", "RouteB/room_b116" });
            AssignPOIs("RouteB_A2", new[] { "RouteB/room_b107", "RouteB/room_b106" });
            AssignPOIs("RouteB_B2", new[] { "RouteB/room_b101", "RouteB/room_b102" });
            AssignPOIs("RouteB_C1", new[] { "RouteB/room_b104", "RouteB/room_b105" });
        }

        private static void AssignPOIs(string missionName, string[] poiPaths)
        {
            var mission = LoadMission(missionName);
            if (mission == null) return;

            var pois = new List<POIData>();
            foreach (var p in poiPaths)
            {
                var poi = AssetDatabase.LoadAssetAtPath<POIData>($"Assets/Data/POIs/{p}.asset");
                if (poi != null) pois.Add(poi);
            }

            var so = new SerializedObject(mission);
            var prop = so.FindProperty("relevantPOIs");
            prop.arraySize = pois.Count;
            for (int i = 0; i < pois.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = pois[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mission);
        }

        private static MissionData LoadMission(string name)
        {
            return AssetDatabase.LoadAssetAtPath<MissionData>($"Assets/Data/Missions/{name}.asset");
        }

        private static void SetMissionList(SerializedProperty listProp, MissionData[] missions)
        {
            listProp.arraySize = missions.Length;
            for (int i = 0; i < missions.Length; i++)
            {
                if (missions[i] != null)
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = missions[i];
                else
                    Debug.LogWarning($"Mission asset not found at index {i}");
            }
        }
    }
}
