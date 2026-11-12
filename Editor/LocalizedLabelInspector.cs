using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RTLLocalizationStudio.Editor
{
    [CustomEditor(typeof(LocalizedLabel)), CanEditMultipleObjects]
    public sealed class LocalizedLabelInspector : UnityEditor.Editor
    {
        private string search = "";
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reference"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("alignment"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useLanguageDigits"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoTranslateWrittenText"));
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceText"));
            serializedObject.ApplyModifiedProperties();
            if (!LocalizationWorkspace.Collection) return;
            EditorGUILayout.Space();
            search = EditorGUILayout.TextField("Find key", search);
            if (!string.IsNullOrWhiteSpace(search))
                foreach (var entry in LocalizationWorkspace.Collection.SharedData.Entries.Where(x => x.Key.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).Take(12))
                    if (GUILayout.Button(entry.Key))
                    {
                        foreach (LocalizedLabel label in targets)
                        {
                            Undo.RecordObject(label, "Select localization key");
                            label.reference.SetReference(LocalizationWorkspace.Profile.table, entry.Key);
                            EditorUtility.SetDirty(label);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                        }
                        search = "";
                    }
            if (GUILayout.Button("Open Localization Studio")) LocalizationStudio.Open();
        }
    }
}
