using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RTLLocalizationStudio.Editor
{
    public static class LocalizationValidation
    {
        [InitializeOnLoadMethod]
        private static void RequestCheck()
        {
            EditorApplication.delayCall += () =>
            {
                const string request = "Temp/RTLLocalizationStudio.validate";
                if (!File.Exists(request)) return;
                File.Delete(request);
                Run();
            };
        }
        [MenuItem("Tools/RTL Localization Studio/Validate Localization")]
        public static void Run()
        {
            var results = new List<string>();
            void Check(string name, Action action)
            {
                try { action(); results.Add("PASS " + name); }
                catch (Exception ex) { results.Add("FAIL " + name + ": " + ex); }
            }
            void Assert(bool value, string message) { if (!value) throw new Exception(message); }
            Check("CSV preserves Persian, Arabic, quotes, commas and newlines", () =>
            {
                var data = new[] { new[] { "Key", "fa", "en", "ar" }, new[] { "test", "سلام\nدنیا", "Hello, \"friend\"", "مرحبا" } };
                var parsed = TranslationCsv.Read("\ufeff" + TranslationCsv.Write(data));
                Assert(parsed.Count == 2 && parsed[1].SequenceEqual(data[1]), "Round trip mismatch");
            });
            Check("CSV rejects malformed quoting", () =>
            {
                bool rejected = false;
                try { TranslationCsv.Read("a,\"unfinished"); } catch (FormatException) { rejected = true; }
                Assert(rejected, "Malformed CSV was accepted");
            });
            Check("Code phrase scan skips comments, interpolation and concatenation", () =>
            {
                var phrases = ScriptSourceExtractor.Extract("// label.text = \"comment\";\nlabel.text = \"Hello\"; label.SetText(\"Score: {0}\", 12); label.text = \"Prefix\" + player; label.text = $\"Player {player}\"; label.text = ok ? \"Yes\" : \"No\";").ToArray();
                Assert(phrases.Contains("Hello") && phrases.Contains("Score: {0}") && phrases.Contains("Yes") && phrases.Contains("No"), "Static phrases were missed");
                Assert(!phrases.Contains("comment") && !phrases.Contains("Prefix") && !phrases.Any(x => x.Contains("Player")), "Unsafe phrases were extracted");
            });
            Check("Source index resolves numeric templates and rejects ambiguous text", () =>
            {
                var table = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.StringTable>();
                var shared = ScriptableObject.CreateInstance<UnityEngine.Localization.Tables.SharedTableData>();
                table.SharedData = shared;
                try
                {
                    table.AddEntry("a", "شروع"); table.AddEntry("b", "شروع");
                    table.AddEntry("score", "امتیاز: {0}");
                    SourceTextIndex.Rebuild(table);
                    Assert(!SourceTextIndex.TryResolve("شروع", null, out _), "Ambiguous source was guessed");
                    Assert(SourceTextIndex.TryResolve("شروع", "b", out var exact) && exact.key == "b", "Existing key was ignored");
                    Assert(SourceTextIndex.TryResolve("امتیاز: ۱۲۳", null, out var numeric) && (decimal)numeric.arguments[0] == 123, "Persian numeric argument was lost");
                    Assert(!SourceTextIndex.TryResolve("Player_123", null, out _) && !SourceTextIndex.TryResolve("امتیاز: {0}", null, out _), "Unknown/template text was translated");
                }
                finally { UnityEngine.Object.DestroyImmediate(table); UnityEngine.Object.DestroyImmediate(shared); }
            });
            Check("Digit conversion preserves rich text tags", () =>
            {
                Assert(Localization.FormatDigits("<color=#123456>12</color>", DigitStyle.Persian) == "<color=#123456>۱۲</color>", "Tag corruption");
                Assert(Localization.FormatDigits("۱۲٣", DigitStyle.Latin) == "123", "Mixed digit conversion failed");
            });
            Check("Workspace contains all locales, fallback and preload", () =>
            {
                LocalizationWorkspace.Setup();
                foreach (string code in LocalizationWorkspace.Codes) Assert(LocalizationWorkspace.Table(code), "Missing table: " + code);
                var english = UnityEditor.Localization.LocalizationEditorSettings.GetLocales().First(x => x.Identifier.Code == "en");
                Assert(english.Metadata.GetMetadata<UnityEngine.Localization.Metadata.FallbackLocale>().Locale.Identifier.Code == "fa", "Missing Persian fallback");
                Assert(LocalizationWorkspace.Collection.IsPreloadTableFlagSet(), "Tables are not preloaded");
            });
            Check("All screenshot languages have tables, styles and Persian fallback", () =>
            {
                Assert(LocalizationWorkspace.Codes.Length == 29 && LocalizationWorkspace.Codes.Distinct().Count() == 29, "Incorrect language count");
                Assert(LocalizationWorkspace.Profile.Find("ckb").rightToLeft && !LocalizationWorkspace.Profile.Find("ku").rightToLeft, "Incorrect Kurdish direction");
                foreach (string code in LocalizationWorkspace.Codes)
                {
                    Assert(LocalizationWorkspace.Profile.Find(code) != null && LocalizationWorkspace.Table(code), "Missing style/table: " + code);
                    var locale = UnityEditor.Localization.LocalizationEditorSettings.GetLocales().First(x => x.Identifier.Code == code);
                    if (code != "fa") Assert(locale.Metadata.GetMetadata<UnityEngine.Localization.Metadata.FallbackLocale>().Locale.Identifier.Code == "fa", "Fallback missing: " + code);
                }
                string path = "Temp/rtl-localization-language-export.csv";
                try
                {
                    TranslationExchange.Export(path);
                    var header = TranslationCsv.Read(File.ReadAllText(path))[0];
                    Assert(LocalizationWorkspace.Codes.All(header.Contains), "Export omitted a language");
                    Assert(TranslationExchange.Preview(File.ReadAllText(path)).errors.Count == 0, "Export cannot be imported");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
            Check("Specific font rules override default fonts and font-only preview restores", () =>
            {
                var profile = LocalizationWorkspace.Profile;
                var style = profile.Find("ja");
                var previousRules = style.fontReplacements; var previousFont = style.font;
                var original = TMP_Settings.defaultFontAsset;
                var replacement = UnityEngine.Object.Instantiate(original);
                var fallback = UnityEngine.Object.Instantiate(original);
                var customMaterial = UnityEngine.Object.Instantiate(original.material);
                var item = new GameObject("Font-only fixture", typeof(RectTransform)); item.SetActive(false);
                try
                {
                    style.font = fallback;
                    style.fontReplacements = new[] { new FontReplacement { source = original, replacement = replacement } };
                    Assert(profile.ResolveFont(original, "ja") == replacement, "Specific rule lost priority");
                    Assert(profile.ResolveFont(replacement, "ja") == fallback, "Default font not used");
                    var text = item.AddComponent<TextMeshProUGUI>(); text.font = original; text.fontSharedMaterial = customMaterial; text.text = "Authored";
                    LocalizationScanner.BindFont(text); LocalizationScanner.BindFont(text);
                    Assert(item.GetComponents<LocalizedFont>().Length == 1 && text.font == original, "Font binding changed source or duplicated");
                    var binding = item.GetComponent<LocalizedFont>();
                    LocalizationScenePreview.Show("ja", Array.Empty<LocalizedLabel>(), new[] { binding });
                    Assert(text.font == replacement && text.text == "Authored", "Font-only preview failed");
                    LocalizationScenePreview.Stop();
                    Assert(text.font == original && text.fontSharedMaterial == customMaterial && text.text == "Authored", "Font-only preview did not restore");
                    binding.ApplyPreview("ja"); binding.ApplyPreview("fa");
                    Assert(text.font == profile.ResolveFont(original, "fa"), "Switch back lost original font");
                    if (text.font == original) Assert(text.fontSharedMaterial == customMaterial, "Authored material was lost on switching back");
                }
                finally
                {
                    LocalizationScenePreview.Stop(); style.fontReplacements = previousRules; style.font = previousFont;
                    UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(replacement); UnityEngine.Object.DestroyImmediate(fallback); UnityEngine.Object.DestroyImmediate(customMaterial);
                }
            });
            Check("Legacy UI Text and TextMesh font preview restores font and material", () =>
            {
                var style = LocalizationWorkspace.Profile.Find("ko"); var previous = style.legacyFontReplacements;
                var original = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var replacement = UnityEngine.Object.Instantiate(original);
                var uiObject = new GameObject("Legacy UI", typeof(RectTransform)); uiObject.SetActive(false);
                var meshObject = new GameObject("Legacy Mesh"); meshObject.SetActive(false);
                try
                {
                    style.legacyFontReplacements = new[] { new LegacyFontReplacement { source = original, replacement = replacement } };
                    var ui = uiObject.AddComponent<UnityEngine.UI.Text>(); ui.font = original; ui.text = "UI authored";
                    var uiBinding = uiObject.AddComponent<LocalizedLegacyFont>(); uiBinding.authoredFont = original;
                    var mesh = meshObject.AddComponent<TextMesh>(); mesh.font = original; mesh.text = "Mesh authored";
                    var meshBinding = meshObject.AddComponent<LocalizedLegacyFont>(); meshBinding.authoredFont = original;
                    var renderer = mesh.GetComponent<Renderer>(); renderer.sharedMaterial = original.material;
                    meshBinding.authoredMaterial = renderer.sharedMaterial;
                    LocalizationScenePreview.Show("ko", Array.Empty<LocalizedLabel>(), legacyFonts: new[] { uiBinding, meshBinding });
                    Assert(ui.font == replacement && mesh.font == replacement && renderer.sharedMaterial == replacement.material, "Legacy replacement failed");
                    LocalizationScenePreview.Stop();
                    Assert(ui.font == original && mesh.font == original && renderer.sharedMaterial == original.material, "Legacy restoration failed");
                    Assert(ui.text == "UI authored" && mesh.text == "Mesh authored", "Legacy text changed");
                }
                finally { LocalizationScenePreview.Stop(); style.legacyFontReplacements = previous; UnityEngine.Object.DestroyImmediate(uiObject); UnityEngine.Object.DestroyImmediate(meshObject); UnityEngine.Object.DestroyImmediate(replacement); }
            });
            string key = "__rtlls_validation_" + Guid.NewGuid().ToString("N");
            var testScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Validation label", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, testScene);
            go.SetActive(false);
            try
            {
                var text = go.AddComponent<TextMeshProUGUI>();
                text.text = "شروع بازی";
                Check("Bulk binding preserves source and is idempotent", () =>
                {
                    var row = LocalizationScanner.Describe(text, ""); row.key = key;
                    Assert(row.status == "Ready", "Static text not classified ready");
                    LocalizationScanner.Bind(text, row, LabelAlignment.Preserve);
                    LocalizationScanner.Bind(text, row, LabelAlignment.Preserve);
                    Assert(go.GetComponents<LocalizedLabel>().Length == 1, "Duplicate label");
                    Assert(text.text == "شروع بازی", "Binding modified visible source");
                    Assert(LocalizationWorkspace.Value(key, "fa") == "شروع بازی", "Source not stored");
                });
                Check("Import previews before mutation and keeps blanks", () =>
                {
                    var entry = LocalizationWorkspace.Collection.SharedData.GetEntry(key);
                    string[] header = { "Key", "Id", "fa", "en", "ar", "Smart", "SourceHash" };
                    string[] row = { key, entry.Id.ToString(), "شروع بازی", "Play", "", "False", LocalizationWorkspace.SourceHash("شروع بازی") };
                    var import = TranslationExchange.Preview(TranslationCsv.Write(new[] { header, row }));
                    Assert(import.errors.Count == 0 && import.changes.Count == 1, "Unexpected preview");
                    Assert(LocalizationWorkspace.Value(key, "en") == "", "Preview mutated table");
                    TranslationExchange.Apply(import);
                    Assert(LocalizationWorkspace.Value(key, "en") == "Play", "Import not applied");
                    row[3] = "";
                    Assert(TranslationExchange.Preview(TranslationCsv.Write(new[] { header, row })).changes.Count == 0, "Blank would clear translation");
                    row[6] = "stale";
                    Assert(TranslationExchange.Preview(TranslationCsv.Write(new[] { header, row })).errors.Count > 0, "Stale source accepted");
                });
                Check("Numeric, input and ignored texts require review", () =>
                {
                    var other = new GameObject("Other", typeof(RectTransform));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(other, testScene);
                    other.SetActive(false);
                    try
                    {
                        var t = other.AddComponent<TextMeshProUGUI>(); t.text = "۱۲۳";
                        Assert(LocalizationScanner.Describe(t, "").status.Contains("review"), "Numeric text auto-selected");
                        t.text = "Name"; other.AddComponent<LocalizationIgnore>();
                        Assert(LocalizationScanner.Describe(t, "").status == "Excluded", "Ignore marker not honored");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(other); }
                });
                Check("Locale switching and missing-translation fallback", () =>
                {
                    var initialization = UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
                    initialization.WaitForCompletion();
                    var previous = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                    string preference = LocalizationWorkspace.Profile.preferenceKey;
                    bool hadPreference = PlayerPrefs.HasKey(preference);
                    string savedPreference = PlayerPrefs.GetString(preference);
                    try
                    {
                        var reference = new UnityEngine.Localization.LocalizedString(LocalizationWorkspace.Profile.table, key);
                        Localization.SetLanguage("en");
                        Assert(reference.GetLocalizedString() == "Play", "English value not selected");
                        Localization.SetLanguage("ar");
                        Assert(reference.GetLocalizedString() == "شروع بازی", "Arabic did not fall back to Persian");
                        Localization.SetLanguage("fa");
                        Assert(reference.GetLocalizedString() == "شروع بازی", "Persian switch failed");
                    }
                    finally
                    {
                        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = previous;
                        if (hadPreference) PlayerPrefs.SetString(preference, savedPreference); else PlayerPrefs.DeleteKey(preference);
                        PlayerPrefs.Save();
                    }
                });
                Check("Source changes mark translations for review", () =>
                {
                    var review = TranslationReviewState.Get();
                    review.MarkReviewed(key, "en");
                    Assert(!review.NeedsReview(key, "en"), "Fresh translation marked stale");
                    LocalizationWorkspace.SetValue(key, "fa", "شروع");
                    Assert(review.NeedsReview(key, "en"), "Source edit did not mark review");
                    LocalizationWorkspace.SetValue(key, "fa", "شروع بازی");
                });
                Check("Smart placeholder mismatch blocks import", () =>
                {
                    LocalizationWorkspace.SetValue(key, "fa", "امتیاز: {score}", true);
                    var entry = LocalizationWorkspace.Collection.SharedData.GetEntry(key);
                    string[] header = { "Key", "Id", "fa", "en", "ar", "Smart", "SourceHash" };
                    string[] row = { key, entry.Id.ToString(), "امتیاز: {score}", "Score: {wrong}", "", "True", LocalizationWorkspace.SourceHash("امتیاز: {score}") };
                    Assert(TranslationExchange.Preview(TranslationCsv.Write(new[] { header, row })).errors.Count > 0, "Mismatched placeholder accepted");
                    row[3] = "Score: {score}";
                    Assert(TranslationExchange.Preview(TranslationCsv.Write(new[] { header, row })).errors.Count == 0, "Valid Smart String rejected");
                    LocalizationWorkspace.SetValue(key, "fa", "شروع بازی");
                });
                Check("Saved prefab binding survives reload", () =>
                {
                    string path = LocalizationWorkspace.Root + "/" + key + ".prefab";
                    var prefabObject = new GameObject("Prefab text", typeof(RectTransform));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefabObject, testScene);
                    prefabObject.SetActive(false);
                    try
                    {
                        var t = prefabObject.AddComponent<TextMeshProUGUI>(); t.text = "شروع بازی";
                        PrefabUtility.SaveAsPrefabAsset(prefabObject, path);
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var row = LocalizationScanner.Describe(asset.GetComponent<TMP_Text>(), path); row.key = key;
                        Assert(LocalizationScanner.Apply(new List<ScanRow> { row }, LabelAlignment.Preserve) == 1, "Prefab was not processed");
                        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<LocalizedLabel>();
                        Assert(saved && saved.reference.TableEntryReference.Key == key, "Prefab lost binding");
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, testScene);
                        try
                        {
                            var instanceLabel = instance.GetComponent<LocalizedLabel>();
                            var instanceText = instance.GetComponent<TMP_Text>();
                            string before = EditorJsonUtility.ToJson(instanceText);
                            LocalizationScenePreview.Show("en", new[] { instanceLabel });
                            Assert(instanceText.text == "Play", "Prefab instance preview did not update");
                            LocalizationScenePreview.Stop();
                            Assert(EditorJsonUtility.ToJson(instanceText) == before, "Preview changed prefab instance properties");
                        }
                        finally { LocalizationScenePreview.Stop(); UnityEngine.Object.DestroyImmediate(instance); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(prefabObject); AssetDatabase.DeleteAsset(path); }
                });
                Check("Bulk exclusion is persistent and prevents binding", () =>
                {
                    var excluded = new GameObject("Excluded text", typeof(RectTransform));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(excluded, testScene);
                    excluded.SetActive(false);
                    try
                    {
                        var t = excluded.AddComponent<TextMeshProUGUI>(); t.text = "User content";
                        var row = LocalizationScanner.Describe(t, "");
                        LocalizationScanner.Apply(new List<ScanRow> { row }, LabelAlignment.Preserve, true);
                        Assert(t.GetComponent<LocalizationIgnore>(), "Ignore marker missing");
                        row = LocalizationScanner.Describe(t, "");
                        Assert(row.status == "Excluded" && !row.selected, "Excluded text was selected for binding");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(excluded); }
                });
                Check("Scene preview switches all labels and restores their authored style", () =>
                {
                    LocalizationWorkspace.SetValue(key, "fa", "شروع بازی");
                    LocalizationWorkspace.SetValue(key, "en", "Play");
                    LocalizationWorkspace.SetValue(key, "ar", "ابدأ اللعب");
                    var label = go.GetComponent<LocalizedLabel>();
                    var originalText = LocalizationScanner.Original(text);
                    float originalSize = text.fontSize;
                    var originalAlignment = text.alignment;
                    var previousLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                    var englishStyle = LocalizationWorkspace.Profile.Find("en");
                    float previousScale = englishStyle.fontScale;
                    var previousStyleFont = englishStyle.font;
                    var originalFont = text.font;
                    var originalMaterial = text.fontSharedMaterial;
                    var other = new GameObject("Inactive label", typeof(RectTransform));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(other, testScene);
                    other.SetActive(false);
                    var secondText = other.AddComponent<TextMeshProUGUI>();
                    secondText.text = "Original second text";
                    var secondLabel = other.AddComponent<LocalizedLabel>();
                    secondLabel.reference.SetReference(LocalizationWorkspace.Profile.table, key);
                    secondLabel.alignment = LabelAlignment.FollowLanguage;
                    try
                    {
                        englishStyle.fontScale = .8f;
                        englishStyle.font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(x => x);
                        LocalizationScenePreview.Show("en", new[] { label, secondLabel });
                        Assert(text.text == "Play" && secondText.text == "Play", "English did not update both inactive labels");
                        Assert(Mathf.Approximately(text.fontSize, originalSize * .8f), "Font style was not applied");
                        if (englishStyle.font) Assert(text.font == englishStyle.font, "Language font was not applied");
                        Assert(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale == previousLocale, "Preview changed runtime locale");
                        LocalizationScenePreview.Show("ar", new[] { label, secondLabel });
                        Assert(secondText.text == "ابدأ اللعب", "Arabic translation was not applied");
                        Assert((int)secondText.alignment % 256 == 4, "Arabic follow-language alignment not right");
                        LocalizationScenePreview.Show("fa", new[] { label, secondLabel });
                        Assert(secondText.text == "شروع بازی", "Persian button did not restore source translation");
                        LocalizationScenePreview.Stop();
                        Assert(LocalizationScanner.Original(text) == originalText, "Authored text not restored");
                        Assert(secondText.text == "Original second text", "Second label was not restored");
                        Assert(text.alignment == originalAlignment && Mathf.Approximately(text.fontSize, originalSize), "Authored style not restored");
                        Assert(text.font == originalFont && text.fontSharedMaterial == originalMaterial, "Authored font/material not restored");
                        Assert(!LocalizationScenePreview.IsActive, "Preview did not stop");
                    }
                    finally
                    {
                        LocalizationScenePreview.Stop();
                        englishStyle.fontScale = previousScale;
                        englishStyle.font = previousStyleFont;
                        UnityEngine.Object.DestroyImmediate(other);
                    }
                });
                Check("Scene save restores preview before serialization", () =>
                {
                    string path = LocalizationWorkspace.Root + "/" + key + ".unity";
                    // Opening an empty saved fixture works even if the user's current scene is untitled.
                    File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n");
                    AssetDatabase.ImportAsset(path);
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    var item = new GameObject("Saved label", typeof(RectTransform));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, scene);
                    item.SetActive(false);
                    try
                    {
                        var t = item.AddComponent<TextMeshProUGUI>(); t.text = "Authored text";
                        var label = item.AddComponent<LocalizedLabel>(); label.reference.SetReference(LocalizationWorkspace.Profile.table, key);
                        LocalizationScenePreview.Show("en", new[] { label });
                        Assert(t.text == "Play", "Preview did not change saved scene");
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path);
                        Assert(!LocalizationScenePreview.IsActive && t.text == "Authored text", "Scene save kept preview data");
                        Assert(!File.ReadAllText(path).Contains("m_text: Play"), "Preview translation was serialized");
                    }
                    finally
                    {
                        LocalizationScenePreview.Stop();
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                        AssetDatabase.DeleteAsset(path);
                    }
                });
            }
            finally
            {
                LocalizationScenePreview.Stop();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(testScene);
                foreach (string code in LocalizationWorkspace.Codes)
                {
                    var table = LocalizationWorkspace.Table(code);
                    if (table) { table.RemoveEntry(key); EditorUtility.SetDirty(table); }
                }
                if (LocalizationWorkspace.Collection)
                {
                    LocalizationWorkspace.Collection.SharedData.RemoveKey(key);
                    EditorUtility.SetDirty(LocalizationWorkspace.Collection.SharedData);
                }
                var ledger = TranslationReviewState.Get();
                ledger.records.RemoveAll(x => x.key == key);
                EditorUtility.SetDirty(ledger);
                AssetDatabase.SaveAssets();
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/localization-validation.txt", results);
            Debug.Log(string.Join("\n", results));
            if (results.Any(x => x.StartsWith("FAIL")) && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
