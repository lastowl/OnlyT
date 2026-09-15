using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Updates individual values in an existing .resx file without regenerating it,
/// so comments, key order, encoding and line endings are preserved
/// </summary>
public class ResxInPlaceUpdater
{
    /// <summary>
    /// The script of a resx file's translated values (those that differ from the base English),
    /// or null if nothing is translated yet
    /// </summary>
    public string? DetectTranslationScript(string resxPath, IReadOnlyDictionary<string, string> baseValues)
    {
        var (text, _, _) = ReadFile(resxPath);
        return TranslationScript(ReadValues(text), baseValues);
    }

    /// <summary>
    /// Sets the given values in a resx file. Keys missing from the base resx are ignored;
    /// keys missing from the file are added.
    /// </summary>
    /// <returns>The number of values changed, or -1 if the file was skipped</returns>
    public int Update(
        string resxPath,
        IReadOnlyDictionary<string, string> newValues,
        IReadOnlyDictionary<string, string> baseValues,
        bool dryRun)
    {
        var fileName = Path.GetFileName(resxPath);
        var (text, hasBom, usesCrLf) = ReadFile(resxPath);
        var current = ReadValues(text);

        // Guard against a wrong language mapping writing text in another script
        var fileScript = TranslationScript(current, baseValues);
        var newScript = ScriptDetector.Dominant(string.Concat(newValues.Values));
        if (fileScript != null && newScript != null && fileScript != newScript)
        {
            Log.Warning("Skipping {File}: its translations are {FileScript} script but the workbook text is {NewScript}",
                fileName, fileScript, newScript);
            return -1;
        }

        var changes = 0;
        var additions = new StringBuilder();

        foreach (var (key, value) in newValues)
        {
            if (!baseValues.ContainsKey(key))
            {
                continue;
            }

            var exists = current.TryGetValue(key, out var oldValue);
            if (exists && oldValue == value)
            {
                continue;
            }

            if (exists)
            {
                var pattern = new Regex($@"(<data name=""{Regex.Escape(key)}""[^>]*>\s*<value>).*?(</value>)", RegexOptions.Singleline);
                if (!pattern.IsMatch(text))
                {
                    Log.Warning("Could not update {Key} in {File}", key, fileName);
                    continue;
                }

                text = pattern.Replace(text, m => m.Groups[1].Value + Escape(value) + m.Groups[2].Value, 1);
            }
            else
            {
                additions.Append($"\n  <data name=\"{key}\" xml:space=\"preserve\">\n    <value>{Escape(value)}</value>\n  </data>");
            }

            Log.Information("  {File} {Key}: {Old} -> {New}", fileName, key, exists ? oldValue : "(missing)", value);
            changes++;
        }

        if (additions.Length > 0)
        {
            var lastData = text.LastIndexOf("</data>", StringComparison.Ordinal);
            var insertAt = lastData >= 0
                ? lastData + "</data>".Length
                : text.LastIndexOf("</root>", StringComparison.Ordinal);
            text = text.Insert(insertAt, additions.ToString());
        }

        if (changes == 0 || dryRun)
        {
            return changes;
        }

        // Throws rather than writing a broken file
        XDocument.Parse(text);

        if (usesCrLf)
        {
            text = text.Replace("\n", "\r\n");
        }

        using var stream = File.Create(resxPath);
        if (hasBom)
        {
            stream.Write(Encoding.UTF8.Preamble);
        }

        stream.Write(Encoding.UTF8.GetBytes(text));
        return changes;
    }

    // Returns the file text with LF line endings, plus how it was originally encoded
    private static (string Text, bool HasBom, bool UsesCrLf) ReadFile(string resxPath)
    {
        var bytes = File.ReadAllBytes(resxPath);
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(hasBom ? bytes[3..] : bytes);
        return (text.Replace("\r\n", "\n"), hasBom, text.Contains("\r\n"));
    }

    private static Dictionary<string, string> ReadValues(string text) =>
        XDocument.Parse(text).Root?.Elements("data")
            .Where(d => d.Attribute("name") != null)
            .GroupBy(d => d.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.Last().Element("value")?.Value ?? string.Empty)
        ?? new Dictionary<string, string>();

    private static string? TranslationScript(
        Dictionary<string, string> values,
        IReadOnlyDictionary<string, string> baseValues) =>
        ScriptDetector.Dominant(string.Concat(values
            .Where(kvp => baseValues.TryGetValue(kvp.Key, out var english) && kvp.Value != english)
            .Select(kvp => kvp.Value)));

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
