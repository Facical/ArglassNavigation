using UnityEngine;
using UnityEditor;
using ARNavExperiment.Mission;

namespace ARNavExperiment.EditorTools
{
    public class InfoCardDataGenerator
    {
        [MenuItem("ARNav/Generate InfoCard & Comparison Data")]
        public static void GenerateAll()
        {
            GenerateRouteAInfoCards();
            GenerateRouteBInfoCards();
            WireInfoCardsToMissions();
            SetComparisonData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[InfoCardDataGenerator] InfoCard 및 Comparison 데이터 생성 완료!");
            EditorUtility.DisplayDialog("완료",
                "InfoCard 10개 + ComparisonData 2개 생성 완료!\n\n" +
                "- Route A: 5개 InfoCard + B132 vs B133 비교표\n" +
                "- Route B: 5개 InfoCard + B104 vs B105 비교표\n\n" +
                "Assets/Data/InfoCards/ 폴더를 확인하세요.", "확인");
        }

        // ==========================================
        // Route A InfoCards
        // ==========================================
        private static void GenerateRouteAInfoCards()
        {
            string path = "Assets/Data/InfoCards/RouteA";
            EnsureFolder(path);
            EnsureFolder("Assets/Resources/Data/InfoCards/RouteA");

            CreateInfoCard(path, "a_card_01", "sign_card", "B125 Main Lecture Hall Guide",
                "Capacity: 80\nEquipment: Projector, Microphone, Large Screen\nPurpose: Largest classroom on B1F\nLocation: South end of west corridor",
                "WP02", true,
                "B125 대강의실 안내",
                "수용인원: 80\n장비: 프로젝터, 마이크, 대형 스크린\n용도: B1F 최대 규모 강의실\n위치: 서쪽 복도 남단");

            CreateInfoCard(path, "a_card_02", "poi_detail", "SW Junction Area Guide",
                "Left (West): B125 Main Lecture Hall, B127 AV Room\nRight (South): B123 Classroom, Central Stairs\nStraight (North): B128 Student Counseling, B129-B131 Classrooms",
                "WP03", true,
                "남서 교차로 안내",
                "왼쪽 (서쪽): B125 대강의실, B127 시청각실\n오른쪽 (남쪽): B123 강의실, 중앙 계단\n직진 (북쪽): B128 학생 상담실, B129-B131 강의실");

            CreateInfoCard(path, "a_card_03", "sign_card", "B130 Graduate Classroom Guide",
                "Capacity: 30\nEquipment: Projector, Whiteboard\nPurpose: Graduate-only classroom\nLocation: Mid-section of west corridor",
                "WP05", true,
                "B130 대학원 강의실 안내",
                "수용인원: 30\n장비: 프로젝터, 화이트보드\n용도: 대학원 전용 강의실\n위치: 서쪽 복도 중간");

            CreateInfoCard(path, "a_card_04", "landmark", "NW Junction Area Guide",
                "South (West corridor): B131 Classroom\nEast (North corridor): B132 Classroom, B133 Seminar Room, B134 Classroom\nCurrent location: West-North corridor junction",
                "WP06", true,
                "북서 교차로 안내",
                "남쪽 (서쪽 복도): B131 강의실\n동쪽 (북쪽 복도): B132 강의실, B133 세미나실, B134 강의실\n현재 위치: 서쪽-북쪽 복도 교차점");

            CreateInfoCard(path, "a_card_05", "comparison", "B132 vs B133 Comparison",
                "B132: Classroom (60), Projector/Microphone\nB133: Seminar Room (15), Whiteboard/Monitor\n→ B133 is suitable for small group discussions",
                "WP07", true,
                "B132 vs B133 비교",
                "B132: 강의실 (60명), 프로젝터/마이크\nB133: 세미나실 (15명), 화이트보드/모니터\n→ B133이 소그룹 토론에 적합");
        }

