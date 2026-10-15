using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BigWalkDLSS;

// Lossless editor for OptiScaler options owned by the Home panel.
internal sealed class IniSettings
{
    private readonly Encoding _encoding;
    private readonly byte[] _preamble;
    private readonly List<(string Text, string Ending)> _lines = new();
    internal byte[] Original { get; }

    internal IniSettings(byte[] bytes)
    {
        Original = (byte[])bytes.Clone();
        int offset = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        { _encoding = new UTF8Encoding(false, true); offset = 3; }
        else if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
        { _encoding = new UnicodeEncoding(false, false, true); offset = 2; }
        else if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
        { _encoding = new UnicodeEncoding(true, false, true); offset = 2; }
        else _encoding = new UTF8Encoding(false, true);
        _preamble = new byte[offset];
        Array.Copy(bytes, _preamble, offset);
        string text = _encoding.GetString(bytes, offset, bytes.Length - offset);
        if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Unsupported INI encoding.");
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n') continue;
            int end = i;
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            _lines.Add((text.Substring(start, end - start), text.Substring(end, i - end + 1)));
            start = i + 1;
        }
        if (start < text.Length) _lines.Add((text.Substring(start), ""));
    }

    internal string? Get(string section, string key)
    {
        int index = Find(section, key, out _);
        if (index < 0) return null;
        string line = _lines[index].Text;
        string value = line.Substring(line.IndexOf('=') + 1);
        return value.Substring(0, CommentAt(value)).Trim();
    }

    internal byte[] WithValue(string section, string key, string value)
    {
        if (section.IndexOfAny(new[] { '[', ']', '\r', '\n' }) >= 0 || string.IsNullOrWhiteSpace(section) ||
            key.IndexOfAny(new[] { '=', '\r', '\n' }) >= 0 || string.IsNullOrWhiteSpace(key) ||
            value.IndexOfAny(new[] { '\r', '\n', ';', '#' }) >= 0)
            throw new ArgumentException("Invalid INI option.");
        var lines = new List<(string Text, string Ending)>(_lines);
        int index = Find(section, key, out int sectionEnd);
        if (index >= 0)
        {
            string line = lines[index].Text;
            int equals = line.IndexOf('=');
            string tail = line.Substring(equals + 1);
            int comment = CommentAt(tail);
            int start = 0, end = comment;
            while (start < end && char.IsWhiteSpace(tail[start])) start++;
            while (end > start && char.IsWhiteSpace(tail[end - 1])) end--;
            string separator = comment < tail.Length && end == comment ? " " : "";
            lines[index] = (line.Substring(0, equals + 1) + tail.Substring(0, start) + value + separator + tail.Substring(end), lines[index].Ending);
        }
        else
        {
            string newline = "\r\n";
            foreach (var line in lines) if (line.Ending.Length > 0) { newline = line.Ending; break; }
            if (sectionEnd < 0)
            {
                EndPrevious(lines, lines.Count, newline);
                lines.Add(("[" + section + "]", newline));
                lines.Add((key + "=" + value, newline));
            }
            else
            {
                EndPrevious(lines, sectionEnd, newline);
                lines.Insert(sectionEnd, (key + "=" + value, newline));
            }
        }
        var text = new StringBuilder();
        foreach (var line in lines) text.Append(line.Text).Append(line.Ending);
        byte[] body = _encoding.GetBytes(text.ToString());
        byte[] result = new byte[_preamble.Length + body.Length];
        Array.Copy(_preamble, result, _preamble.Length);
        Array.Copy(body, 0, result, _preamble.Length, body.Length);
        return result;
    }

    internal byte[] WithValues(params (string Section, string Key, string Value)[] changes)
    {
        // Build the whole profile before saving anything. A malformed later option
        // must not leave a partially selected provider on disk.
        var next = this;
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            if (!targets.Add(change.Section + "\0" + change.Key))
                throw new InvalidDataException("Duplicate option in configuration batch.");
            next = new IniSettings(next.WithValue(change.Section, change.Key, change.Value));
        }
        return next.Original;
    }

    private static void EndPrevious(List<(string Text, string Ending)> lines, int index, string newline)
    {
        if (index > 0 && lines[index - 1].Ending.Length == 0) lines[index - 1] = (lines[index - 1].Text, newline);
    }

    private int Find(string section, string key, out int sectionEnd)
    {
        bool inside = false, foundSection = false;
        int found = -1;
        sectionEnd = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            string text = _lines[i].Text.Trim();
            if (text.Length == 0 || text[0] == ';' || text[0] == '#') continue;
            if (text[0] == '[')
            {
                int close = text.IndexOf(']');
                if (close < 0) throw new InvalidDataException("Malformed INI section.");
                if (inside) sectionEnd = i;
                inside = string.Equals(text.Substring(1, close - 1).Trim(), section, StringComparison.OrdinalIgnoreCase);
                if (inside)
                {
                    if (foundSection) throw new InvalidDataException("Duplicate target INI section.");
                    foundSection = true;
                    sectionEnd = _lines.Count;
                }
                continue;
            }
            int equals = text.IndexOf('=');
            if (!inside || equals < 0 || !string.Equals(text.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;
            if (found >= 0) throw new InvalidDataException("Duplicate target INI key.");
            found = i;
        }
        return found;
    }

    private static int CommentAt(string text)
    {
        for (int i = 0; i < text.Length; i++)
            if ((text[i] == ';' || text[i] == '#') && (i == 0 || char.IsWhiteSpace(text[i - 1]))) return i;
        return text.Length;
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static void RejectReparsePath(string path)
    {
        string? current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked configuration paths are unsupported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
    }

    internal static void SaveAtomic(string path, byte[] expected, byte[] replacement, string backupDirectory)
    {
        RejectReparsePath(path);
        RejectReparsePath(backupDirectory);
        if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != Hash(expected))
            throw new IOException("The INI changed outside Home. Reload its settings before trying again.");
        Directory.CreateDirectory(backupDirectory);
        string backup = Path.Combine(backupDirectory, "OptiScaler-" + Hash(expected) + ".ini");
        RejectReparsePath(backup);
        if (File.Exists(backup))
        {
            if (Hash(File.ReadAllBytes(backup)) != Hash(expected)) throw new IOException("Configuration backup does not match.");
        }
        else
        {
            using var copy = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            copy.Write(expected, 0, expected.Length);
            copy.Flush(true);
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { output.Write(replacement, 0, replacement.Length); output.Flush(true); }
            RejectReparsePath(path);
            if (Hash(File.ReadAllBytes(path)) != Hash(expected)) throw new IOException("The INI changed outside Home. Reload its settings before trying again.");
            File.Replace(temporary, path, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
