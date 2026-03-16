using System;
using UnityEngine;

namespace ARNavExperiment.Core
{
    public enum Language { EN, KO }

    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public Language CurrentLanguage { get; private set; } = Language.EN;
        public event Action<Language> OnLanguageChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // 항상 영어로 시작 (실험 영상 녹화 시 한국어 노출 방지)
            CurrentLanguage = Language.EN;
        }

        public void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == Language.EN ? Language.KO : Language.EN);
        }

        public void SetLanguage(Language lang)
        {
            if (lang == CurrentLanguage) return;
            CurrentLanguage = lang;
            OnLanguageChanged?.Invoke(lang);
        }

        public static string Get(string key)
        {
            if (Instance == null) return LocalizationTable.GetEN(key);
            return Instance.CurrentLanguage == Language.KO
                ? LocalizationTable.GetKO(key)
                : LocalizationTable.GetEN(key);
        }
    }
}
