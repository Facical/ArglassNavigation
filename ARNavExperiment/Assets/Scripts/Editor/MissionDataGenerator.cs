using UnityEngine;
using UnityEditor;
using ARNavExperiment.Mission;

namespace ARNavExperiment.EditorTools
{
    public class MissionDataGenerator
    {
        [MenuItem("ARNav/Generate Mission Data")]
        public static void GenerateAll()
        {
            GenerateSet1Missions();
            GenerateSet2Missions();
            GenerateRouteBPOIs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MissionDataGenerator] 모든 미션/POI 데이터 생성 완료!");
            EditorUtility.DisplayDialog("완료", "Set1/Set2 미션 데이터와 POI 데이터가 생성되었습니다.\n\nAssets/Data/ 폴더를 확인하세요.", "확인");
        }

        // ==========================================
        // Set1 Missions (B111 시작 → 북상 → NE U턴 → 남하)
        // 트리거: T2(정보불일치), T3(해상도부족)
        // ==========================================
        private static void GenerateSet1Missions()
        {
            // A1: 지능 관련 연구실 찾기 (북상 중 B107)
            CreateMission("Set1_A1", new MissionParams {
                missionId = "A1",
                type = MissionType.A_DirectionVerify,
                briefing = "Find a research lab focused on intelligence-related studies. Upon arrival, check the room number.",
                targetWP = "WP02",
                question = "What is the room number of the research lab you just arrived at?",
                options = new[] { "B106", "B107", "B108", "B109" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "지능 관련 연구를 수행하는 연구실을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B106", "B107", "B108", "B109" }
            });

            // B1: 첫 번째 교수 연구실 찾기 (T2 트리거, 북상 중 B105)
            CreateMission("Set1_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Walk along the corridor and find the first professor's office you encounter.",
                targetWP = "WP03",
                question = "What is the room number of the first professor's office you encountered?",
                options = new[] { "B104", "B105", "B106", "B107" },
                correctIndex = 1,
                triggerId = "T2",
                briefingKo = "복도를 따라 이동하면서 처음 만나는 교수 연구실을 찾으세요.",
                questionKo = "처음 만난 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B104", "B105", "B106", "B107" }
            });

            // A2: 복도 끝 교수 연구실 찾기 (북상 끝 B101)
            CreateMission("Set1_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "Find a professor's office at the far end of this corridor. Upon arrival, check the room number.",
                targetWP = "WP05",
                question = "What is the room number of the professor's office you just arrived at?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 0,
                triggerId = "",
                briefingKo = "이 복도 끝에 있는 교수 연구실을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B101", "B102", "B103", "B104" }
            });

            // B2: 네트워크 연구실 찾기 (T3 트리거, NE코너 U턴 B102)
            CreateMission("Set1_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find the network research lab near this corner.",
                targetWP = "WP06",
                question = "What is the room number of the research lab you arrived at?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 1,
                triggerId = "T3",
                briefingKo = "이 코너 근처에서 네트워크 연구실을 찾으세요.",
                questionKo = "도착한 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B101", "B102", "B103", "B104" }
            });

            // C1: B104 vs B105 비교 (복귀 중)
            // B104: 최세운 교수 이름 문패만 있음
            // B105: 송광섭 교수 이름 문패 + ICT 융합센터, NIPA, (주)유에이션 추가 간판
            CreateMission("Set1_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "Check rooms B104 and B105 (professor offices). Select the one with more detailed information posted on the door and wall signs.",
                targetWP = "WP07",
                question = "Which professor's office has more detailed sign info?",
                options = new[] { "B104", "B105", "Both equal", "Neither has info" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "B104와 B105(교수 연구실)를 확인하세요. 문과 벽에 더 상세한 정보가 게시된 쪽을 선택하세요.",
                questionKo = "어느 교수 연구실의 문패 정보가 더 상세합니까?",
                optionsKo = new[] { "B104", "B105", "둘 다 동일", "둘 다 정보 없음" }
            });
        }

        // ==========================================
        // Set2 Missions (동일 경로, 다른 미션 콘텐츠)
        // 트리거: T1(안내열화), T4(안내부재)
        // 영상 분석 확정 (2026-03-09)
        // ==========================================
        private static void GenerateSet2Missions()
        {
            // A1: B106 의료공학연구실 찾기
            CreateMission("Set2_A1", new MissionParams {
                missionId = "A1",
                type = MissionType.A_DirectionVerify,
                briefing = "Find the Medical Engineering Research Lab. Upon arrival, check the room number.",
                targetWP = "WP02",
                question = "What is the room number of the lab you just arrived at?",
                options = new[] { "B105", "B106", "B107", "B108" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "의료공학연구실을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B105", "B106", "B107", "B108" }
            });

            // B1: 최세운 교수실 찾기 (T1 트리거 — 안내열화)
            CreateMission("Set2_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find Prof. Choe's office in this corridor.",
                targetWP = "WP03",
                question = "What is the room number of Prof. Choe's office?",
                options = new[] { "B103", "B104", "B105", "B106" },
                correctIndex = 1,
                triggerId = "T1",
                briefingKo = "이 복도에서 최 교수 연구실을 찾으세요.",
                questionKo = "최 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B103", "B104", "B105", "B106" }
            });

            // A2: B102 네트워크 기반 시스템 연구실(NSL) 찾기
            CreateMission("Set2_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "Find the Networked Systems Lab (NSL). Upon arrival, check the room number.",
                targetWP = "WP05",
                question = "What is the room number of the NSL?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "네트워크 기반 시스템 연구실(NSL)을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "NSL 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B101", "B102", "B103", "B104" }
            });

            // B2: 이재민 교수실 찾기 (T4 트리거 — 안내부재)
            CreateMission("Set2_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find Prof. Lee's office at the far end of this corridor.",
                targetWP = "WP06",
                question = "What is the room number of Prof. Lee's office?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 0,
                triggerId = "T4",
                briefingKo = "이 복도 끝에 있는 이 교수 연구실을 찾으세요.",
                questionKo = "이 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B101", "B102", "B103", "B104" }
            });

            // C1: B101 vs B102 비교 — 문 밖 게시물/포스터 수 비교
            // B101: Hanwha Systems + LGU+ 산학협력 간판 2개
            // B102: NSL 모집 포스터 + 취업현황 + 강의시간표 + 연구 논문 = 다수
            CreateMission("Set2_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "Check rooms B101 and B102. Compare the amount of notices, posters, and papers posted outside each room's door and wall.",
                targetWP = "WP07",
                question = "Which room has more notices and posters posted outside?",
                options = new[] { "B101 (Prof. Lee)", "B102 (NSL Lab)", "Both equal", "Neither has postings" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "B101과 B102를 확인하세요. 각 방 문과 벽에 게시된 안내문, 포스터, 자료의 양을 비교하세요.",
                questionKo = "문 밖에 게시물과 포스터가 더 많이 붙어있는 곳은 어디입니까?",
                optionsKo = new[] { "B101 (이교수실)", "B102 (NSL 연구실)", "둘 다 동일", "둘 다 없음" }
            });
        }

        // ==========================================
        // POIs (Set1, Set2 공용)
        // ==========================================
        private static void GenerateRouteBPOIs()
        {
            string path = "Assets/Data/POIs/RouteB";
            EnsureFolder(path);
            string resPath = "Assets/Resources/Data/POIs/RouteB";
            EnsureFolder(resPath);

            CreatePOI(path, "room_b121", "B121 Computer Lab", "computer_lab", "PC computer lab", 40, new[] { "Desktop PC", "Projector" },
                "B121 컴퓨터실", "PC 컴퓨터 실습실", new[] { "데스크톱 PC", "프로젝터" },
                mapPosition: new Vector2(36, -7));
            CreatePOI(path, "room_b116", "B116 Education Computer Room", "computer_lab", "Education computer room (Cloud Computing class)", 40, new[] { "Desktop PC", "Projector" },
                "B116 교육용 컴퓨터실", "교육용 컴퓨터실 (클라우드컴퓨팅 수업)", new[] { "데스크톱 PC", "프로젝터" },
                mapPosition: new Vector2(39, 48));
            CreatePOI(path, "room_b111", "B111 HAX Lab", "research_lab", "Human-centered AX (HAX) Lab + Industry-academia R&D platform", 10, new[] { "Workstation" },
                "B111 인간중심 AX 연구실", "인간중심 AX(HAX) 연구실 + 산학협력거점플랫폼 공동연구실", new[] { "워크스테이션" },
                mapPosition: new Vector2(36, 18));
            CreatePOI(path, "room_b107", "B107 Pervasive & Intelligent Computing Lab", "research_lab", "Pervasive and Intelligent Computing Lab + Smart Military Logistics Innovation Center", 10, new[] { "Server", "Workstation" },
                "B107 편재 및 지능형 컴퓨팅 연구실", "편재 및 지능형 컴퓨팅 연구실 + 스마트군수혁신융합연구센터", new[] { "서버", "워크스테이션" },
                mapPosition: new Vector2(36, 33));
            CreatePOI(path, "room_b106", "B106 Medical Engineering Research Lab", "research_lab", "Biomedical Engineering Dept. Medical Engineering Research Lab", 10, new[] { "Medical equipment", "Workstation" },
                "B106 의료공학연구실", "바이오메디컬공학과 의료공학연구실", new[] { "의료 장비", "워크스테이션" },
                mapPosition: new Vector2(36, 39));
            CreatePOI(path, "room_b104", "B104 Prof. Choe's Office", "professor_office", "Prof. Se-woon Choe's office (Biomedical Engineering)", 2, new string[0],
                "B104 최세운 교수 연구실", "최세운 교수 연구실 (바이오메디컬공학과)", null,
                mapPosition: new Vector2(36, 51));
            CreatePOI(path, "room_b105", "B105 Prof. Song's Office", "professor_office", "Prof. Kwang-Soup Song's office (Biomedical Engineering) + ICT Convergence Research Center", 2, new string[0],
                "B105 송광섭 교수 연구실", "송광섭 교수 연구실 (바이오메디컬공학과) + ICT 융합 특성화 연구센터", null,
                mapPosition: new Vector2(36, 45));
            CreatePOI(path, "room_b102", "B102 Networked Systems Lab (NSL)", "research_lab", "Networked Systems Lab (NSL) — network-based systems research", 10, new[] { "Network equipment", "Server" },
                "B102 네트워크 기반 시스템 연구실", "네트워크 기반 시스템 연구실 (NSL)", new[] { "네트워크 장비", "서버" },
                mapPosition: new Vector2(36, 63));
            CreatePOI(path, "room_b101", "B101 Prof. Lee's Office", "professor_office", "Prof. Jae-Min Lee's office (Computer Engineering) + Hanwha Systems Maritime Lab + LGU+ Smart Factory Center", 2, new string[0],
                "B101 이재민 교수 연구실", "이재민 교수 연구실 (컴퓨터공학과) + Hanwha Systems 해양연구소 + LGU+ 스마트팩토리 연구센터", null,
                mapPosition: new Vector2(36, 66));
            CreatePOI(path, "stairs_main_b", "Central Stairs", "stairs", "South central staircase", 0, new string[0],
                "중앙 계단", "남쪽 중앙 계단", null,
                mapPosition: new Vector2(0, 0));
        }

        // ==========================================
        // Helper Methods
        // ==========================================
        private struct MissionParams
        {
            public string missionId;
            public MissionType type;
            public string briefing;
            public string targetWP;
            public string question;
            public string[] options;
            public int correctIndex;
            public string triggerId;
            public string briefingKo;
            public string questionKo;
            public string[] optionsKo;
        }

        private static void CreateMission(string fileName, MissionParams p)
        {
            // Resources 폴더에 생성하여 런타임 Resources.Load() fallback 가능
            string path = $"Assets/Resources/Data/Missions/{fileName}.asset";
            EnsureFolder("Assets/Resources/Data/Missions");

            // 기존 Assets/Data/Missions/ 경로에도 호환용으로 생성/업데이트
            string legacyPath = $"Assets/Data/Missions/{fileName}.asset";
            EnsureFolder("Assets/Data/Missions");
            CreateOrUpdateMissionAsset(legacyPath, p);

            CreateOrUpdateMissionAsset(path, p);
        }

        private static void CreateOrUpdateMissionAsset(string path, MissionParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MissionData>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"  Created mission: {path}");
            }
            else
            {
                Debug.Log($"  Updated mission: {path}");
            }

            asset.missionId = p.missionId;
            asset.type = p.type;
            asset.briefingText = p.briefing;
            asset.targetWaypointId = p.targetWP;
            asset.verificationQuestion = p.question;
            asset.answerOptions = p.options;
            asset.correctAnswerIndex = p.correctIndex;
            asset.associatedTriggerId = p.triggerId;
            asset.briefingTextKo = p.briefingKo ?? "";
            asset.verificationQuestionKo = p.questionKo ?? "";
            asset.answerOptionsKo = p.optionsKo ?? new string[0];
            EditorUtility.SetDirty(asset);
        }

        private static void CreatePOI(string folder, string poiId, string displayName, string poiType,
            string description, int capacity, string[] equipment,
            string displayNameKo = "", string descriptionKo = "", string[] equipmentKo = null,
            Vector2 mapPosition = default)
        {
            string path = $"{folder}/{poiId}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<POIData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<POIData>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"  Created POI: {path}");
            }
            else
            {
                Debug.Log($"  Updated POI: {path}");
            }

            asset.poiId = poiId;
            asset.displayName = displayName;
            asset.poiType = poiType;
            asset.description = description;
            asset.capacity = capacity;
            asset.equipment = equipment;
            asset.displayNameKo = displayNameKo;
            asset.descriptionKo = descriptionKo;
            asset.equipmentKo = equipmentKo ?? new string[0];
            asset.mapPosition = mapPosition;
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
