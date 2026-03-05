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

        private const string PREF_KEY = "ARNav_Language";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            string saved = PlayerPrefs.GetString(PREF_KEY, "EN");
            CurrentLanguage = saved == "KO" ? Language.KO : Language.EN;
        }

        public void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == Language.EN ? Language.KO : Language.EN);
        }

        public void SetLanguage(Language lang)
        {
            if (lang == CurrentLanguage) return;
            CurrentLanguage = lang;
            PlayerPrefs.SetString(PREF_KEY, lang.ToString());
            PlayerPrefs.Save();
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
