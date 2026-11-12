using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace RTLLocalizationStudio.Editor
{
    /// <summary>Temporary scene rendering, independent of runtime locale and PlayerPrefs.</summary>
    [InitializeOnLoad]
    public static class LocalizationScenePreview
    {
        // These are the serialized properties touched by TMP font/text setters and the RTL bridge.
        private static readonly string[] Properties = {
            "m_text", "m_fontAsset", "m_sharedMaterial", "m_fontMaterial", "m_fontSize", "m_fontSizeBase",
            "m_fontSizeMin", "m_fontSizeMax", "m_horizontalAlignment", "m_verticalAlignment",
            "m_textAlignment", "m_isRightToLeft", "originalText", "farsi", "forceFix", "preserveNumbers"
        };
        private static readonly List<LocalizedLabel> rendered = new List<LocalizedLabel>();
        private static readonly List<TMP_Text> fontRendered = new List<TMP_Text>();
        private static readonly List<UnityEngine.UI.Text> legacyRendered = new List<UnityEngine.UI.Text>();
        private static AnimationModeDriver driver;
        public static string Language { get; private set; }
        public static int LabelCount { get; private set; }
        public static int FallbackCount { get; private set; }
        public static int UnformattedCount { get; private set; }
        public static bool IsActive => driver && AnimationMode.InAnimationMode(driver);

        static LocalizationScenePreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Stop(); };
            EditorSceneManager.sceneSaving += (_, __) => Stop();
            EditorSceneManager.sceneClosing += (_, __) => Stop();
        }

        [MenuItem("Tools/RTL Localization Studio/Scene Language/Persian (fa)")]
        private static void Persian() => Show("fa");
        [MenuItem("Tools/RTL Localization Studio/Scene Language/English (en)")]
        private static void English() => Show("en");
        [MenuItem("Tools/RTL Localization Studio/Scene Language/Arabic (ar)")]
        private static void Arabic() => Show("ar");
        [MenuItem("Tools/RTL Localization Studio/Scene Language/Restore authored view")]
        public static void Stop()
        {
            if (driver && AnimationMode.InAnimationMode(driver)) AnimationMode.StopAnimationMode(driver);
            foreach (var label in rendered)
            {
                if (!label) continue;
                label.ResetPreviewBaseline();
                var text = label.GetComponent<TMP_Text>();
                if (!text) continue;
                // RTLTMPro stores its source separately; regenerate its display after restoration.
                text.GetType().GetMethod("ResetTextWriteTracking", Type.EmptyTypes)?.Invoke(text, null);
                text.GetType().GetMethod("UpdateText", Type.EmptyTypes)?.Invoke(text, null);
                text.havePropertiesChanged = true;
                text.SetAllDirty();
            }
            rendered.Clear();
            foreach (var text in fontRendered) if (text) { text.havePropertiesChanged = true; text.SetAllDirty(); }
            fontRendered.Clear();
            foreach (var text in legacyRendered) if (text) text.SetAllDirty();
            legacyRendered.Clear();
            Language = null;
            LabelCount = FallbackCount = UnformattedCount = 0;
            if (driver) UnityEngine.Object.DestroyImmediate(driver);
            driver = null;
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Show(string language)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Scene preview is available outside Play Mode.");
            if (!LocalizationWorkspace.Profile || LocalizationWorkspace.Profile.Find(language) == null)
                throw new InvalidOperationException("Set up the localization workspace first.");
            Stop();
            if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Stop the Animation window preview before previewing scene languages.");
            var roots = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(s => s.isLoaded).SelectMany(s => s.GetRootGameObjects()).ToList();
            var prefab = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefab != null && prefab.prefabContentsRoot) roots.Add(prefab.prefabContentsRoot);
            Show(language, roots.SelectMany(x => x.GetComponentsInChildren<LocalizedLabel>(true)).Distinct(),
                roots.SelectMany(x => x.GetComponentsInChildren<LocalizedFont>(true)).Distinct(),
                roots.SelectMany(x => x.GetComponentsInChildren<LocalizedLegacyFont>(true)).Distinct());
        }

        // Explicit scope keeps integration tests isolated from the user's loaded scenes.
        internal static void Show(string language, IEnumerable<LocalizedLabel> labels, IEnumerable<LocalizedFont> fonts = null, IEnumerable<LocalizedLegacyFont> legacyFonts = null)
        {
            Stop();
            if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Another animation preview is active.");
            driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            driver.hideFlags = HideFlags.HideAndDontSave;
            AnimationMode.StartAnimationMode(driver);
            Language = language;
            try
            {
                foreach (var label in labels)
                {
                    if (!label || !label.enabled || EditorUtility.IsPersistent(label)) continue;
                    var text = label.GetComponent<TMP_Text>();
                    if (!text || LocalizationScanner.Ignored(text)) continue;
                    var reference = label.reference;
                    var collection = LocalizationEditorSettings.GetStringTableCollection(reference.TableReference);
                    if (!collection || reference.TableEntryReference.ReferenceType == TableEntryReference.Type.Empty) continue;
                    StringTableEntry Find(string code)
                    {
                        var table = collection.GetTable(code) as StringTable;
                        if (!table) return null;
                        return reference.TableEntryReference.ReferenceType == TableEntryReference.Type.Id
                            ? table.GetEntry(reference.TableEntryReference.KeyId)
                            : table.GetEntry(reference.TableEntryReference.Key);
                    }
                    var entry = Find(language);
                    bool fallback = entry == null || string.IsNullOrEmpty(entry.Value);
                    if (fallback) entry = Find(LocalizationWorkspace.Profile.defaultLanguage);
                    string value = entry?.Value;
                    if (string.IsNullOrEmpty(value)) value = label.sourceText;
                    if (string.IsNullOrEmpty(value)) continue;
                    if (entry != null && (entry.IsSmart || reference.Arguments != null && reference.Arguments.Count > 0))
                    {
                        try { value = entry.GetLocalizedString(null, reference.Arguments, null); }
                        catch { UnformattedCount++; } // Keep the template visible if runtime arguments are absent.
                    }
                    RegisterProperties(text);
                    rendered.Add(label);
                    label.ResetPreviewBaseline();
                    label.RenderPreview(value, language);
                    text.havePropertiesChanged = true;
                    text.SetAllDirty();
                    LabelCount++;
                    if (fallback && language != LocalizationWorkspace.Profile.defaultLanguage) FallbackCount++;
                }
                foreach (var font in fonts ?? Enumerable.Empty<LocalizedFont>())
                {
                    if (!font || !font.enabled || EditorUtility.IsPersistent(font)) continue;
                    var text = font.GetComponent<TMP_Text>();
                    if (!text || LocalizationScanner.Ignored(text) || rendered.Any(x => x.gameObject == font.gameObject)) continue;
                    RegisterProperties(text);
                    fontRendered.Add(text);
                    font.ApplyPreview(language);
                    text.havePropertiesChanged = true; text.SetAllDirty();
                }
                foreach (var font in legacyFonts ?? Enumerable.Empty<LocalizedLegacyFont>())
                {
                    if (!font || !font.enabled || EditorUtility.IsPersistent(font)) continue;
                    var ui = font.GetComponent<UnityEngine.UI.Text>();
                    var mesh = font.GetComponent<TextMesh>();
                    if (ui) { RegisterObjectProperty(ui, "m_FontData.m_Font"); legacyRendered.Add(ui); }
                    if (mesh)
                    {
                        RegisterObjectProperty(mesh, "m_Font");
                        var renderer = mesh.GetComponent<Renderer>();
                        if (renderer) RegisterObjectProperty(renderer, "m_Materials.Array.data[0]");
                    }
                    font.ApplyPreview(language);
                }
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { Stop(); throw; }
        }

        private static void RegisterProperties(TMP_Text text)
        {
            using (var serialized = new SerializedObject(text))
                foreach (string path in Properties)
                {
                    var property = serialized.FindProperty(path);
                    if (property == null) continue;
                    var modification = new PropertyModification { target = text, propertyPath = path };
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.String: modification.value = property.stringValue; break;
                        case SerializedPropertyType.Boolean: modification.value = property.boolValue ? "1" : "0"; break;
                        case SerializedPropertyType.Float: modification.value = property.floatValue.ToString("R", CultureInfo.InvariantCulture); break;
                        case SerializedPropertyType.Integer:
                        case SerializedPropertyType.Enum: modification.value = property.intValue.ToString(CultureInfo.InvariantCulture); break;
                        case SerializedPropertyType.ObjectReference: modification.objectReference = property.objectReferenceValue; break;
                        default: continue;
                    }
                    var binding = property.propertyType == SerializedPropertyType.ObjectReference
                        ? EditorCurveBinding.PPtrCurve("", text.GetType(), path)
                        : EditorCurveBinding.FloatCurve("", text.GetType(), path);
                    AnimationMode.AddPropertyModification(binding, modification, property.prefabOverride);
                }
        }
        private static void RegisterObjectProperty(UnityEngine.Object target, string path)
        {
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.FindProperty(path);
                if (property == null) return;
                AnimationMode.AddPropertyModification(EditorCurveBinding.PPtrCurve("", target.GetType(), path),
                    new PropertyModification { target = target, propertyPath = path, objectReference = property.objectReferenceValue }, property.prefabOverride);
            }
        }
    }

    internal sealed class LocalizationPreviewSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            LocalizationScenePreview.Stop();
            return paths;
        }
    }
}
