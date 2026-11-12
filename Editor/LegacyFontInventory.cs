using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RTLLocalizationStudio.Editor
{
    public sealed class LegacyFontRow
    {
        public Font font;
        public string assetPath, hierarchy, text, kind;
        public bool connected, excluded;
    }
    public static class LegacyFontInventory
    {
        public const string ReportPath = "Library/RTLLocalizationStudio/legacy-font-usage.csv";
        public static void Inspect(GameObject root, string path, List<LegacyFontRow> rows)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                Font font; string text;
                if (component is Text ui) { font = ui.font; text = ui.text; }
                else if (component is TextMesh mesh) { font = mesh.font; text = mesh.text; }
                else continue;
                rows.Add(new LegacyFontRow { font = font, text = text, assetPath = path, hierarchy = LocalizationScanner.Hierarchy(component.transform), kind = component is Text ? "UI Text" : "TextMesh",
                    connected = component.GetComponent<LocalizedLegacyFont>(), excluded = component.GetComponentsInParent<LocalizationIgnore>(true).Any(x => x.gameObject == component.gameObject || x.includeChildren) });
            }
        }
        public static void Export(IEnumerable<LegacyFontRow> rows)
        {
            Directory.CreateDirectory("Library/RTLLocalizationStudio");
            var data = new List<string[]> { new[] { "Font", "FontAsset", "Asset", "Hierarchy", "Text", "Kind", "Connected", "Excluded" } };
            data.AddRange(rows.Select(x => new[] { x.font ? x.font.name : "(missing)", AssetDatabase.GetAssetPath(x.font), x.assetPath, x.hierarchy, x.text, x.kind, x.connected.ToString(), x.excluded.ToString() }));
            File.WriteAllText(ReportPath, TranslationCsv.Write(data), new System.Text.UTF8Encoding(true));
        }
        public static List<LegacyFontRow> Load()
        {
            if (!File.Exists(ReportPath)) return new List<LegacyFontRow>();
            return TranslationCsv.Read(File.ReadAllText(ReportPath)).Skip(1).Where(x => x.Length == 8).Select(x => new LegacyFontRow { font = AssetDatabase.LoadAssetAtPath<Font>(x[1]), assetPath = x[2], hierarchy = x[3], text = x[4], kind = x[5], connected = x[6] == "True", excluded = x[7] == "True" }).ToList();
        }
        public static int Connect(IEnumerable<LegacyFontRow> rows)
        {
            LocalizationScenePreview.Stop();
            int count = 0;
            string backup = "Library/RTLLocalizationStudioBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            foreach (var group in rows.Where(x => !x.connected && !x.excluded).GroupBy(x => x.assetPath))
            {
                string path = group.Key;
                string destination = Path.Combine(backup, path); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination, true);
                GameObject prefab = null; Scene scene = default;
                bool opened = false;
                try
                {
                    if (path.EndsWith(".prefab")) prefab = PrefabUtility.LoadPrefabContents(path);
                    else { scene = SceneManager.GetSceneByPath(path); opened = !scene.IsValid() || !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                    var roots = prefab ? new[] { prefab } : scene.GetRootGameObjects();
                    foreach (var row in group)
                    {
                        var target = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => LocalizationScanner.Hierarchy(x) == row.hierarchy);
                        if (!target || target.GetComponent<LocalizedLegacyFont>()) continue;
                        var ui = target.GetComponent<Text>(); var mesh = target.GetComponent<TextMesh>();
                        if ((ui ? ui.text : mesh ? mesh.text : null) != row.text) continue;
                        var component = Undo.AddComponent<LocalizedLegacyFont>(target.gameObject);
                        component.authoredFont = ui ? ui.font : mesh.font;
                        if (mesh) component.authoredMaterial = mesh.GetComponent<Renderer>()?.sharedMaterial;
                        EditorUtility.SetDirty(component);
                        if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                        row.connected = true; count++;
                    }
                    if (prefab) PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    else { EditorSceneManager.MarkSceneDirty(scene); if (opened) EditorSceneManager.SaveScene(scene); }
                }
                finally { if (prefab) PrefabUtility.UnloadPrefabContents(prefab); if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets(); return count;
        }
    }
}
