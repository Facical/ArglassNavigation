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
            GenerateRouteAMissions();
            GenerateRouteBMissions();
            GenerateRouteAPOIs();
            GenerateRouteBPOIs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MissionDataGenerator] 모든 미션/POI 데이터 생성 완료!");
            EditorUtility.DisplayDialog("완료", "Route A/B 미션 데이터와 POI 데이터가 생성되었습니다.\n\nAssets/Data/ 폴더를 확인하세요.", "확인");
        }

        // ==========================================
        // Route A Missions (서쪽-북쪽 루프)
        // ==========================================
        private static void GenerateRouteAMissions()
        {
            // A1: 대강의실 찾기
            CreateMission("RouteA_A1", new MissionParams {
                missionId = "A1",
                type = MissionType.A_DirectionVerify,
                briefing = "Find the largest classroom (main lecture hall) on this floor. Upon arrival, check the room number.",
                targetWP = "WP02",
                question = "What is the room number of the classroom you just arrived at?",
                options = new[] { "B124", "B125", "B126", "B127" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "이 층에서 가장 큰 강의실(대강의실)을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 강의실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B124", "B125", "B126", "B127" }
            });

            // B1: 시청각실 찾기 (T1 트리거)
            CreateMission("RouteA_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find a room equipped with audio-visual facilities.",
                targetWP = "WP03",
                question = "What is the room number of the space you arrived at?",
                options = new[] { "B125", "B126", "B127", "B128" },
                correctIndex = 2,
                triggerId = "T1",
                briefingKo = "시청각 설비가 갖춰진 공간을 찾으세요.",
                questionKo = "도착한 공간의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B125", "B126", "B127", "B128" }
            });

            // A2: 대학원 강의실 찾기
            CreateMission("RouteA_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "Find a graduate-only classroom. Upon arrival, check the room number on the door.",
                targetWP = "WP05",
                question = "What is the room number of the graduate classroom you just arrived at?",
                options = new[] { "B128", "B129", "B130", "B131" },
                correctIndex = 2,
                triggerId = "",
                briefingKo = "대학원 전용 강의실을 찾으세요. 도착하면 문에 적힌 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 대학원 강의실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B128", "B129", "B130", "B131" }
            });

            // B2: 세미나실 찾기 (T4 트리거)
            CreateMission("RouteA_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find the seminar room.",
                targetWP = "WP06",
                question = "What is the room number of the seminar room you arrived at?",
                options = new[] { "B131", "B132", "B133", "B134" },
                correctIndex = 2,
                triggerId = "T4",
                briefingKo = "세미나실을 찾으세요.",
                questionKo = "도착한 세미나실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B131", "B132", "B133", "B134" }
            });

            // C1: B132 vs B133 비교
            CreateMission("RouteA_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "Check rooms B132 (classroom) and B133 (seminar room). Select the room number more suitable for a small group discussion of 10 or fewer.",
                targetWP = "WP08",
                question = "Which space is more suitable for a small group discussion?",
                options = new[] { "B132 (Classroom)", "B133 (Seminar Room)", "Both suitable", "Neither suitable" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "B132(강의실)와 B133(세미나실)을 확인하세요. 10명 이하 소그룹 토론에 더 적합한 호실 번호를 선택하세요.",
                questionKo = "소그룹 토론에 더 적합한 공간은 어디입니까?",
                optionsKo = new[] { "B132 (강의실)", "B133 (세미나실)", "둘 다 적합", "둘 다 부적합" }
            });
        }

        // ==========================================
        // Route B Missions (동쪽-북쪽 루프)
        // ==========================================
        private static void GenerateRouteBMissions()
        {
            // A1: 연구실 찾기
            CreateMission("RouteB_A1", new MissionParams {
                missionId = "A1",
                type = MissionType.A_DirectionVerify,
                briefing = "Find one of the labs or research rooms on this floor. Upon arrival, check the room number.",
                targetWP = "WP02",
                question = "What is the room number of the space you just arrived at?",
                options = new[] { "B115", "B116", "B117", "B118" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "이 층의 연구실 또는 실험실을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 공간의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B115", "B116", "B117", "B118" }
            });

            // B1: 교수 연구실 방향 (T2 트리거)
            CreateMission("RouteB_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Move toward the direction of the professors' offices.",
                targetWP = "WP03",
                question = "What is the room number of the first professor's office you encountered?",
                options = new[] { "B108", "B109", "B110", "B111" },
                correctIndex = 2,
                triggerId = "T2",
                briefingKo = "교수 연구실 방향으로 이동하세요.",
                questionKo = "처음 만난 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B108", "B109", "B110", "B111" }
            });

            // A2: 지능 관련 연구실 찾기
            CreateMission("RouteB_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "Find a research lab focused on intelligence-related studies. Upon arrival, check the room number.",
                targetWP = "WP05",
                question = "What is the room number of the research lab you just arrived at?",
                options = new[] { "B106", "B107", "B108", "B109" },
                correctIndex = 1,
                triggerId = "",
                briefingKo = "지능 관련 연구를 수행하는 연구실을 찾으세요. 도착하면 호실 번호를 확인하세요.",
                questionKo = "방금 도착한 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B106", "B107", "B108", "B109" }
            });

            // B2: 교수실 찾기 (T3 트리거)
            CreateMission("RouteB_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "Find a professor's office near this junction.",
                targetWP = "WP06",
                question = "What is the room number of the professor's office you arrived at?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 0,
                triggerId = "T3",
                briefingKo = "이 교차로 근처에서 교수 연구실을 찾으세요.",
                questionKo = "도착한 교수 연구실의 호실 번호는 무엇입니까?",
                optionsKo = new[] { "B101", "B102", "B103", "B104" }
            });

            // C1: B104 vs B105 비교
            CreateMission("RouteB_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "Check rooms B104 and B105 (professor offices). Select the one with more detailed lab information posted on the door sign.",
                targetWP = "WP08",
                question = "Which professor's office has more detailed sign info?",
                options = new[] { "B104", "B105", "Both equal", "Neither has info" },
                correctIndex = 0,
                triggerId = "",
                briefingKo = "B104와 B105(교수 연구실)를 확인하세요. 문패에 더 상세한 연구실 정보가 있는 쪽을 선택하세요.",
                questionKo = "어느 교수 연구실의 문패 정보가 더 상세합니까?",
                optionsKo = new[] { "B104", "B105", "둘 다 동일", "둘 다 정보 없음" }
            });
        }

        // ==========================================
        // Route A POIs
        // ==========================================
        private static void GenerateRouteAPOIs()
        {
            string path = "Assets/Data/POIs/RouteA";
            EnsureFolder(path);
            string resPath = "Assets/Resources/Data/POIs/RouteA";
            EnsureFolder(resPath);

            CreatePOI(path, "room_b123", "B123 Classroom", "lecture_room", "South corridor classroom", 40, new[] { "Projector", "Whiteboard" },
                "B123 강의실", "남쪽 복도 강의실", new[] { "프로젝터", "화이트보드" },
                mapPosition: new Vector2(-7, 0));
            CreatePOI(path, "room_b125", "B125 Main Lecture Hall", "large_lecture", "Largest classroom on B1F", 80, new[] { "Projector", "Microphone", "Screen" },
                "B125 대강의실", "B1F 최대 규모 강의실", new[] { "프로젝터", "마이크", "스크린" },
                mapPosition: new Vector2(-20, 4));
            CreatePOI(path, "room_b127", "B127 AV Room", "av_room", "Multi-purpose room with AV equipment", 50, new[] { "Large Screen", "Audio System", "Projector" },
                "B127 시청각실", "시청각 설비가 갖춰진 다목적실", new[] { "대형 스크린", "음향 시스템", "프로젝터" },
                mapPosition: new Vector2(-20, 0));
            CreatePOI(path, "room_b128", "B128 Student Counseling", "counseling", "Student counseling room", 5, new string[0],
                "B128 학생 상담실", "학생 상담실", null,
                mapPosition: new Vector2(-20, 7));
            CreatePOI(path, "room_b129", "B129 Graduate Classroom", "grad_lecture", "Graduate-only classroom", 30, new[] { "Projector" },
                "B129 대학원 강의실", "대학원 전용 강의실", new[] { "프로젝터" },
                mapPosition: new Vector2(-20, 11));
            CreatePOI(path, "room_b130", "B130 Graduate Classroom", "grad_lecture", "Graduate-only classroom", 30, new[] { "Projector", "Whiteboard" },
                "B130 대학원 강의실", "대학원 전용 강의실", new[] { "프로젝터", "화이트보드" },
                mapPosition: new Vector2(-20, 14));
            CreatePOI(path, "room_b131", "B131 Classroom", "lecture_room", "Upper west corridor classroom", 40, new[] { "Projector" },
                "B131 강의실", "서쪽 복도 상부 강의실", new[] { "프로젝터" },
                mapPosition: new Vector2(-20, 18));
            CreatePOI(path, "room_b132", "B132 Classroom", "lecture_room", "North corridor classroom (large)", 60, new[] { "Projector", "Microphone" },
                "B132 강의실", "북쪽 복도 대형 강의실", new[] { "프로젝터", "마이크" },
                mapPosition: new Vector2(-11, 25));
            CreatePOI(path, "room_b133", "B133 Seminar Room", "seminar_room", "Small seminar/discussion room", 15, new[] { "Whiteboard", "Monitor" },
                "B133 세미나실", "소규모 세미나/토론실", new[] { "화이트보드", "모니터" },
                mapPosition: new Vector2(-3, 25));
            CreatePOI(path, "restroom_w", "Women's Restroom", "restroom", "Restroom near B128", 0, new string[0],
                "여자 화장실", "B128 근처 화장실", null,
                mapPosition: new Vector2(-20, 8));
            CreatePOI(path, "stairs_main", "Central Stairs", "stairs", "South central staircase", 0, new string[0],
                "중앙 계단", "남쪽 중앙 계단", null,
                mapPosition: new Vector2(0, -5));
        }

        // ==========================================
        // Route B POIs
        // ==========================================
        private static void GenerateRouteBPOIs()
        {
            string path = "Assets/Data/POIs/RouteB";
            EnsureFolder(path);
            string resPath = "Assets/Resources/Data/POIs/RouteB";
            EnsureFolder(resPath);

            CreatePOI(path, "room_b121", "B121 Computer Lab", "computer_lab", "PC computer lab", 40, new[] { "Desktop PC", "Projector" },
                "B121 컴퓨터실", "PC 컴퓨터 실습실", new[] { "데스크톱 PC", "프로젝터" },
                mapPosition: new Vector2(7, 0));
            CreatePOI(path, "room_b116", "B116", "tbd", "East corridor south end (TBD on-site)", 0, new string[0],
                "B116", "동쪽 복도 남단 (현장 확인 필요)", null,
                mapPosition: new Vector2(20, 4));
            CreatePOI(path, "room_b110", "B110", "tbd", "East corridor mid-section (TBD on-site)", 0, new string[0],
                "B110", "동쪽 복도 중간 (현장 확인 필요)", null,
                mapPosition: new Vector2(20, 11));
            CreatePOI(path, "room_b107", "B107 Computational Intelligence Lab", "research_lab", "Computational intelligence lab", 10, new[] { "Server", "Workstation" },
                "B107 전산지능연구실", "전산지능 연구실", new[] { "서버", "워크스테이션" },
                mapPosition: new Vector2(20, 15));
            CreatePOI(path, "room_b106", "B106 Intelligent Engineering Lab", "research_lab", "Intelligent engineering lab", 10, new[] { "Server", "Workstation" },
                "B106 지능공학연구실", "지능공학 연구실", new[] { "서버", "워크스테이션" },
                mapPosition: new Vector2(20, 18));
            CreatePOI(path, "room_b104", "B104 Prof. Choi's Office", "professor_office", "Prof. Taehoon Choi's office", 2, new string[0],
                "B104 최교수 연구실", "최태훈 교수 연구실", null,
                mapPosition: new Vector2(11, 25));
            CreatePOI(path, "room_b105", "B105 Prof. Song's Office", "professor_office", "Prof. Youngmin Song's office", 2, new string[0],
                "B105 송교수 연구실", "송영민 교수 연구실", null,
                mapPosition: new Vector2(3, 25));
            CreatePOI(path, "room_b102", "B102 Network Lab", "research_lab", "Network research lab", 10, new[] { "Network equipment" },
                "B102 네트워크 연구실", "네트워크 연구실", new[] { "네트워크 장비" },
                mapPosition: new Vector2(20, 22));
            CreatePOI(path, "room_b101", "B101 Prof. Lee's Office", "professor_office", "Prof. Jaemin Lee's office", 2, new string[0],
                "B101 이교수 연구실", "이재민 교수 연구실", null,
                mapPosition: new Vector2(20, 25));
            CreatePOI(path, "stairs_main_b", "Central Stairs", "stairs", "South central staircase", 0, new string[0],
                "중앙 계단", "남쪽 중앙 계단", null,
                mapPosition: new Vector2(0, -5));
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
