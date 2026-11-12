using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RTLLocalizationStudio.Editor
{
    // RFC 4180: quoted commas, multiline cells, escaped quotes and UTF-8 BOM.
    public static class TranslationCsv
    {
        public static string Write(IEnumerable<string[]> rows)
        {
            return string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(cell =>
                "\"" + (cell ?? "").Replace("\"", "\"\"") + "\"")))) + "\r\n";
        }

        public static List<string[]> Read(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false;
            text = (text ?? "").TrimStart('\ufeff');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else { quoted = false; closed = true; }
                    }
                    else cell.Append(c);
                    continue;
                }
                if (c == '"' && cell.Length == 0 && !closed) { quoted = true; continue; }
                if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString()); cell.Clear(); closed = false;
                    if (c != ',')
                    {
                        if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                        row.Clear();
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                    continue;
                }
                if (closed || c == '"') throw new FormatException("Invalid CSV quoting at character " + i);
                cell.Append(c);
            }
            if (quoted) throw new FormatException("Unclosed quoted CSV cell.");
            if (cell.Length > 0 || row.Count > 0 || closed) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }
    }
}
