using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace RTLLocalizationStudio.Editor
{
    public sealed class TranslationChange
    {
        public string key, language, before, after, sourceHash;
        public bool smart;
    }
    public sealed class TranslationImport
    {
        public readonly List<TranslationChange> changes = new List<TranslationChange>();
        public readonly List<string> errors = new List<string>();
    }
    public static class TranslationExchange
    {
        public static void Export(string path)
        {
            var rows = new List<string[]> { new[] { "Key", "Id" }.Concat(LocalizationWorkspace.Codes).Concat(new[] { "Smart", "SourceHash" }).ToArray() };
            foreach (var entry in LocalizationWorkspace.Collection.SharedData.Entries)
            {
                string fa = LocalizationWorkspace.Value(entry.Key, "fa");
                rows.Add(new[] { entry.Key, entry.Id.ToString() }.Concat(LocalizationWorkspace.Codes.Select(code => LocalizationWorkspace.Value(entry.Key, code)))
                    .Concat(new[] { (LocalizationWorkspace.Table("fa").GetEntry(entry.Id)?.IsSmart ?? false).ToString(), LocalizationWorkspace.SourceHash(fa) }).ToArray());
            }
            File.WriteAllText(path, TranslationCsv.Write(rows), new UTF8Encoding(true));
        }

        public static TranslationImport Preview(string text)
        {
            var result = new TranslationImport();
            List<string[]> rows;
            try { rows = TranslationCsv.Read(text); }
            catch (Exception ex) { result.errors.Add(ex.Message); return result; }
            if (rows.Count == 0) { result.errors.Add("CSV is empty."); return result; }
            var required = new[] { "Key", "Id", "fa", "Smart", "SourceHash" };
            var header = rows[0];
            if (header.Distinct().Count() != header.Length || required.Any(x => !header.Contains(x)))
            { result.errors.Add("Required unique columns: " + string.Join(", ", required)); return result; }
            var seen = new HashSet<string>();
            foreach (var row in rows.Skip(1))
            {
                if (row.Length != header.Length) { result.errors.Add("Wrong column count in CSV row."); continue; }
                string Get(string column) => row[Array.IndexOf(header, column)];
                string key = Get("Key");
                if (!seen.Add(key)) { result.errors.Add("Duplicate key: " + key); continue; }
                var entry = LocalizationWorkspace.Collection.SharedData.GetEntry(key);
                if (entry == null || entry.Id.ToString() != Get("Id")) { result.errors.Add("Unknown or changed Key/Id: " + key); continue; }
                string source = LocalizationWorkspace.Value(key, "fa");
                if (Get("SourceHash") != LocalizationWorkspace.SourceHash(source) || Get("fa") != source)
                { result.errors.Add("Persian source changed; export a fresh file: " + key); continue; }
                bool smart = LocalizationWorkspace.Table("fa").GetEntry(key)?.IsSmart ?? false;
                if (!bool.TryParse(Get("Smart"), out bool importedSmart) || smart != importedSmart)
                { result.errors.Add("Do not change Smart in translation files: " + key); continue; }
                foreach (string code in LocalizationWorkspace.Codes.Where(x => x != "fa" && header.Contains(x)))
                {
                    string value = Get(code);
                    if (string.IsNullOrWhiteSpace(value)) continue; // Empty cells preserve existing translations.
                    if (smart)
                    {
                        try
                        {
                            var formatter = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.SmartFormatter;
                            formatter.Parser.ParseFormat(value, formatter.GetNotEmptyFormatterExtensionNames());
                        }
                        catch (Exception ex) { result.errors.Add(key + " / " + code + ": " + ex.Message); continue; }
                        if (!Arguments(source).SetEquals(Arguments(value)))
                        { result.errors.Add("Placeholder mismatch: " + key + " / " + code); continue; }
                    }
                    string before = LocalizationWorkspace.Value(key, code);
                    if (before != value) result.changes.Add(new TranslationChange { key = key, language = code, before = before, after = value, smart = smart, sourceHash = Get("SourceHash") });
                }
            }
            return result;
        }
        private static HashSet<string> Arguments(string text) => new HashSet<string>(
            Regex.Matches(text, @"(?<!\{)\{([\w.]+)(?=[:}])").Cast<Match>().Select(x => x.Groups[1].Value));

        public static void Apply(TranslationImport import)
        {
            if (import.errors.Count > 0) throw new InvalidOperationException("Resolve CSV errors before importing.");
            // Guard against edits made after the preview was generated.
            foreach (var change in import.changes)
                if (LocalizationWorkspace.Value(change.key, change.language) != change.before ||
                    LocalizationWorkspace.SourceHash(LocalizationWorkspace.Value(change.key, "fa")) != change.sourceHash)
                    throw new InvalidOperationException("Translation changed since preview. Reload CSV: " + change.key);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (var change in import.changes)
            {
                LocalizationWorkspace.SetValue(change.key, change.language, change.after, change.smart);
                TranslationReviewState.Get().MarkReviewed(change.key, change.language);
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
        }
    }
}
