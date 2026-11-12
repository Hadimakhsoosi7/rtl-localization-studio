using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace RTLLocalizationStudio.Editor
{
    public static class LocalizationWorkspace
    {
        public const string Root = "Assets/Localization";
        public static readonly string[] Codes = LanguageCatalog.Codes;
        public static LocalizationProfile Profile => AssetDatabase.LoadAssetAtPath<LocalizationProfile>(Root + "/Resources/RTLLocalizationStudio.asset");
        public static StringTableCollection Collection => LocalizationEditorSettings.GetStringTableCollection(Profile ? Profile.table : "Game");
        public static StringTable Table(string code) => Collection ? Collection.GetTable(code) as StringTable : null;
        public static string Value(string key, string code) => Table(code)?.GetEntry(key)?.Value ?? "";

        public static void Setup()
        {
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Locales");
            Directory.CreateDirectory(Root + "/Tables");
            AssetDatabase.Refresh();
            if (!Profile)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LocalizationProfile>(), Root + "/Resources/RTLLocalizationStudio.asset");
            var styles = Profile.languages.ToList();
            foreach (string code in Codes)
                if (!styles.Any(x => x.code == code)) styles.Add(new LanguageStyle { code = code, rightToLeft = LanguageCatalog.IsRightToLeft(code) });
            Profile.languages = styles.ToArray();
            EditorUtility.SetDirty(Profile);
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
                settings.GetStartupLocaleSelectors().Clear();
                settings.GetStartupLocaleSelectors().Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = Profile.preferenceKey });
                settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = "fa" });
                EditorUtility.SetDirty(settings);
            }
            settings.GetStringDatabase().UseFallback = true;
            EditorUtility.SetDirty(settings);
            foreach (string code in Codes)
            {
                var locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(x => x.Identifier.Code == code);
                if (locale) continue;
                locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, Root + "/Locales/" + code + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
            var fa = LocalizationEditorSettings.GetLocales().First(x => x.Identifier.Code == "fa");
            foreach (var locale in LocalizationEditorSettings.GetLocales().Where(x => Codes.Contains(x.Identifier.Code) && x.Identifier.Code != "fa"))
            {
                if (locale.Metadata.GetMetadata<FallbackLocale>() == null)
                {
                    locale.Metadata.AddMetadata(new FallbackLocale(fa));
                    EditorUtility.SetDirty(locale);
                }
            }
            if (!Collection) LocalizationEditorSettings.CreateStringTableCollection(Profile.table, Root + "/Tables",
                LocalizationEditorSettings.GetLocales().Where(x => Codes.Contains(x.Identifier.Code)).ToList());
            foreach (string code in Codes) if (!Table(code)) Collection.AddNewTable(code);
            Collection.SetPreloadTableFlag(true);
            Profile.schemaVersion = LanguageCatalog.SchemaVersion;
            AssetDatabase.SaveAssets();
        }

        public static void SetValue(string key, string code, string value, bool smart = false)
        {
            var table = Table(code);
            if (!table) throw new InvalidOperationException("Run project setup first.");
            Undo.RecordObject(Collection.SharedData, "Edit translation keys");
            Undo.RecordObject(table, "Edit translation");
            var entry = table.AddEntry(key, value);
            entry.IsSmart = smart;
            EditorUtility.SetDirty(Collection.SharedData);
            EditorUtility.SetDirty(table);
        }
        public static string SourceHash(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "").Substring(0, 16);
        }
    }
}
