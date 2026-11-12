using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTLLocalizationStudio.Editor
{
    public sealed class ScanRow
    {
        public TMP_Text target;
        public TMP_FontAsset font;
        public bool fontConnected;
        public string assetPath, hierarchy, text, key, status;
        public bool selected;
    }
    public static class LocalizationScanner
    {
        public static string Original(TMP_Text text)
        {
            var property = text.GetType().GetProperty("OriginalText");
            return property != null ? property.GetValue(text) as string ?? "" : text.text ?? "";
        }
        public static string Hierarchy(Transform t)
        {
            return t.parent ? Hierarchy(t.parent) + "/" + t.name + "[" + t.GetSiblingIndex() + "]" : t.name + "[" + t.GetSiblingIndex() + "]";
        }
        public static bool Ignored(TMP_Text t) => t.GetComponentsInParent<LocalizationIgnore>(true)
            .Any(x => x.gameObject == t.gameObject || x.includeChildren);

        public static ScanRow Describe(TMP_Text text, string path)
        {
            string source = Original(text);
            var label = text.GetComponent<LocalizedLabel>();
            string status = Ignored(text) ? "Excluded" : label ? "Connected" :
                text.GetComponentInParent<TMP_InputField>(true) ? "Input field: review" :
                string.IsNullOrWhiteSpace(source) ? "Empty: review" :
                !source.Any(char.IsLetter) ? "Number / symbol: review" : "Ready";
            string hierarchy = Hierarchy(text.transform);
            string slug = Regex.Replace(text.name.ToLowerInvariant(), "[^a-z0-9]+", ".").Trim('.');
            string identity = AssetDatabase.AssetPathToGUID(path) + "/" + hierarchy;
            return new ScanRow { target = text, font = text.font, fontConnected = text.GetComponent<LocalizedFont>(), assetPath = path, hierarchy = hierarchy, text = source,
                key = label ? label.reference.TableEntryReference.Key ?? LocalizationWorkspace.Collection?.SharedData.GetEntry(label.reference.TableEntryReference.KeyId)?.Key : "ui." + (slug.Length > 0 ? slug : "text") + "." + LocalizationWorkspace.SourceHash(identity).ToLowerInvariant(),
                status = status, selected = status == "Ready" };
        }

        public static List<ScanRow> ScanLoaded(bool selectionOnly)
        {
            LocalizationScenePreview.Stop();
            IEnumerable<TMP_Text> texts;
            if (selectionOnly) texts = Selection.gameObjects.Where(x => !EditorUtility.IsPersistent(x))
                .SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)).Distinct();
            else texts = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(s => s.isLoaded).SelectMany(s => s.GetRootGameObjects()).SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true));
            return texts.Select(t => Describe(t, t.gameObject.scene.path)).ToList();
        }

        public static List<ScanRow> ScanAssets(string folder, bool scenes, bool prefabs, Action<GameObject, string> inspectRoot = null)
        {
            LocalizationScenePreview.Stop();
            if (!AssetDatabase.IsValidFolder(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException("Choose a folder inside Assets.");
            var paths = new List<string>();
            if (scenes) paths.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));
            if (prefabs) paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));
            var rows = new List<ScanRow>();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Scan localization", path, (float)i / paths.Count)) break;
                    if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        inspectRoot?.Invoke(root, path);
                        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) rows.Add(Describe(t, path));
                    }
                    else
                    {
                        var loaded = SceneManager.GetSceneByPath(path);
                        bool opened = !loaded.IsValid() || !loaded.isLoaded;
                        var scene = opened ? EditorSceneManager.OpenPreviewScene(path) : loaded;
                        try
                        {
                            foreach (var root in scene.GetRootGameObjects())
                            {
                                inspectRoot?.Invoke(root, path);
                                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                                {
                                    var row = Describe(t, path);
                                    if (opened) row.target = null;
                                    rows.Add(row);
                                }
                            }
                        }
                        finally { if (opened) EditorSceneManager.ClosePreviewScene(scene); }
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            return rows;
        }

        public static void Bind(TMP_Text text, ScanRow row, LabelAlignment alignment, bool autoOnly = false)
        {
            if (text.GetComponent<LocalizedLabel>() || Ignored(text)) return;
            // Never overwrite a different source if a user enters a shared key by mistake.
            string existing = autoOnly ? "" : LocalizationWorkspace.Value(row.key, "fa");
            if (!string.IsNullOrEmpty(existing) && existing != row.text)
                throw new InvalidOperationException("Key has a different Persian source: " + row.key);
            if (!autoOnly && (string.IsNullOrWhiteSpace(row.key) || string.IsNullOrWhiteSpace(row.text))) return;
            if (!autoOnly) LocalizationWorkspace.SetValue(row.key, "fa", row.text);
            var label = Undo.AddComponent<LocalizedLabel>(text.gameObject);
            label.sourceText = autoOnly ? "" : row.text;
            label.alignment = alignment;
            if (!autoOnly) label.reference.SetReference(LocalizationWorkspace.Profile.table, row.key);
            var renderer = Type.GetType("RTLLocalizationStudio.RTL.LocalizedRTLRenderer, RTLLocalizationStudio.RTL");
            if (renderer != null && !text.GetComponent(renderer)) Undo.AddComponent(text.gameObject, renderer);
            EditorUtility.SetDirty(label);
            if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            if (text.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
        }

        private static void ExcludeText(TMP_Text text)
        {
            if (!text.GetComponent<LocalizationIgnore>()) Undo.AddComponent<LocalizationIgnore>(text.gameObject);
            if (text.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
        }

        public static void BindFont(TMP_Text text)
        {
            if (text.GetComponent<LocalizedFont>() || Ignored(text)) return;
            var binding = Undo.AddComponent<LocalizedFont>(text.gameObject);
            binding.authoredFont = text.font;
            binding.authoredMaterial = text.fontSharedMaterial;
            EditorUtility.SetDirty(binding);
            if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
            if (text.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
        }

        public static int Apply(List<ScanRow> rows, LabelAlignment alignment, bool exclude = false, bool autoOnly = false, bool fontOnly = false)
        {
            LocalizationScenePreview.Stop();
            var selected = rows.Where(x => x.selected && (fontOnly ? !x.fontConnected : x.status != "Connected") && x.status != "Excluded").ToList();
            foreach (var group in selected.Where(x => !exclude && !autoOnly && !fontOnly).GroupBy(x => x.key))
            {
                if (string.IsNullOrWhiteSpace(group.Key) || group.Select(x => x.text).Distinct().Count() > 1)
                    throw new InvalidOperationException("Invalid or conflicting key: " + group.Key);
                string old = LocalizationWorkspace.Value(group.Key, "fa");
                if (old.Length > 0 && old != group.First().text) throw new InvalidOperationException("Key conflict: " + group.Key);
            }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup(), count = 0;
            Undo.SetCurrentGroupName("Bulk localization binding");
            // Saved assets use an exact file backup; loaded scenes also support normal Undo.
            string backup = "Library/RTLLocalizationStudioBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            System.IO.Directory.CreateDirectory(backup);
            foreach (var group in selected.GroupBy(x => x.assetPath).OrderBy(x => x.Key.EndsWith(".prefab") ? 0 : 1))
            {
                string path = group.Key;
                if (System.IO.File.Exists(path))
                {
                    string destination = System.IO.Path.Combine(backup, path);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                    System.IO.File.Copy(path, destination, true);
                }
                if (path.EndsWith(".prefab"))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var row in group)
                        {
                            var t = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => Hierarchy(x.transform) == row.hierarchy);
                            if (!t || Original(t) != row.text) continue;
                            if (fontOnly) BindFont(t); else if (exclude) ExcludeText(t); else Bind(t, row, alignment, autoOnly);
                            count++; if (fontOnly) row.fontConnected = true; else row.status = exclude ? "Excluded" : "Connected"; row.selected = false;
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened && !string.IsNullOrEmpty(path)) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        foreach (var row in group)
                        {
                            var t = row.target;
                            if (!t && scene.IsValid()) t = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(x => Hierarchy(x.transform) == row.hierarchy);
                            if (!t || Original(t) != row.text) continue;
                            if (fontOnly) BindFont(t); else if (exclude) ExcludeText(t); else Bind(t, row, alignment, autoOnly);
                            count++; if (fontOnly) row.fontConnected = true; else row.status = exclude ? "Excluded" : "Connected"; row.selected = false;
                        }
                        if (opened && scene.IsValid()) EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            Undo.CollapseUndoOperations(undo);
            AssetDatabase.SaveAssets();
            return count;
        }
    }
}
