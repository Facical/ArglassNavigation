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
            GenerateSet1InfoCards();
            GenerateSet2InfoCards();
            WireInfoCardsToMissions();
            SetComparisonData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[InfoCardDataGenerator] InfoCard 및 Comparison 데이터 생성 완료!");
            EditorUtility.DisplayDialog("완료",
                "InfoCard 10개 + ComparisonData 2개 생성 완료!\n\n" +
                "- Set1: 5개 InfoCard + B104 vs B105 비교표 (B105 정답)\n" +
                "- Set2: 5개 InfoCard + B101 vs B102 비교표 (B102 정답)\n\n" +
                "Assets/Data/InfoCards/ 폴더를 확인하세요.", "확인");
        }

        // ==========================================
        // Set1 InfoCards
        // ==========================================
        private static void GenerateSet1InfoCards()
        {
            string path = "Assets/Data/InfoCards/Set1";
            EnsureFolder(path);
            EnsureFolder("Assets/Resources/Data/InfoCards/Set1");

            CreateInfoCard(path, "s1_card_01", "sign_card", "B107 Pervasive & Intelligent Computing Lab Guide",
                "Research area: Pervasive and Intelligent Computing\nCapacity: 10\nEquipment: Server, Workstation\nAlso: Smart Military Logistics Innovation Center\nLocation: East corridor, north of B111",
                "WP02", true,
                "B107 편재 및 지능형 컴퓨팅 연구실 안내",
                "연구 분야: 편재 및 지능형 컴퓨팅\n수용인원: 10\n장비: 서버, 워크스테이션\n부설: 스마트군수혁신융합연구센터\n위치: 동쪽 복도, B111에서 북쪽");

            CreateInfoCard(path, "s1_card_02", "poi_detail", "East Corridor Professor Area Guide",
                "North (Straight): B104 Prof. Choe, B103, B102, B101\nSouth (Back): B106 Medical Eng. Lab, B107 Computing Lab, B111 Start\nCurrent area: B105 Prof. Song's Office",
                "WP03", true,
                "동쪽 복도 교수실 구간 안내",
                "북쪽 (직진): B104 최세운 교수실, B103, B102, B101\n남쪽 (뒤): B106 의료공학연구실, B107 컴퓨팅 연구실, B111 출발점\n현재 구간: B105 송광섭 교수 연구실");

            CreateInfoCard(path, "s1_card_03", "sign_card", "B101 Prof. Lee's Office Guide",
                "Professor: Prof. Jae-Min Lee (Computer Engineering)\nPartnership: Hanwha Systems Maritime Lab, LGU+ Smart Factory Center\nLocation: North end of east corridor\nNearby: B102 NSL Lab, B103",
                "WP05", true,
                "B101 이재민 교수 연구실 안내",
                "교수: 이재민 교수 (컴퓨터공학과)\n산학협력: Hanwha Systems 해양연구소, LGU+ 스마트팩토리 연구센터\n위치: 동쪽 복도 북쪽 끝\n인근: B102 NSL 연구실, B103");

            CreateInfoCard(path, "s1_card_04", "landmark", "NE Corner Area Guide",
                "South (East corridor): B103, B104 Prof. Choe, B105 Prof. Song\nWest (North corridor): B132, B134\nNearby: B101 Prof. Lee, B102 NSL Lab\nStairs visible at NE corner",
                "WP06", true,
                "북동 코너 안내",
                "남쪽 (동쪽 복도): B103, B104 최세운 교수실, B105 송광섭 교수실\n서쪽 (북쪽 복도): B132, B134\n인근: B101 이재민 교수실, B102 NSL 연구실\n북동 코너에 계단 있음");

            CreateInfoCard(path, "s1_card_05", "comparison", "B104 vs B105 Comparison",
                "B104: Prof. Choe Se-woon (Biomedical Eng.) — name sign only\nB105: Prof. Song Kwang-Soup (Biomedical Eng.) — name sign + ICT Convergence, NIPA signs\n→ Compare the amount of sign info posted outside each office",
                "WP07", true,
                "B104 vs B105 비교",
                "B104: 최세운 교수 (바이오메디컬공학과) — 이름 문패만\nB105: 송광섭 교수 (바이오메디컬공학과) — 이름 문패 + ICT 융합센터, NIPA 간판\n→ 각 연구실 문 밖 게시 정보량을 비교하세요");
        }

        // ==========================================
        // Set2 InfoCards (영상 분석 확정 — 2026-03-09)
        // ==========================================
        private static void GenerateSet2InfoCards()
        {
            string path = "Assets/Data/InfoCards/Set2";
            EnsureFolder(path);
            EnsureFolder("Assets/Resources/Data/InfoCards/Set2");

            CreateInfoCard(path, "s2_card_01", "sign_card", "B106 Medical Engineering Research Lab Guide",
                "Department: Biomedical Engineering\nResearch area: Medical Engineering\nCapacity: 10\nEquipment: Medical equipment, Workstation\nLocation: East corridor, between B105 and B107",
                "WP02", true,
                "B106 의료공학연구실 안내",
                "소속: 바이오메디컬공학과\n연구 분야: 의료공학\n수용인원: 10\n장비: 의료 장비, 워크스테이션\n위치: 동쪽 복도, B105와 B107 사이");

            CreateInfoCard(path, "s2_card_02", "poi_detail", "East Corridor Prof. Office Guide",
                "North: B103, B102 NSL Lab, B101 Prof. Lee\nSouth: B106 Medical Eng. Lab, B107 Computing Lab\nCurrent area: B104 Prof. Choe Se-woon's Office (Biomedical Eng.)",
                "WP03", true,
                "동쪽 복도 교수실 구간 안내",
                "북쪽: B103, B102 NSL 연구실, B101 이재민 교수실\n남쪽: B106 의료공학연구실, B107 컴퓨팅 연구실\n현재 구간: B104 최세운 교수 연구실 (바이오메디컬공학과)");

            CreateInfoCard(path, "s2_card_03", "sign_card", "B102 Networked Systems Lab (NSL) Guide",
                "Lab: Networked Systems Lab (NSL)\nResearch: Network-based systems, DDS, Edge-AI, K-MOSA, blockchain\nPartners: Hanwha Systems, NS Lab, LGU+\nCapacity: 10\nLocation: North section of east corridor",
                "WP05", true,
                "B102 네트워크 기반 시스템 연구실 안내",
                "연구실: 네트워크 기반 시스템 연구실 (NSL)\n연구: 네트워크 시스템, DDS, Edge-AI, K-MOSA, 블록체인\n협력: Hanwha Systems, NS Lab, LGU+\n수용인원: 10\n위치: 동쪽 복도 북쪽 구간");

            CreateInfoCard(path, "s2_card_04", "landmark", "NE Corner & Prof. Lee Area Guide",
                "South: B103, B104 Prof. Choe, B105 Prof. Song\nWest: North corridor (B132, B134)\nNearby: B101 Prof. Lee (Hanwha Systems, LGU+ signs), B102 NSL Lab\nStairs at NE corner",
                "WP06", true,
                "북동 코너 및 이교수실 구간 안내",
                "남쪽: B103, B104 최세운 교수실, B105 송광섭 교수실\n서쪽: 북쪽 복도 (B132, B134)\n인근: B101 이재민 교수실 (Hanwha Systems, LGU+ 간판), B102 NSL 연구실\n북동 코너에 계단 있음");

            CreateInfoCard(path, "s2_card_05", "comparison", "B101 vs B102 Comparison",
                "B101: Prof. Lee's Office — Hanwha Systems + LGU+ partnership plaques\nB102: NSL Lab — recruitment poster, employment chart, class schedule, research papers\n→ Compare the amount of notices/posters posted outside each room",
                "WP07", true,
                "B101 vs B102 비교",
                "B101: 이재민 교수실 — Hanwha Systems + LGU+ 산학협력 간판\nB102: NSL 연구실 — 모집 포스터, 취업현황, 강의시간표, 연구 논문\n→ 각 방 문 밖 게시물/포스터 양을 비교하세요");
        }

        // ==========================================
        // Wire InfoCards to Missions
        // ==========================================
        private static void WireInfoCardsToMissions()
        {
            // Set1
            WireCards("Set1_A1", new[] { "Set1/s1_card_01" });
            WireCards("Set1_B1", new[] { "Set1/s1_card_02" });
            WireCards("Set1_A2", new[] { "Set1/s1_card_03" });
            WireCards("Set1_B2", new[] { "Set1/s1_card_04" });
            WireCards("Set1_C1", new[] { "Set1/s1_card_05" });

            // Set2
            WireCards("Set2_A1", new[] { "Set2/s2_card_01" });
            WireCards("Set2_B1", new[] { "Set2/s2_card_02" });
            WireCards("Set2_A2", new[] { "Set2/s2_card_03" });
            WireCards("Set2_B2", new[] { "Set2/s2_card_04" });
            WireCards("Set2_C1", new[] { "Set2/s2_card_05" });
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
            // Set1: B104 vs B105 — B105가 정답 (추가 간판 다수)
            SetMissionComparison("Set1_C1",
                comparisonId: "comp_s1_b104_b105",
                items: new[] { "B104 Prof. Choe's Office", "B105 Prof. Song's Office" },
                attrs: new[] { "Professor", "Department", "Sign Detail", "Additional Signs" },
                vals: new[] {
                    "Choe Se-woon", "Biomedical Eng.", "Basic (name only)", "None",
                    "Song Kwang-Soup", "Biomedical Eng.", "Detailed", "ICT Convergence Center, NIPA, UACon"
                });

            // Set2: B101 vs B102 — B102가 정답 (게시물 다수)
            SetMissionComparison("Set2_C1",
                comparisonId: "comp_s2_b101_b102",
                items: new[] { "B101 Prof. Lee's Office", "B102 NSL Lab" },
                attrs: new[] { "Type", "Occupant", "Outside Postings", "Partnership Signs" },
                vals: new[] {
                    "Professor Office", "Prof. Lee Jae-Min", "Few", "Hanwha Systems, LGU+",
                    "Research Lab", "NSL Team", "Many (poster, papers, schedule)", "Listed in recruitment poster"
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
