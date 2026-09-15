using System.Text;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Coarse writing-script classification, used to match resource files to jw.org language
/// variants and to avoid writing text in the wrong script
/// </summary>
public static class ScriptDetector
{
    /// <summary>
    /// The script used by most letters in the text, or null if it has no letters
    /// </summary>
    public static string? Dominant(string text)
    {
        var counts = new Dictionary<string, int>();
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                continue;
            }

            var script = rune.Value switch
            {
                < 0x0370 or (>= 0x1E00 and < 0x1F00) => "Latin",
                < 0x0400 => "Greek",
                < 0x0530 => "Cyrillic",
                >= 0x0590 and < 0x0600 => "Hebrew",
                >= 0x0600 and < 0x0780 => "Arabic",
                _ => "Other"
            };
            counts[script] = counts.GetValueOrDefault(script) + 1;
        }

        return counts.Count == 0 ? null : counts.MaxBy(kvp => kvp.Value).Key;
    }

    /// <summary>
    /// Maps a jw.org script name (e.g. "ROMAN", "CYRILLIC") to the categories used by <see cref="Dominant"/>
    /// </summary>
    public static string FromJwScript(string? jwScript) => jwScript?.ToUpperInvariant() switch
    {
        "ROMAN" => "Latin",
        "GREEK" => "Greek",
        "CYRILLIC" => "Cyrillic",
        "HEBREW" => "Hebrew",
        "ARABIC" or "URDU" or "SINDHI" => "Arabic",
        _ => "Other"
    };
}
