using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RTLLocalizationStudio
{
    /// <summary>Index authored Persian source strings once; look up only when UI text changes.</summary>
    public static class SourceTextIndex
    {
        public sealed class Match
        {
            public string key;
            public object[] arguments;
        }
        private sealed class Candidate
        {
            public string key, source;
            public Regex pattern;
            public int[] slots;
        }
        private static readonly Dictionary<string, List<Candidate>> exact = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
        private static readonly List<Candidate> templates = new List<Candidate>();
        private static readonly Dictionary<long, string> keysById = new Dictionary<long, string>();
        private static bool started;
        public static bool IsReady { get; private set; }
        public static event Action Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { exact.Clear(); templates.Clear(); keysById.Clear(); started = IsReady = false; Ready = null; }
        public static string KeyForId(long id) => keysById.TryGetValue(id, out var key) ? key : null;

        public static void Initialize()
        {
            if (started || !Localization.Profile) return;
            started = true;
            var initialization = LocalizationSettings.InitializationOperation;
            if (initialization.IsDone) Load();
            else initialization.Completed += _ => Load();
        }
        private static void Load()
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(Localization.Profile.defaultLanguage);
            if (!locale) { started = false; return; }
            var operation = LocalizationSettings.StringDatabase.GetTableAsync(Localization.Profile.table, locale);
            if (operation.IsDone) Loaded(operation);
            else operation.Completed += Loaded;
        }
        private static void Loaded(AsyncOperationHandle<StringTable> operation)
        {
            if (operation.Status != AsyncOperationStatus.Succeeded || !operation.Result) { started = false; return; }
            Rebuild(operation.Result);
        }

        /// <summary>Also supports rebuilding after explicitly replacing translation data.</summary>
        public static void Rebuild(StringTable sourceTable)
        {
            exact.Clear(); templates.Clear(); keysById.Clear();
            foreach (var entry in sourceTable.Values)
            {
                if (string.IsNullOrEmpty(entry.Value) || string.IsNullOrEmpty(entry.Key)) continue;
                var candidate = new Candidate { key = entry.Key, source = entry.Value };
                keysById[entry.KeyId] = entry.Key;
                if (!exact.TryGetValue(entry.Value, out var list)) exact[entry.Value] = list = new List<Candidate>();
                list.Add(candidate);
                BuildNumericPattern(candidate);
                if (candidate.pattern != null) templates.Add(candidate);
            }
            IsReady = true;
            Ready?.Invoke();
        }
        private static void BuildNumericPattern(Candidate candidate)
        {
            // Deliberately accepts numbered numeric placeholders only, not arbitrary player names.
            var matches = Regex.Matches(candidate.source, @"(?<!\{)\{([0-7])(?::[^{}]+)?\}(?!\})");
            if (matches.Count == 0) return;
            var expression = new StringBuilder("\\A");
            var slots = new List<int>();
            int previous = 0;
            foreach (System.Text.RegularExpressions.Match item in matches)
            {
                string literal = candidate.source.Substring(previous, item.Index - previous);
                if (literal.Contains("{") || literal.Contains("}")) return;
                expression.Append(Regex.Escape(literal));
                int slot = int.Parse(item.Groups[1].Value, CultureInfo.InvariantCulture);
                if (slots.Contains(slot)) expression.Append(@"\k<n" + slot + ">");
                else
                {
                    expression.Append("(?<n" + slot + @">[+\-−]?[0-9۰-۹٠-٩]+(?:[,.٬٫][0-9۰-۹٠-٩]+)*)");
                    slots.Add(slot);
                }
                previous = item.Index + item.Length;
            }
            string tail = candidate.source.Substring(previous);
            if (tail.Contains("{") || tail.Contains("}")) return;
            expression.Append(Regex.Escape(tail)).Append("\\z");
            candidate.pattern = new Regex(expression.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
            candidate.slots = slots.ToArray();
        }
        public static bool TryResolve(string source, string preferredKey, out Match result)
        {
            result = null;
            if (!IsReady || string.IsNullOrEmpty(source)) return false;
            if (exact.TryGetValue(source, out var matches))
            {
                var candidate = matches.FirstOrDefault(x => x.key == preferredKey) ?? (matches.Count == 1 ? matches[0] : null);
                // A raw template needs actual arguments; don't send it to the formatter prematurely.
                if (candidate == null || candidate.pattern != null) return false;
                result = new Match { key = candidate.key, arguments = Array.Empty<object>() };
                return true;
            }
            var numericMatches = new List<Match>();
            foreach (var candidate in templates)
            {
                System.Text.RegularExpressions.Match match;
                try { match = candidate.pattern.Match(source); }
                catch (RegexMatchTimeoutException) { continue; }
                if (!match.Success) continue;
                var args = new object[candidate.slots.Max() + 1];
                bool valid = true;
                foreach (int slot in candidate.slots)
                {
                    string number = Localization.FormatDigits(match.Groups["n" + slot].Value, DigitStyle.Latin)
                        .Replace('−', '-').Replace('٬', ',').Replace('٫', '.');
                    if (!decimal.TryParse(number, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal parsed)) { valid = false; break; }
                    args[slot] = parsed;
                }
                if (!valid) continue;
                var resolved = new Match { key = candidate.key, arguments = args };
                if (candidate.key == preferredKey) { result = resolved; return true; }
                numericMatches.Add(resolved);
            }
            if (numericMatches.Count != 1) return false;
            result = numericMatches[0];
            return true;
        }
    }
}