        // ==========================================
        // Route B InfoCards
        // ==========================================
        private static void GenerateRouteBInfoCards()
        {
            string path = "Assets/Data/InfoCards/RouteB";
            EnsureFolder(path);
            EnsureFolder("Assets/Resources/Data/InfoCards/RouteB");

            CreateInfoCard(path, "b_card_01", "sign_card", "B116 Guide",
                "Location: South end of east corridor\nPurpose: TBD on-site\nNote: First room heading north from SE junction",
                "WP02", true,
                "B116 안내",
                "위치: 동쪽 복도 남단\n용도: 현장 확인 필요\n참고: 남동 교차로에서 북쪽으로 첫 번째 공간");

            CreateInfoCard(path, "b_card_02", "poi_detail", "E-T Junction Area Guide",
                "North (Straight): B107 Computational Intelligence Lab, B106 Intelligent Engineering Lab\nSouth (Back): B116, SE junction\nNearby: B110 (TBD)",
                "WP03", true,
                "동쪽 교차로 안내",
                "북쪽 (직진): B107 전산지능연구실, B106 지능공학연구실\n남쪽 (뒤): B116, 남동 교차로\n인근: B110 (현장 확인 필요)");

            CreateInfoCard(path, "b_card_03", "sign_card", "B107 Computational Intelligence Lab Guide",
                "Research area: Computational Intelligence\nCapacity: 10\nEquipment: Server, Workstation\nLocation: Mid-section of east corridor",
                "WP05", true,
                "B107 전산지능연구실 안내",
                "연구 분야: 전산지능\n수용인원: 10\n장비: 서버, 워크스테이션\n위치: 동쪽 복도 중간");

            CreateInfoCard(path, "b_card_04", "landmark", "NE Junction Area Guide",
                "South (East corridor): B104-B106 Offices/Labs\nWest (North corridor): B102 Network Lab, B134 Classroom\nNE corner: B101 Prof. Lee's Office",
                "WP06", true,
                "북동 교차로 안내",
                "남쪽 (동쪽 복도): B104-B106 연구실/교수실\n서쪽 (북쪽 복도): B102 네트워크 연구실, B134 강의실\n북동 모서리: B101 이교수 연구실");

            CreateInfoCard(path, "b_card_05", "comparison", "B104 vs B105 Comparison",
                "B104: Prof. Choi's Office\nB105: Prof. Song's Office\n→ Compare door sign info (verify on-site)",
                "WP07", true,
                "B104 vs B105 비교",
                "B104: 최교수 연구실\nB105: 송교수 연구실\n→ 문패 정보를 비교하세요 (현장에서 확인)");
        }

        // ==========================================
        // Wire InfoCards to Missions
        // ==========================================
        private static void WireInfoCardsToMissions()
        {
            // Route A: Each mission gets its relevant info cards
            WireCards("RouteA_A1", new[] { "RouteA/a_card_01" });
            WireCards("RouteA_B1", new[] { "RouteA/a_card_02" });
            WireCards("RouteA_A2", new[] { "RouteA/a_card_03" });
            WireCards("RouteA_B2", new[] { "RouteA/a_card_04" });
            WireCards("RouteA_C1", new[] { "RouteA/a_card_05" });

            // Route B
            WireCards("RouteB_A1", new[] { "RouteB/b_card_01" });
            WireCards("RouteB_B1", new[] { "RouteB/b_card_02" });
            WireCards("RouteB_A2", new[] { "RouteB/b_card_03" });
            WireCards("RouteB_B2", new[] { "RouteB/b_card_04" });
            WireCards("RouteB_C1", new[] { "RouteB/b_card_05" });
        }

        private static void WireCards(string missionName, string[] cardPaths)
        {
            var mission = AssetDatabase.LoadAssetAtPath<MissionData>($"Assets/Data/Missions/{missionName}.asset");
            if (mission == null) { Debug.LogWarning($"Mission not found: {missionName}"); return; }

            var so = new SerializedObject(mission);
            var prop = so.FindProperty("infoCards");
            prop.arraySize = cardPaths.Length;
            for (int i = 0; i < cardPaths.Length; i++)
            {
                var card = AssetDatabase.LoadAssetAtPath<InfoCardData>($"Assets/Data/InfoCards/{cardPaths[i]}.asset");
                if (card != null)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = card;
                else
                    Debug.LogWarning($"InfoCard not found: {cardPaths[i]}");
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mission);
        }

