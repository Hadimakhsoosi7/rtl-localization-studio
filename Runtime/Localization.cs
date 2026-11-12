using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace RTLLocalizationStudio
{
    public static class Localization
    {
        public static event Action LanguageChanged;
        private static LocalizationProfile profile;
        private static string requested;
        public static LocalizationProfile Profile => profile ? profile : (profile = Resources.Load<LocalizationProfile>("RTLLocalizationStudio"));
        public static string Language => LocalizationSettings.SelectedLocale ? LocalizationSettings.SelectedLocale.Identifier.Code : (Profile ? Profile.defaultLanguage : "fa");
        public static LanguageStyle Style => Profile ? Profile.Find(Language) : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            LocalizationSettings.SelectedLocaleChanged -= Changed;
            profile = null;
            requested = null;
            LanguageChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!Profile) return;
            LocalizationSettings.SelectedLocaleChanged += Changed;
            string saved = PlayerPrefs.GetString(Profile.preferenceKey, Profile.defaultLanguage);
            // A stale/removed saved code must not leave the game without a selected language.
            SetLanguage(Profile.Find(saved) != null ? saved : Profile.defaultLanguage, false);
        }

        public static void SetLanguage(string code) => SetLanguage(code, true);
        private static void SetLanguage(string code, bool save)
        {
            if (!Profile || Profile.Find(code) == null) return;
            requested = code;
            var initialization = LocalizationSettings.InitializationOperation;
            if (initialization.IsDone) Apply(code, save);
            else initialization.Completed += _ => { if (requested == code) Apply(code, save); };
        }

        private static void Apply(string code, bool save)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (!locale) return;
            LocalizationSettings.SelectedLocale = locale;
            if (save) { PlayerPrefs.SetString(Profile.preferenceKey, code); PlayerPrefs.Save(); }
        }
        private static void Changed(Locale _) => LanguageChanged?.Invoke();

        public static string FormatDigits(string value, DigitStyle style)
        {
            if (string.IsNullOrEmpty(value) || style == DigitStyle.Preserve) return value;
            var chars = value.ToCharArray();
            var start = style == DigitStyle.Persian ? '\u06f0' : style == DigitStyle.Arabic ? '\u0660' : '0';
            bool tag = false;
            for (int i = 0; i < chars.Length; i++)
            {
                // Only a '<' with a closing '>' is a rich-text tag; "5 < 6" must still be converted.
                if (chars[i] == '<' && Array.IndexOf(chars, '>', i + 1) > i) tag = true;
                if (!tag)
                {
                    int n = chars[i] >= '0' && chars[i] <= '9' ? chars[i] - '0' :
                        chars[i] >= '\u06f0' && chars[i] <= '\u06f9' ? chars[i] - '\u06f0' :
                        chars[i] >= '\u0660' && chars[i] <= '\u0669' ? chars[i] - '\u0660' : -1;
                    if (n >= 0) chars[i] = (char)(start + n);
                }
                if (chars[i] == '>') tag = false;
            }
            return new string(chars);
        }
    }
}
