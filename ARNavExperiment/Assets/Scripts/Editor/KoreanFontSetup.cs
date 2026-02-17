using UnityEngine;
using UnityEditor;
using TMPro;

namespace ARNavExperiment.EditorTools
{
    public class KoreanFontSetup
    {
        [MenuItem("ARNav/Setup Korean Font (TMP)")]
        public static void SetupKoreanFont()
        {
            // 1. Load the Korean font
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/AppleGothic.ttf");
            if (font == null)
            {
                EditorUtility.DisplayDialog("에러",
                    "Assets/Fonts/AppleGothic.ttf를 찾을 수 없습니다.", "확인");
                return;
            }

            // 2. Create TMP Font Asset (dynamic)
            string fontAssetPath = "Assets/Fonts/AppleGothic SDF.asset";
            var existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);

            TMP_FontAsset fontAsset;
            if (existingAsset != null)
            {
                fontAsset = existingAsset;
                Debug.Log("[KoreanFontSetup] 기존 폰트 에셋 재사용");
            }
            else
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(font);
                if (fontAsset == null)
                {
                    EditorUtility.DisplayDialog("에러", "TMP Font Asset 생성 실패", "확인");
                    return;
                }
                fontAsset.name = "AppleGothic SDF";
                AssetDatabase.CreateAsset(fontAsset, fontAssetPath);

                // Save material as sub-asset
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "AppleGothic SDF Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAssetPath);
                }

                // Save atlas texture as sub-asset
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "AppleGothic SDF Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAssetPath);
                }

                AssetDatabase.SaveAssets();
                Debug.Log("[KoreanFontSetup] TMP Font Asset 생성 완료");
            }

            // 3. Set as TMP default font
            var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
            if (tmpSettings != null)
            {
                var so = new SerializedObject(tmpSettings);
                var defaultProp = so.FindProperty("m_defaultFontAsset");
                if (defaultProp != null)
                {
                    defaultProp.objectReferenceValue = fontAsset;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tmpSettings);
                    Debug.Log("[KoreanFontSetup] TMP 기본 폰트 변경 완료");
                }
            }
            else
            {
                Debug.LogWarning("[KoreanFontSetup] TMP Settings를 찾을 수 없습니다. 수동으로 설정해주세요.");
            }

            // 4. Update all existing TMP texts in scene
            int updated = 0;
            var allTMP = Object.FindObjectsOfType<TextMeshProUGUI>(true);
            foreach (var tmp in allTMP)
            {
                if (tmp.font == null || tmp.font.name.Contains("LiberationSans"))
                {
                    tmp.font = fontAsset;
                    EditorUtility.SetDirty(tmp);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("완료",
                $"한국어 폰트 설정 완료!\n\n" +
                $"- AppleGothic SDF 폰트 에셋 생성\n" +
                $"- TMP 기본 폰트 변경\n" +
                $"- 씬 내 {updated}개 텍스트 폰트 교체\n\n" +
                "씬을 저장하세요 (Cmd+S)", "확인");
        }
    }
}