        // ==========================================
        // ComparisonData (embedded in Mission C)
        // ==========================================
        private static void SetComparisonData()
        {
            // Route A: B132 vs B133
            SetMissionComparison("RouteA_C1",
                comparisonId: "comp_a_b132_b133",
                items: new[] { "B132 Classroom", "B133 Seminar Room" },
                attrs: new[] { "Purpose", "Capacity", "Seating", "Equipment", "Small Group Suitability" },
                vals: new[] {
                    "Lecture", "60", "Fixed (lecture-facing)", "Projector, Microphone", "Not suitable (too large)",
                    "Seminar/Discussion", "15", "Movable (circle layout possible)", "Whiteboard, Monitor", "Suitable"
                });

            // Route B: B104 vs B105
            SetMissionComparison("RouteB_C1",
                comparisonId: "comp_b_b104_b105",
                items: new[] { "B104 Prof. Choi's Office", "B105 Prof. Song's Office" },
                attrs: new[] { "Professor", "Research Field", "Sign Detail Level", "Office Location" },
                vals: new[] {
                    "Prof. Choi", "(verify on-site)", "Detailed (verify on-site)", "North end of east corridor",
                    "Prof. Song", "(verify on-site)", "Brief (verify on-site)", "North end of east corridor"
                });
        }

        private static void SetMissionComparison(string missionName, string comparisonId,
            string[] items, string[] attrs, string[] vals)
        {
            var mission = AssetDatabase.LoadAssetAtPath<MissionData>($"Assets/Data/Missions/{missionName}.asset");
            if (mission == null) { Debug.LogWarning($"Mission not found: {missionName}"); return; }

            var so = new SerializedObject(mission);
            var comp = so.FindProperty("comparisonData");
            comp.FindPropertyRelative("comparisonId").stringValue = comparisonId;

            var itemNames = comp.FindPropertyRelative("itemNames");
            itemNames.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                itemNames.GetArrayElementAtIndex(i).stringValue = items[i];

            var attributes = comp.FindPropertyRelative("attributes");
            attributes.arraySize = attrs.Length;
            for (int i = 0; i < attrs.Length; i++)
                attributes.GetArrayElementAtIndex(i).stringValue = attrs[i];

            var values = comp.FindPropertyRelative("values");
            values.arraySize = vals.Length;
            for (int i = 0; i < vals.Length; i++)
                values.GetArrayElementAtIndex(i).stringValue = vals[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mission);
        }

        // ==========================================
        // Helper
        // ==========================================
        private static void CreateInfoCard(string folder, string cardId, string cardType,
            string title, string content, string triggerWP, bool autoShow,
            string titleKo = "", string contentKo = "")
        {
            // 기존 경로 (Assets/Data/)
            CreateOrUpdateInfoCardAsset($"{folder}/{cardId}.asset",
                cardId, cardType, title, content, triggerWP, autoShow, titleKo, contentKo);

            // Resources 경로 (런타임 fallback용)
            string resFolder = folder.Replace("Assets/Data/", "Assets/Resources/Data/");
            CreateOrUpdateInfoCardAsset($"{resFolder}/{cardId}.asset",
                cardId, cardType, title, content, triggerWP, autoShow, titleKo, contentKo);
        }

        private static void CreateOrUpdateInfoCardAsset(string path, string cardId, string cardType,
            string title, string content, string triggerWP, bool autoShow,
            string titleKo, string contentKo)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InfoCardData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<InfoCardData>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"  Created InfoCard: {path}");
            }
            else
            {
                Debug.Log($"  Updated InfoCard: {path}");
            }

            asset.cardId = cardId;
            asset.cardType = cardType;
            asset.title = title;
            asset.content = content;
            asset.triggerWaypointId = triggerWP;
            asset.autoShow = autoShow;
            asset.titleKo = titleKo;
            asset.contentKo = contentKo;
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
