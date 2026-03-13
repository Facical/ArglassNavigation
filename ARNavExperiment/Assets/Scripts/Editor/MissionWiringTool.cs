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

            // Set1 missions (순서: A1, B1, A2, B2, C1)
            var set1 = so.FindProperty("set1Missions");
            var s1Missions = new[] {
                LoadMission("Set1_A1"),
                LoadMission("Set1_B1"),
                LoadMission("Set1_A2"),
                LoadMission("Set1_B2"),
                LoadMission("Set1_C1")
            };
            SetMissionList(set1, s1Missions);

            // Set2 missions (순서: A1, B1, A2, B2, C1)
            var set2 = so.FindProperty("set2Missions");
            var s2Missions = new[] {
                LoadMission("Set2_A1"),
                LoadMission("Set2_B1"),
                LoadMission("Set2_A2"),
                LoadMission("Set2_B2"),
                LoadMission("Set2_C1")
            };
            SetMissionList(set2, s2Missions);

            so.ApplyModifiedProperties();

            // Wire POIs to missions
            WirePOIsToMissions();

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(missionMgr);

            Debug.Log("[MissionWiring] 미션 데이터 연결 완료!");
            EditorUtility.DisplayDialog("완료",
                $"MissionManager에 연결 완료:\n" +
                $"- Set1: {s1Missions.Length}개 미션\n" +
                $"- Set2: {s2Missions.Length}개 미션\n" +
                $"- POI 연결 완료\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }

        private static void WirePOIsToMissions()
        {
            // Set1 POI assignments
            AssignPOIs("Set1_A1", new[] { "RouteB/room_b107", "RouteB/room_b106" });
            AssignPOIs("Set1_B1", new[] { "RouteB/room_b105", "RouteB/room_b104" });
            AssignPOIs("Set1_A2", new[] { "RouteB/room_b101", "RouteB/room_b102" });
            AssignPOIs("Set1_B2", new[] { "RouteB/room_b102", "RouteB/room_b101" });
            AssignPOIs("Set1_C1", new[] { "RouteB/room_b104", "RouteB/room_b105" });

            // Set2 POI assignments
            AssignPOIs("Set2_A1", new[] { "RouteB/room_b106", "RouteB/room_b107" });
            AssignPOIs("Set2_B1", new[] { "RouteB/room_b104", "RouteB/room_b105" });
            AssignPOIs("Set2_A2", new[] { "RouteB/room_b102", "RouteB/room_b101" });
            AssignPOIs("Set2_B2", new[] { "RouteB/room_b101", "RouteB/room_b102" });
            AssignPOIs("Set2_C1", new[] { "RouteB/room_b101", "RouteB/room_b102" });
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
