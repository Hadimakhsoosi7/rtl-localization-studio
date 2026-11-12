using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RTLLocalizationStudio.Editor
{
    /// <summary>Conservative literal extraction; never executes or rewrites game scripts.</summary>
    public static class ScriptSourceExtractor
    {
        private sealed class Literal { public int start, end; public string value; public bool interpolated; }
        public static List<string> Extract(string source)
        {
            var mask = source.ToCharArray();
            var literals = new List<Literal>();
            void Hide(int start, int end) { for (int j = start; j < end; j++) if (mask[j] != '\n' && mask[j] != '\r') mask[j] = ' '; }
            for (int i = 0; i < source.Length;)
            {
                int start = i;
                if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
                { while (i < source.Length && source[i] != '\n') i++; Hide(start, i); continue; }
                if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
                { int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal); i = end < 0 ? source.Length : end + 2; Hide(start, i); continue; }
                if (source[i] == '\'')
                {
                    i++;
                    while (i < source.Length) { if (source[i++] == '\\' && i < source.Length) i++; else if (source[i - 1] == '\'') break; }
                    Hide(start, i); continue;
                }
                bool verbatim = false, interpolated = false;
                int quote = i;
                if (source[quote] == '@' || source[quote] == '$')
                {
                    for (int n = 0; n < 2 && quote < source.Length && (source[quote] == '@' || source[quote] == '$'); n++)
                    { verbatim |= source[quote] == '@'; interpolated |= source[quote] == '$'; quote++; }
                }
                if (quote >= source.Length || source[quote] != '"') { i++; continue; }
                i = quote + 1;
                var value = new StringBuilder();
                bool valid = true, closed = false;
                while (i < source.Length)
                {
                    char c = source[i++];
                    if (c == '"')
                    {
                        if (verbatim && i < source.Length && source[i] == '"') { value.Append('"'); i++; continue; }
                        closed = true; break;
                    }
                    if (c == '\\' && !verbatim)
                    {
                        if (i >= source.Length) { valid = false; break; }
                        char escaped = source[i++];
                        switch (escaped)
                        {
                            case 'n': value.Append('\n'); break;
                            case 'r': value.Append('\r'); break;
                            case 't': value.Append('\t'); break;
                            case '0': value.Append('\0'); break;
                            case '\\': value.Append('\\'); break;
                            case '"': value.Append('"'); break;
                            case '\'': value.Append('\''); break;
                            case 'u':
                                if (i + 4 <= source.Length && int.TryParse(source.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                                { value.Append((char)code); i += 4; } else valid = false;
                                break;
                            default: valid = false; break;
                        }
                    }
                    else value.Append(c);
                }
                Hide(start, i);
                if (valid && closed) literals.Add(new Literal { start = start, end = i, value = value.ToString(), interpolated = interpolated });
            }
            string codeMask = new string(mask);
            var results = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match assignment in Regex.Matches(codeMask, @"\.text\s*=(?!=)|\.SetText\s*\("))
            {
                int expression = assignment.Index + assignment.Length;
                bool method = assignment.Value.Contains("SetText");
                int end = codeMask.IndexOf(';', expression);
                if (end < 0) end = codeMask.Length;
                foreach (var literal in literals.Where(x => x.start >= expression && x.end <= end))
                {
                    if (literal.interpolated || string.IsNullOrWhiteSpace(literal.value) || !literal.value.Any(char.IsLetter)) continue;
                    string before = codeMask.Substring(expression, literal.start - expression).Trim();
                    string after = codeMask.Substring(literal.end, end - literal.end).TrimStart();
                    bool first = before.Length == 0;
                    bool ternary = !method && before.Length > 0 && (before[before.Length - 1] == '?' || before[before.Length - 1] == ':');
                    bool full = method ? first && after.Length > 0 && (after[0] == ',' || after[0] == ')') :
                        (first || ternary) && (after.Length == 0 || after[0] == ':' || after[0] == ')');
                    if (full) results.Add(literal.value);
                    if (method) break;
                }
            }
            return results.ToList();
        }
    }
}
