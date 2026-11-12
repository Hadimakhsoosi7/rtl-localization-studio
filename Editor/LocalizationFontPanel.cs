using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RTLLocalizationStudio.Editor
{
    public sealed class LocalizationFontPanel
    {
        private List<ScanRow> rows = new List<ScanRow>();
        private List<LegacyFontRow> legacyRows = new List<LegacyFontRow>();
        private Font selectedLegacyFont;
        private bool showLegacyUsage;
        private TMP_FontAsset[] available = Array.Empty<TMP_FontAsset>();
        private Font[] sourceFonts = Array.Empty<Font>();
        private string folder = "Assets", search = "", message = "";
        private int language, page;
        private Vector2 scroll;
        private TMP_FontAsset selectedFont;
        private bool showAllFonts;
        public void Draw()
        {
            GUILayout.Label("Fonts by language", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scan scenes and prefabs, choose an original font, then assign its replacement for a language. Original fonts are preserved. Font switching also supports empty and script-written TMP texts.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField("Scan folder", folder);
                if (GUILayout.Button("Load last scan", GUILayout.Width(105)))
                { rows = LocalizationFontInventory.LoadReport(); legacyRows = LegacyFontInventory.Load(); page = 0; RefreshAvailableFonts(); message = rows.Count + " TMP + " + legacyRows.Count + " legacy occurrences loaded from the last scan."; }
                if (GUILayout.Button("Scan all texts & fonts", GUILayout.Width(180), GUILayout.Height(26)))
                {
                    legacyRows.Clear();
                    rows = LocalizationScanner.ScanAssets(folder, true, true, (root, path) => LegacyFontInventory.Inspect(root, path, legacyRows));
                    Directory.CreateDirectory("Library/RTLLocalizationStudio"); Export(LocalizationFontInventory.ReportPath, rows); LegacyFontInventory.Export(legacyRows);
                    RefreshAvailableFonts();
                    page = 0;
                    message = rows.Count + " TMP + " + legacyRows.Count + " legacy text occurrences · " + rows.Select(x => x.font).Distinct().Count() + " used TMP fonts · " + available.Length + " available TMP fonts. Scene instances and their prefab assets are listed separately.";
                }
            }
            language = EditorGUILayout.Popup("Language to configure", language, LanguageCatalog.Names);
            string code = LanguageCatalog.Codes[language];
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save font rules")) AssetDatabase.SaveAssets();
                if (GUILayout.Button("Preview language in open scenes")) LocalizationScenePreview.Show(code);
                if (GUILayout.Button("Restore preview")) LocalizationScenePreview.Stop();
                if (GUILayout.Button("Export usage CSV"))
                {
                    string path = EditorUtility.SaveFilePanel("Font usage", "", "font-usage", "csv");
                    if (path.Length > 0) Export(path, rows);
                }
            }
            showAllFonts = EditorGUILayout.ToggleLeft("Show available font assets (including unused fonts)", showAllFonts);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (showAllFonts)
            {
                GUILayout.Label("TMP font assets", EditorStyles.miniBoldLabel);
                foreach (var font in available) EditorGUILayout.ObjectField(font, typeof(TMP_FontAsset), false);
                GUILayout.Label("Source fonts / legacy fonts (TTF / OTF)", EditorStyles.miniBoldLabel);
                foreach (var font in sourceFonts) EditorGUILayout.ObjectField(font, typeof(Font), false);
            }
            foreach (var group in rows.GroupBy(x => x.font).OrderByDescending(x => x.Count()))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField("Original (" + group.Count() + " texts)", group.Key, typeof(TMP_FontAsset), false);
                        if (GUILayout.Button("Show texts", GUILayout.Width(95))) { selectedFont = group.Key; showLegacyUsage = false; page = 0; }
                    }
                    var style = LocalizationWorkspace.Profile.Find(code);
                    var rules = (style.fontReplacements ?? Array.Empty<FontReplacement>()).ToList();
                    var rule = rules.FirstOrDefault(x => x.source == group.Key);
                    EditorGUI.BeginChangeCheck();
                    var replacement = (TMP_FontAsset)EditorGUILayout.ObjectField(LanguageCatalog.Name(code) + " replacement", rule?.replacement, typeof(TMP_FontAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        LocalizationScenePreview.Stop();
                        Undo.RecordObject(LocalizationWorkspace.Profile, "Change language font rule");
                        rules.RemoveAll(x => x.source == group.Key);
                        if (replacement) rules.Add(new FontReplacement { source = group.Key, replacement = replacement });
                        style.fontReplacements = rules.ToArray(); EditorUtility.SetDirty(LocalizationWorkspace.Profile);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int pending = group.Count(x => !x.fontConnected && x.status != "Excluded");
                        using (new EditorGUI.DisabledScope(pending == 0))
                            if (GUILayout.Button("Enable font switching on " + pending + " texts"))
                            {
                                var chosen = group.ToList(); foreach (var row in chosen) row.selected = true;
                                int count = LocalizationScanner.Apply(chosen, LabelAlignment.Preserve, fontOnly: true);
                                message = count + " texts connected for font switching. Save open scenes; saved assets have backups in Library/RTLLocalizationStudioBackups.";
                            }
                        if (GUILayout.Button("Check glyph coverage")) message = Coverage(group, code);
                    }
                    GUILayout.Label("Effective font: " + (LocalizationWorkspace.Profile.ResolveFont(group.Key, code)?.name ?? "Missing") + " · Specific replacement takes priority over the language's default font.", EditorStyles.wordWrappedMiniLabel);
                }
            }
            GUILayout.Label("Legacy UI Text / TextMesh fonts", EditorStyles.boldLabel);
            foreach (var group in legacyRows.GroupBy(x => x.font).OrderByDescending(x => x.Count()))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField("Original (" + group.Count() + " texts)", group.Key, typeof(Font), false);
                    var style = LocalizationWorkspace.Profile.Find(code);
                    var rules = (style.legacyFontReplacements ?? Array.Empty<LegacyFontReplacement>()).ToList();
                    EditorGUI.BeginChangeCheck();
                    var replacement = (Font)EditorGUILayout.ObjectField(LanguageCatalog.Name(code) + " replacement", rules.FirstOrDefault(x => x.source == group.Key)?.replacement, typeof(Font), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        LocalizationScenePreview.Stop(); Undo.RecordObject(LocalizationWorkspace.Profile, "Change legacy font rule");
                        rules.RemoveAll(x => x.source == group.Key);
                        if (replacement) rules.Add(new LegacyFontReplacement { source = group.Key, replacement = replacement });
                        style.legacyFontReplacements = rules.ToArray(); EditorUtility.SetDirty(LocalizationWorkspace.Profile);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Show texts")) { selectedLegacyFont = group.Key; showLegacyUsage = true; page = 0; }
                        int pending = group.Count(x => !x.connected && !x.excluded);
                        using (new EditorGUI.DisabledScope(pending == 0)) if (GUILayout.Button("Enable font switching on " + pending + " texts")) message = LegacyFontInventory.Connect(group) + " legacy texts connected. Save open scenes.";
                    }
                }
            if (showLegacyUsage)
            {
                GUILayout.Label("Legacy usage — " + (selectedLegacyFont ? selectedLegacyFont.name : "Missing"), EditorStyles.boldLabel);
                var items = legacyRows.Where(x => x.font == selectedLegacyFont).ToList();
                int totalPages = Math.Max(1, (items.Count + 29) / 30); page = Math.Min(page, totalPages - 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("Previous legacy texts")) page--;
                    GUILayout.Label((page + 1) + " / " + totalPages);
                    using (new EditorGUI.DisabledScope(page == totalPages - 1)) if (GUILayout.Button("Next legacy texts")) page++;
                }
                foreach (var row in items.Skip(page * 30).Take(30))
                    EditorGUILayout.SelectableLabel(row.assetPath + "\n" + row.hierarchy + "\n" + row.text, GUILayout.Height(52));
            }
            if (!showLegacyUsage)
            {
            GUILayout.Space(10);
            GUILayout.Label("Text usage — " + (selectedFont ? selectedFont.name : "Missing font"), EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("Filter path / text", search);
            var usages = rows.Where(x => x.font == selectedFont && (string.IsNullOrEmpty(search) || (x.assetPath + x.hierarchy + x.text).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            int pages = Math.Max(1, (usages.Count + 29) / 30); page = Math.Min(page, pages - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("Previous")) page--;
                GUILayout.Label((page + 1) + " / " + pages + " · " + usages.Count + " texts");
                using (new EditorGUI.DisabledScope(page == pages - 1)) if (GUILayout.Button("Next")) page++;
            }
            foreach (var row in usages.Skip(page * 30).Take(30))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.SelectableLabel(row.assetPath + "\n" + row.hierarchy, GUILayout.Height(34));
                    EditorGUILayout.SelectableLabel(row.text, EditorStyles.wordWrappedLabel, GUILayout.Height(38));
                    if (GUILayout.Button("Locate asset")) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(row.assetPath));
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private void RefreshAvailableFonts()
        {
            available = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<TMP_FontAsset>().Distinct().OrderBy(x => x.name).ToArray();
            sourceFonts = AssetDatabase.FindAssets("t:Font", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Font>).Where(x => x).Distinct().OrderBy(x => x.name).ToArray();
        }
        public static string Coverage(IEnumerable<ScanRow> rows, string code)
        {
            var missing = new HashSet<uint>(); int untranslated = 0;
            foreach (var row in rows)
            {
                string value = string.IsNullOrEmpty(row.key) ? "" : LocalizationWorkspace.Value(row.key, code);
                if (string.IsNullOrEmpty(value)) { untranslated++; value = row.text; }
                value = System.Text.RegularExpressions.Regex.Replace(value ?? "", @"<[^>]*>|(?<!\{)\{[^{}]+\}(?!\})", "");
                var font = LocalizationWorkspace.Profile.ResolveFont(row.font, code);
                if (!font) return "Missing font asset. Assign a replacement first.";
                if (!font.HasCharacters(value, out uint[] absent, true, false)) foreach (uint c in absent) missing.Add(c);
            }
            return missing.Count + " missing codepoints in current atlas/fallbacks: " + string.Join(" ", missing.Take(40).Select(x => "U+" + x.ToString("X4")))
                + ". " + untranslated + " texts checked using their authored text because no translation was found. Dynamic fonts may add glyphs at runtime; this read-only check does not change atlases. Review complex scripts visually.";
        }
        public static void Export(string path, IEnumerable<ScanRow> rows)
        {
            var data = new List<string[]> { new[] { "Font", "FontAsset", "Asset", "Hierarchy", "Text", "FontConnected", "TranslationStatus", "Key" } };
            data.AddRange(rows.Select(x => new[] { x.font ? x.font.name : "(missing)", AssetDatabase.GetAssetPath(x.font), x.assetPath, x.hierarchy, x.text, x.fontConnected.ToString(), x.status, x.key ?? "" }));
            File.WriteAllText(path, TranslationCsv.Write(data), new System.Text.UTF8Encoding(true));
        }
    }
}
