using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTLLocalizationStudio.Editor
{
    public sealed class LocalizationTextPreview : EditorWindow
    {
        private PreviewRenderUtility preview;
        private GameObject root;
        private LocalizedLabel label;
        private string value, code;
        private int language;
        public static void Open(TMP_Text source, string text, string languageCode)
        {
            var window = GetWindow<LocalizationTextPreview>(true, "Localized text preview");
            window.minSize = new Vector2(650, 350);
            window.Build(source, text, languageCode);
            window.Show();
        }
        private void Build(TMP_Text source, string text, string languageCode)
        {
            Cleanup();
            value = text; code = languageCode;
            language = Array.IndexOf(LocalizationWorkspace.Codes, code);
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 150;
            preview.camera.transform.position = new Vector3(0, 0, -10);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.09f, .12f, .16f);
            root = new GameObject("Localization preview", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            preview.AddSingleGO(root);
            var go = new GameObject("Text", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(root.transform, false);
            var target = (TMP_Text)go.AddComponent(source.GetType());
            EditorUtility.CopySerialized(source, target);
            target.enabled = true;
            target.rectTransform.sizeDelta = new Vector2(600, 260);
            target.rectTransform.anchoredPosition = Vector2.zero;
            target.rectTransform.localScale = Vector3.one;
            target.rectTransform.localRotation = Quaternion.identity;
            label = go.AddComponent<LocalizedLabel>();
            var original = source.GetComponent<LocalizedLabel>();
            if (original) { label.alignment = original.alignment; label.useLanguageDigits = original.useLanguageDigits; }
            var renderer = Type.GetType("RTLLocalizationStudio.RTL.LocalizedRTLRenderer, RTLLocalizationStudio.RTL");
            if (renderer != null) go.AddComponent(renderer);
            label.enabled = false; // Preview never subscribes to runtime localization.
            go.SetActive(true);
            label.RenderPreview(value, code);
            target.ForceMeshUpdate();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Isolated preview using the selected text's font and styling. Your scene is not modified.", MessageType.Info);
            if (preview == null) { GUILayout.Label("Choose Preview on selected text in Localization Studio."); return; }
            EditorGUI.BeginChangeCheck();
            language = EditorGUILayout.Popup(language, LanguageCatalog.Names);
            value = EditorGUILayout.TextArea(value, GUILayout.Height(60));
            if (EditorGUI.EndChangeCheck())
            {
                code = LocalizationWorkspace.Codes[language];
                label.RenderPreview(value, code);
                label.GetComponent<TMP_Text>().ForceMeshUpdate();
            }
            var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint) return;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.aspect = rect.width / Mathf.Max(1, rect.height);
            preview.camera.orthographicSize = Mathf.Max(150, 320 / preview.camera.aspect);
            preview.camera.Render();
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }
        private void OnDisable() => Cleanup();
        private void Cleanup()
        {
            if (root) DestroyImmediate(root);
            preview?.Cleanup(); preview = null;
        }
    }
}
