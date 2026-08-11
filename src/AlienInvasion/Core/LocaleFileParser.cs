using System.Collections.Generic;

namespace AlienInvasion.Core
{
    /// <summary>
    /// Parser for the community-translation locale files (no UnityEngine dependency, no file IO -
    /// reading the file is the Game layer's job, in LocaleLoader).
    ///
    /// File format (UTF-8 text, one entry per line):
    ///   key = value
    /// - Whitespace around the key and the value is trimmed.
    /// - Blank lines and lines starting with '#' are comments.
    /// - The value may contain "\n" escapes for line breaks, so a multi-line default still fits on
    ///   one line and the file round-trips through the template writer.
    /// - The FIRST '=' splits key from value, so '=' may appear freely inside the value.
    /// - Later entries for the same key win. That is the simplest rule to explain to someone
    ///   hand-editing a file, and it makes a duplicated key harmless rather than an error.
    /// </summary>
    public static class LocaleFileParser
    {
        public static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;   // no separator, or an empty key: ignore the line

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;

                result[key] = Unescape(value);
            }
            return result;
        }

        /// <summary>Turns the "\n" escape into a real newline; "\\n" stays a literal backslash-n.</summary>
        public static string Unescape(string value)
        {
            if (value == null || value.IndexOf('\\') < 0) return value;

            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The inverse of Unescape, used by the template writer so a multi-line default survives
        /// the one-entry-per-line format.
        /// </summary>
        public static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\r\n", "\\n").Replace("\n", "\\n");
        }
    }
}
