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
                briefing = "이 층에서 가장 큰 강의실(대강의실)을 찾아가세요. 도착 후 해당 강의실의 호실 번호를 확인하세요.",
                targetWP = "WP02",
                question = "방금 도착한 강의실의 호실 번호는 무엇입니까?",
                options = new[] { "B124", "B125", "B126", "B127" },
                correctIndex = 1,
                triggerId = ""
            });

            // B1: 시청각실 찾기 (T1 트리거)
            CreateMission("RouteA_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "시청각 장비가 갖춰진 공간을 찾아가세요.",
                targetWP = "WP03",
                question = "도착한 공간의 호실 번호는 무엇입니까?",
                options = new[] { "B125", "B126", "B127", "B128" },
                correctIndex = 2,
                triggerId = "T1"
            });

            // A2: 대학원 강의실 찾기
            CreateMission("RouteA_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "대학원 전용 강의실을 찾아가세요. 도착 후 강의실 문의 호실 번호를 확인하세요.",
                targetWP = "WP05",
                question = "방금 도착한 대학원 강의실의 호실 번호는 무엇입니까?",
                options = new[] { "B128", "B129", "B130", "B131" },
                correctIndex = 2,
                triggerId = ""
            });

            // B2: 세미나실 찾기 (T4 트리거)
            CreateMission("RouteA_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "세미나실을 찾아가세요.",
                targetWP = "WP06",
                question = "도착한 세미나실의 호실 번호는 무엇입니까?",
                options = new[] { "B131", "B132", "B133", "B134" },
                correctIndex = 2,
                triggerId = "T4"
            });

            // C1: B132 vs B133 비교
            CreateMission("RouteA_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "B132 강의실과 B133 세미나실을 각각 확인하세요. 10명 이하 소규모 그룹 토론에 더 적합한 공간의 호실 번호를 선택하세요.",
                targetWP = "WP08",
                question = "소규모 그룹 토론에 더 적합한 공간은?",
                options = new[] { "B132 (강의실)", "B133 (세미나실)", "둘 다 적합", "둘 다 부적합" },
                correctIndex = 1,
                triggerId = ""
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
                briefing = "이 층의 실습실 또는 연구실 중 하나를 찾아가세요. 도착 후 해당 공간의 호실 번호를 확인하세요.",
                targetWP = "WP02",
                question = "방금 도착한 공간의 호실 번호는 무엇입니까?",
                options = new[] { "B115", "B116", "B117", "B118" },
                correctIndex = 1,
                triggerId = ""
            });

            // B1: 교수 연구실 방향 (T2 트리거)
            CreateMission("RouteB_B1", new MissionParams {
                missionId = "B1",
                type = MissionType.B_AmbiguousDecision,
                briefing = "교수 연구실이 있는 방향으로 이동하세요.",
                targetWP = "WP03",
                question = "이동한 방향에서 처음 만난 교수실의 호실 번호는?",
                options = new[] { "B108", "B109", "B110", "B111" },
                correctIndex = 2,
                triggerId = "T2"
            });

            // A2: 지능 관련 연구실 찾기
            CreateMission("RouteB_A2", new MissionParams {
                missionId = "A2",
                type = MissionType.A_DirectionVerify,
                briefing = "지능 관련 연구를 수행하는 연구실을 찾아가세요. 도착 후 호실 번호를 확인하세요.",
                targetWP = "WP05",
                question = "방금 도착한 연구실의 호실 번호는 무엇입니까?",
                options = new[] { "B106", "B107", "B108", "B109" },
                correctIndex = 1,
                triggerId = ""
            });

            // B2: 교수실 찾기 (T3 트리거)
            CreateMission("RouteB_B2", new MissionParams {
                missionId = "B2",
                type = MissionType.B_AmbiguousDecision,
                briefing = "이 교차로 근처의 교수실을 찾아가세요.",
                targetWP = "WP06",
                question = "도착한 교수실의 호실 번호는 무엇입니까?",
                options = new[] { "B101", "B102", "B103", "B104" },
                correctIndex = 0,
                triggerId = "T3"
            });

            // C1: B104 vs B105 비교
            CreateMission("RouteB_C1", new MissionParams {
                missionId = "C1",
                type = MissionType.C_InfoIntegration,
                briefing = "B104 교수실과 B105 교수실을 각각 확인하세요. 두 교수실 중 문 앞 안내판에 연구실 정보가 더 상세하게 게시된 쪽의 호실 번호를 선택하세요.",
                targetWP = "WP08",
                question = "안내판 정보가 더 상세한 교수실은?",
                options = new[] { "B104", "B105", "둘 다 동일", "둘 다 없음" },
                correctIndex = 0,
                triggerId = ""
            });
        }

        // ==========================================
        // Route A POIs
        // ==========================================
        private static void GenerateRouteAPOIs()
        {
            string path = "Assets/Data/POIs/RouteA";
            EnsureFolder(path);

            CreatePOI(path, "room_b123", "B123 강의실", "lecture_room", "남쪽복도 강의실", 40, new[] { "빔프로젝터", "화이트보드" });
            CreatePOI(path, "room_b125", "B125 대강의실", "large_lecture", "지하1층 최대 규모 강의실", 80, new[] { "빔프로젝터", "마이크", "스크린" });
            CreatePOI(path, "room_b127", "B127 시청각실", "av_room", "시청각 장비 갖춘 다목적 공간", 50, new[] { "대형 스크린", "음향 시스템", "빔프로젝터" });
            CreatePOI(path, "room_b128", "B128 학생상담실", "counseling", "학생 상담 공간", 5, new string[0]);
            CreatePOI(path, "room_b129", "B129 대학원강의실", "grad_lecture", "대학원 전용 강의실", 30, new[] { "빔프로젝터" });
            CreatePOI(path, "room_b130", "B130 대학원강의실", "grad_lecture", "대학원 전용 강의실", 30, new[] { "빔프로젝터", "화이트보드" });
            CreatePOI(path, "room_b131", "B131 강의실", "lecture_room", "서쪽복도 상단 강의실", 40, new[] { "빔프로젝터" });
            CreatePOI(path, "room_b132", "B132 강의실", "lecture_room", "북쪽복도 강의실 (대규모)", 60, new[] { "빔프로젝터", "마이크" });
            CreatePOI(path, "room_b133", "B133 세미나실", "seminar_room", "소규모 세미나/토론용 공간", 15, new[] { "화이트보드", "모니터" });
            CreatePOI(path, "restroom_w", "여자화장실", "restroom", "B128 부근 화장실", 0, new string[0]);
            CreatePOI(path, "stairs_main", "중앙 계단", "stairs", "남쪽 중앙 계단", 0, new string[0]);
        }

        // ==========================================
        // Route B POIs
        // ==========================================
        private static void GenerateRouteBPOIs()
        {
            string path = "Assets/Data/POIs/RouteB";
            EnsureFolder(path);

            CreatePOI(path, "room_b121", "B121 PC실습실", "computer_lab", "PC 실습실", 40, new[] { "데스크탑 PC", "빔프로젝터" });
            CreatePOI(path, "room_b116", "B116", "tbd", "동쪽복도 남단 (현장 확인 필요)", 0, new string[0]);
            CreatePOI(path, "room_b110", "B110", "tbd", "동쪽복도 중단 (현장 확인 필요)", 0, new string[0]);
            CreatePOI(path, "room_b107", "B107 전산지능연구실", "research_lab", "전산지능 연구실", 10, new[] { "서버", "워크스테이션" });
            CreatePOI(path, "room_b106", "B106 지능공학연구실", "research_lab", "지능공학 연구실", 10, new[] { "서버", "워크스테이션" });
            CreatePOI(path, "room_b104", "B104 최태훈 교수실", "professor_office", "최태훈 교수 연구실", 2, new string[0]);
            CreatePOI(path, "room_b105", "B105 송영민 교수실", "professor_office", "송영민 교수 연구실", 2, new string[0]);
            CreatePOI(path, "room_b102", "B102 네트워크연구실", "research_lab", "네트워크 연구실", 10, new[] { "네트워크 장비" });
            CreatePOI(path, "room_b101", "B101 이재민 교수실", "professor_office", "이재민 교수 연구실", 2, new string[0]);
            CreatePOI(path, "stairs_main_b", "중앙 계단", "stairs", "남쪽 중앙 계단", 0, new string[0]);
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
        }

        private static void CreateMission(string fileName, MissionParams p)
        {
            string path = $"Assets/Data/Missions/{fileName}.asset";
            EnsureFolder("Assets/Data/Missions");

            var asset = ScriptableObject.CreateInstance<MissionData>();
            asset.missionId = p.missionId;
            asset.type = p.type;
            asset.briefingText = p.briefing;
            asset.targetWaypointId = p.targetWP;
            asset.verificationQuestion = p.question;
            asset.answerOptions = p.options;
            asset.correctAnswerIndex = p.correctIndex;
            asset.associatedTriggerId = p.triggerId;

            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"  Created mission: {path}");
        }

        private static void CreatePOI(string folder, string poiId, string displayName, string poiType, string description, int capacity, string[] equipment)
        {
            string path = $"{folder}/{poiId}.asset";

            var asset = ScriptableObject.CreateInstance<POIData>();
            asset.poiId = poiId;
            asset.displayName = displayName;
            asset.poiType = poiType;
            asset.description = description;
            asset.capacity = capacity;
            asset.equipment = equipment;

            AssetDatabase.CreateAsset(asset, path);
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
