using UnityEngine;
using UnityEditor;

namespace ARNavExperiment.EditorTools
{
    /// <summary>
    /// ARNav 에디터 메뉴 통합 도구.
    /// 13개 개별 메뉴 → 3개 마스터 메뉴로 통합.
    /// 개별 메뉴는 그대로 유지되며, 마스터 메뉴는 순서대로 일괄 실행.
    /// </summary>
    public class MasterSetupTool
    {
        [MenuItem("ARNav/--- Master Setup ---/1. Full Setup (전체 구성)", false, 0)]
        public static void FullSetup()
        {
            if (!EditorUtility.DisplayDialog("전체 구성 실행",
                "다음 단계를 순서대로 실행합니다:\n\n" +
                "1. Setup Main Scene (ExperimenterCanvas 포함)\n" +
                "2. Setup XR Origin (XREAL) + 핸드트래킹\n" +
                "3. Generate Mission Data\n" +
                "4. Wire Mission Data to Scene\n" +
                "5. Generate InfoCard & Comparison Data\n" +
                "6. Configure Waypoint Routes\n" +
                "7. Cleanup Flow UI\n" +
                "8. Setup Experiment Flow UI (ExperimenterCanvas에 배치)\n" +
                "9. Wire Scene References\n" +
                "10. Setup Korean Font\n\n" +
                "계속하시겠습니까?", "실행", "취소"))
                return;

            int step = 0;
            int total = 10;

            try
            {
                // 1. Setup Main Scene (기존 오브젝트 자동 정리 포함)
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Setup Main Scene...", (float)step / total);
                SceneSetupTool.SetupMainSceneSilent();

                // 2. Setup XR Origin
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Setup XR Origin...", (float)step / total);
                XROriginSetupTool.SetupXROriginSilent();

                // 3. Generate Mission Data
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Generate Mission Data...", (float)step / total);
                MissionDataGenerator.GenerateAll();

                // 4. Wire Mission Data
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Wire Mission Data...", (float)step / total);
                MissionWiringTool.WireMissionData();

                // 5. Generate InfoCard Data
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Generate InfoCard Data...", (float)step / total);
                InfoCardDataGenerator.GenerateAll();

                // 6. Configure Waypoint Routes
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Configure Waypoint Routes...", (float)step / total);
                WaypointDataGenerator.ConfigureRoutes();

                // 7. Cleanup Flow UI
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Cleanup Flow UI...", (float)step / total);
                FlowUISetupTool.CleanupFlowUI();

                // 8. Setup Flow UI (매핑/실험 모드 포함)
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Setup Experiment Flow UI...", (float)step / total);
                FlowUISetupTool.SetupFlowUI();

                // 9. Wire Scene References
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Wire Scene References...", (float)step / total);
                SceneWiringTool.WireReferencesSilent();

                // 10. Korean Font
                step++;
                EditorUtility.DisplayProgressBar("Full Setup", $"[{step}/{total}] Setup Korean Font...", (float)step / total);
                KoreanFontSetup.SetupKoreanFont();

                EditorUtility.ClearProgressBar();
                Debug.Log("[MasterSetup] 전체 구성 완료!");
                EditorUtility.DisplayDialog("전체 구성 완료",
                    $"총 {total}단계 모두 완료되었습니다.\n\n" +
                    "씬을 저장하세요 (Cmd+S)\n\n" +
                    "다음 단계:\n" +
                    "- 에디터 테스트: Play 모드 실행\n" +
                    "- 빌드: ARNav → Build & Validate", "확인");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[MasterSetup] 단계 {step}에서 오류 발생: {e.Message}");
                EditorUtility.DisplayDialog("오류",
                    $"단계 {step}/{total}에서 오류가 발생했습니다.\n\n{e.Message}\n\n" +
                    "나머지 단계를 개별 메뉴에서 수동으로 실행하세요.", "확인");
            }
        }

        [MenuItem("ARNav/--- Master Setup ---/2. Build && Validate (빌드 검증)", false, 1)]
        public static void BuildAndValidate()
        {
            if (!EditorUtility.DisplayDialog("빌드 검증",
                "빌드 설정을 검증하고 카운터밸런스 설정을 생성합니다.\n\n" +
                "1. Validate Build Settings\n" +
                "2. Generate Counterbalance Config\n\n" +
                "계속하시겠습니까?", "실행", "취소"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Build & Validate", "빌드 설정 검증...", 0.5f);
                ExperimentConfigTool.ValidateBuildSettings();

                EditorUtility.DisplayProgressBar("Build & Validate", "카운터밸런스 설정...", 1f);
                ExperimentConfigTool.GenerateCounterbalanceConfig();

                EditorUtility.ClearProgressBar();
                Debug.Log("[MasterSetup] 빌드 검증 완료!");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[MasterSetup] 빌드 검증 오류: {e.Message}");
            }
        }

        [MenuItem("ARNav/--- Master Setup ---/3. Editor Test Mode (에디터 테스트)", false, 2)]
        public static void EditorTestMode()
        {
            if (!EditorUtility.DisplayDialog("에디터 테스트 모드",
                "에디터 테스트 환경을 구성합니다.\n\n" +
                "WASD: 이동\n" +
                "우클릭: 시점 회전\n" +
                "N: 상태 전환\n" +
                "M: 미션 시작\n\n" +
                "계속하시겠습니까?", "실행", "취소"))
                return;

            try
            {
                DebugToolsSetup.SetupTestMode();
                Debug.Log("[MasterSetup] 에디터 테스트 모드 구성 완료!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MasterSetup] 테스트 모드 오류: {e.Message}");
            }
        }
    }
}
