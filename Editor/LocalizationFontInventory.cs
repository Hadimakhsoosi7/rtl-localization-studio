using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;

namespace RTLLocalizationStudio.Editor
{
    [InitializeOnLoad]
    public static class LocalizationFontInventory
    {
        public const string ReportPath = "Library/RTLLocalizationStudio/font-usage.csv";
        static LocalizationFontInventory()
        {
            EditorApplication.delayCall += () =>
            {
                const string request = "Temp/RTLLocalizationStudio.fontscan";
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request);
                try { ScanProject(); }
                catch (Exception ex) { Directory.CreateDirectory("Library/RTLLocalizationStudio"); File.WriteAllText("Library/RTLLocalizationStudio/font-scan-error.txt", ex.ToString()); }
            };
        }
        [MenuItem("Tools/RTL Localization Studio/Scan Project Fonts")]
        public static void ScanProject()
        {
            var legacy = new List<LegacyFontRow>();
            var rows = LocalizationScanner.ScanAssets("Assets", true, true, (root, path) => LegacyFontInventory.Inspect(root, path, legacy));
            Directory.CreateDirectory("Library/RTLLocalizationStudio");
            LocalizationFontPanel.Export(ReportPath, rows);
            LegacyFontInventory.Export(legacy);
            var summary = new List<string> { "# Project font usage", "", rows.Count + " TMP text occurrences in saved scenes and prefabs. Prefab assets and scene instances are counted separately.", "", "| Font | Text occurrences | Asset |", "| --- | ---: | --- |" };
            summary.AddRange(rows.GroupBy(x => x.font).OrderByDescending(x => x.Count()).Select(x => "| " + (x.Key ? x.Key.name : "Missing") + " | " + x.Count() + " | " + AssetDatabase.GetAssetPath(x.Key) + " |"));
            summary.Add("\nLegacy UI Text / TextMesh: " + legacy.Count + " occurrences\n");
            summary.AddRange(legacy.GroupBy(x => x.font).OrderByDescending(x => x.Count()).Select(x => (x.Key ? x.Key.name : "Missing") + ": " + x.Count() + " — " + AssetDatabase.GetAssetPath(x.Key)));
            summary.Add("\nAvailable source font files (TTF/OTF):\n");
            summary.AddRange(AssetDatabase.FindAssets("t:Font", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(x => x));
            File.WriteAllLines("Library/RTLLocalizationStudio/font-summary.md", summary);
            UnityEngine.Debug.Log("Font scan: " + rows.Count + " text occurrences. Report: " + ReportPath);
        }
        public static List<ScanRow> LoadReport()
        {
            var result = new List<ScanRow>();
            if (!File.Exists(ReportPath)) return result;
            foreach (var record in TranslationCsv.Read(File.ReadAllText(ReportPath)).Skip(1))
            {
                if (record.Length < 7) continue;
                result.Add(new ScanRow { font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(record[1]), assetPath = record[2], hierarchy = record[3], text = record[4], fontConnected = record[5] == "True", status = record[6], key = record.Length > 7 ? record[7] : "" });
            }
            return result;
        }
    }
}
