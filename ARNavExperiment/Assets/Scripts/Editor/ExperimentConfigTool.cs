using UnityEngine;
using UnityEditor;
using ARNavExperiment.Utils;

namespace ARNavExperiment.EditorTools
{
    public class ExperimentConfigTool
    {
        [MenuItem("ARNav/Generate Counterbalance Config")]
        public static void GenerateCounterbalanceConfig()
        {
            string folder = "Assets/Data";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Data");

            string path = $"{folder}/CounterbalanceConfig.asset";

            var existing = AssetDatabase.LoadAssetAtPath<CounterbalanceConfig>(path);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("이미 존재",
                    "CounterbalanceConfig가 이미 있습니다. 덮어쓰시겠습니까?", "덮어쓰기", "취소"))
                    return;
                AssetDatabase.DeleteAsset(path);
            }

            var config = ScriptableObject.CreateInstance<CounterbalanceConfig>();
            config.assignments = new GroupAssignment[24];

            // 24명: S1/S2 교대 배정 (12명씩)
            // S1 = Glass Only → Hybrid
            // S2 = Hybrid → Glass Only
            // 홀수 번호: Route A 먼저, 짝수 번호: Route B 먼저
            for (int i = 0; i < 24; i++)
            {
                int pNum = i + 1;
                config.assignments[i] = new GroupAssignment
                {
                    participantId = $"P{pNum:D2}",
                    orderGroup = (pNum % 2 == 1) ? "S1" : "S2"
                };
            }

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Debug.Log("[ExperimentConfig] CounterbalanceConfig 생성 완료!");
            EditorUtility.DisplayDialog("완료",
                "CounterbalanceConfig 생성 완료!\n\n" +
                "24명 참가자 배정:\n" +
                "- S1 (Glass→Hybrid): P01, P03, P05, ... P23\n" +
                "- S2 (Hybrid→Glass): P02, P04, P06, ... P24\n" +
                "- 홀수: Route A 먼저 / 짝수: Route B 먼저\n\n" +
                "Assets/Data/CounterbalanceConfig.asset", "확인");
        }

        [MenuItem("ARNav/Validate Build Settings")]
        public static void ValidateBuildSettings()
        {
            var issues = new System.Text.StringBuilder();
            int issueCount = 0;

            // 1. Platform
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                issues.AppendLine("[ ] 빌드 타겟이 Android가 아닙니다.");
                issues.AppendLine("    → File > Build Settings > Android > Switch Platform");
                issueCount++;
            }
            else
            {
                issues.AppendLine("[O] 빌드 타겟: Android");
            }

            // 2. Graphics API
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
            {
                issues.AppendLine("[O] Graphics API: Auto");
            }
            else
            {
                var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                bool hasVulkan = false;
                foreach (var api in apis)
                    if (api == UnityEngine.Rendering.GraphicsDeviceType.Vulkan) hasVulkan = true;
                if (!hasVulkan)
                {
                    issues.AppendLine("[ ] Vulkan이 Graphics API에 없습니다.");
                    issues.AppendLine("    → Player Settings > Graphics APIs > Vulkan 추가");
                    issueCount++;
                }
                else
                {
                    issues.AppendLine("[O] Graphics API: Vulkan 포함");
                }
            }

            // 3. Architecture
            if (PlayerSettings.Android.targetArchitectures != UnityEditor.AndroidArchitecture.ARM64)
            {
                issues.AppendLine("[ ] Target Architecture가 ARM64가 아닙니다.");
                issues.AppendLine("    → Player Settings > Target Architectures > ARM64만 선택");
                issueCount++;
            }
            else
            {
                issues.AppendLine("[O] Architecture: ARM64");
            }

            // 4. Minimum API Level
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel31)
            {
                issues.AppendLine($"[ ] Min API Level이 31 미만입니다 ({PlayerSettings.Android.minSdkVersion}).");
                issues.AppendLine("    → Player Settings > Minimum API Level > 31");
                issueCount++;
            }
            else
            {
                issues.AppendLine($"[O] Min API Level: {PlayerSettings.Android.minSdkVersion}");
            }

            // 5. Scripting Backend
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            {
                issues.AppendLine("[ ] Scripting Backend이 IL2CPP가 아닙니다.");
                issues.AppendLine("    → Player Settings > Scripting Backend > IL2CPP");
                issueCount++;
            }
            else
            {
                issues.AppendLine("[O] Scripting Backend: IL2CPP");
            }

            // 6. Color Space
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                issues.AppendLine("[ ] Color Space가 Linear가 아닙니다.");
                issues.AppendLine("    → Player Settings > Color Space > Linear");
                issueCount++;
            }
            else
            {
                issues.AppendLine("[O] Color Space: Linear");
            }

            // 7. Input System
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            if (!defines.Contains("ENABLE_INPUT_SYSTEM"))
            {
                issues.AppendLine("[ ] ENABLE_INPUT_SYSTEM이 Scripting Define에 없습니다.");
                issueCount++;
            }
            else
            {
                issues.AppendLine("[O] Input System: 활성화됨");
            }

            // 8. XR Plugin
            issues.AppendLine("\n[수동 확인 필요]");
            issues.AppendLine("- XR Plug-in Management > Android > XREAL 활성화 여부");
            issues.AppendLine("- XREAL SDK 3.1.0 설치 여부");

            string title = issueCount == 0 ? "빌드 설정 검증 통과!" : $"빌드 설정 문제 {issueCount}개 발견";
            EditorUtility.DisplayDialog(title, issues.ToString(), "확인");
            Debug.Log($"[BuildValidation] {title}\n{issues}");
        }
    }
}
