using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RTLLocalizationStudio.Editor
{
    // Editor-only ledger; Unity String Tables remain the source of translation values.
    public sealed class TranslationReviewState : ScriptableObject
    {
        [Serializable] public sealed class Record { public string key, language, sourceHash; }
        public List<Record> records = new List<Record>();
        public static TranslationReviewState Get()
        {
            string path = LocalizationWorkspace.Root + "/TranslationReview.asset";
            var state = AssetDatabase.LoadAssetAtPath<TranslationReviewState>(path);
            if (!state)
            {
                state = CreateInstance<TranslationReviewState>();
                AssetDatabase.CreateAsset(state, path);
            }
            return state;
        }
        public bool NeedsReview(string key, string language)
        {
            var record = records.FirstOrDefault(x => x.key == key && x.language == language);
            return !string.IsNullOrWhiteSpace(LocalizationWorkspace.Value(key, language)) &&
                (record == null || record.sourceHash != LocalizationWorkspace.SourceHash(LocalizationWorkspace.Value(key, "fa")));
        }
        public void MarkReviewed(string key, string language)
        {
            Undo.RecordObject(this, "Review translation");
            var record = records.FirstOrDefault(x => x.key == key && x.language == language);
            if (record == null) { record = new Record { key = key, language = language }; records.Add(record); }
            record.sourceHash = LocalizationWorkspace.SourceHash(LocalizationWorkspace.Value(key, "fa"));
            EditorUtility.SetDirty(this);
        }
    }
}
