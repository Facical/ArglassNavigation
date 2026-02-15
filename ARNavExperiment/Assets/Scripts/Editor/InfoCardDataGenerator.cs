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

            CreateInfoCard(path, "a_card_01", "sign_card", "B125 대강의실 안내",
                "수용인원: 80명\n주요 장비: 빔프로젝터, 마이크, 대형 스크린\n용도: 지하1층 최대 규모 강의실\n위치: 서쪽복도 남단",
                "WP02", true);

            CreateInfoCard(path, "a_card_02", "poi_detail", "SW교차로 주변 시설 안내",
                "좌측(서쪽): B125 대강의실, B127 시청각실\n우측(남쪽): B123 강의실, 중앙 계단\n직진(북쪽): B128 학생상담실, B129-B131 강의실",
                "WP03", true);

            CreateInfoCard(path, "a_card_03", "sign_card", "B130 대학원강의실 안내",
                "수용인원: 30명\n주요 장비: 빔프로젝터, 화이트보드\n용도: 대학원 전용 강의실\n위치: 서쪽복도 중단",
                "WP05", true);

            CreateInfoCard(path, "a_card_04", "landmark", "NW교차로 주변 시설",
                "남쪽(서쪽복도): B131 강의실\n동쪽(북쪽복도): B132 강의실, B133 세미나실, B134 강의실\n현재 위치: 서쪽복도와 북쪽복도 교차점",
                "WP06", true);

            CreateInfoCard(path, "a_card_05", "comparison", "B132 vs B133 비교표",
                "B132: 강의실 (60명), 빔프로젝터/마이크\nB133: 세미나실 (15명), 화이트보드/모니터\n→ 소규모 토론에는 B133이 적합",
                "WP07", true);
        }

        // ==========================================
        // Route B InfoCards
        // ==========================================
        private static void GenerateRouteBInfoCards()
        {
            string path = "Assets/Data/InfoCards/RouteB";
            EnsureFolder(path);

            CreateInfoCard(path, "b_card_01", "sign_card", "B116 안내",
                "위치: 동쪽복도 남단\n용도: 현장 확인 필요\n참고: SE교차로에서 북쪽으로 이동 시 첫 번째 실",
                "WP02", true);

            CreateInfoCard(path, "b_card_02", "poi_detail", "E-T교차로 방향별 시설",
                "북쪽(직진): B107 전산지능연구실, B106 지능공학연구실\n남쪽(뒤로): B116, SE교차로\n주변: B110 (확인 필요)",
                "WP03", true);

            CreateInfoCard(path, "b_card_03", "sign_card", "B107 전산지능연구실 안내",
                "연구 분야: 전산 지능\n수용인원: 10명\n주요 장비: 서버, 워크스테이션\n위치: 동쪽복도 중단",
                "WP05", true);

            CreateInfoCard(path, "b_card_04", "landmark", "NE교차로 주변 시설",
                "남쪽(동쪽복도): B104-B106 교수실/연구실\n서쪽(북쪽복도): B102 네트워크연구실, B134 강의실\nNE 코너: B101 이재민 교수실",
                "WP06", true);

            CreateInfoCard(path, "b_card_05", "comparison", "B104 vs B105 비교표",
                "B104: 최태훈 교수실\nB105: 송영민 교수실\n→ 문 앞 안내판 정보 비교 (현장 확인 필요)",
                "WP07", true);
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
                items: new[] { "B132 강의실", "B133 세미나실" },
                attrs: new[] { "용도", "수용인원", "좌석 배치", "주요 장비", "소규모 토론 적합성" },
                vals: new[] {
                    "강의", "60명", "고정식 (강의 방향)", "빔프로젝터, 마이크", "부적합 (과대)",
                    "세미나/토론", "15명", "이동식 (원형 배치 가능)", "화이트보드, 모니터", "적합"
                });

            // Route B: B104 vs B105
            SetMissionComparison("RouteB_C1",
                comparisonId: "comp_b_b104_b105",
                items: new[] { "B104 최태훈 교수실", "B105 송영민 교수실" },
                attrs: new[] { "교수명", "전공 분야", "안내판 정보량", "연구실 위치" },
                vals: new[] {
                    "최태훈", "(현장 확인)", "상세 (현장 확인)", "동쪽복도 북단",
                    "송영민", "(현장 확인)", "간략 (현장 확인)", "동쪽복도 북단"
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
            string title, string content, string triggerWP, bool autoShow)
        {
            string path = $"{folder}/{cardId}.asset";

            var asset = ScriptableObject.CreateInstance<InfoCardData>();
            asset.cardId = cardId;
            asset.cardType = cardType;
            asset.title = title;
            asset.content = content;
            asset.triggerWaypointId = triggerWP;
            asset.autoShow = autoShow;

            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"  Created InfoCard: {path}");
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
