namespace OnlyT.Avalonia.Services.TalkSchedule;

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Models;
using OnlyT.Avalonia.Utils;
using Serilog;

/// <summary>
/// The talk schedule when using "File-based" operating mode.
/// Reads schedule from talk_schedule.xml in the user's Documents/OnlyT folder.
/// </summary>
internal static class TalkScheduleFileBased
{
    private static readonly int StartId = 5000;

    public static List<TalkScheduleItem> Read(bool autoBell, string? selectedFile = null)
    {
        var path = ResolvePath(selectedFile);
        return ParseFile(path, autoBell);
    }

    /// <summary>
    /// Parses a schedule XML file at the given absolute path. Returns an empty
    /// list when the file is missing or malformed. Extracted from <see cref="Read"/>
    /// so the parsing can be unit tested without touching the user's folders.
    /// </summary>
    internal static List<TalkScheduleItem> ParseFile(string path, bool autoBell)
    {
        var result = new List<TalkScheduleItem>();

        if (File.Exists(path))
        {
            try
            {
                var x = XDocument.Load(path);
                var items = x.Root?.Element("items");
                if (items != null)
                {
                    var talkId = StartId;

                    foreach (XElement elem in items.Elements("item"))
                    {
                        var name = (string?)elem.Attribute("name") ?? $"Unknown name {talkId}";
                        var sectionNameInternal = (string?)elem.Attribute("section") ?? $"Unknown section {talkId}";
                        var sectionNameLocalised = (string?)elem.Attribute("section") ?? $"Unknown section {talkId}";

                        result.Add(new TalkScheduleItem(talkId, name, sectionNameInternal, sectionNameLocalised)
                        {
                            CountUp = AttributeToNullableBool(elem.Attribute("countup"), null),
                            ClosingSecs = AttributeToInt(elem.Attribute("closingSecs") ?? elem.Attribute("closing"), TalkScheduleItem.DefaultClosingSecs),
                            OriginalDuration = AttributeToDuration(elem.Attribute("duration")),
                            Editable = AttributeToBool(elem.Attribute("editable"), false),
                            BellApplicable = AttributeToBool(elem.Attribute("bell"), false),
                            AutoBell = autoBell,
                            PersistFinalTimerValue = AttributeToBool(elem.Attribute("persist"), false)
                        });

                        ++talkId;
                    }
                }

                Log.Information("Loaded {Count} items from talk schedule file: {Path}", result.Count, path);
            }
            catch (Exception ex)
            {
                result.Clear();
                Log.Warning(ex, "Unable to read talk schedule file: {Path}", path);
            }
        }
        else
        {
            Log.Information("Talk schedule file not found: {Path}", path);
        }

        return result;
    }

    /// <summary>
    /// Resolves the schedule file path. If a selected template filename is
    /// provided, it is looked up in the Schedules folder. Otherwise falls
    /// back to the legacy talk_schedule.xml for backward compatibility.
    /// </summary>
    private static string ResolvePath(string? selectedFile)
    {
        if (!string.IsNullOrEmpty(selectedFile))
        {
            var templatePath = System.IO.Path.Combine(
                FileUtils.GetScheduleTemplatesFolder(), selectedFile);
            if (File.Exists(templatePath))
            {
                return templatePath;
            }
            Log.Warning("Selected schedule template not found: {File}, falling back to default", selectedFile);
        }
        return FileUtils.GetTalkSchedulePath();
    }

    private static bool? AttributeToNullableBool(XAttribute? attribute, bool? defaultValue)
    {
        return string.IsNullOrEmpty(attribute?.Value)
            ? defaultValue
            : Convert.ToBoolean(attribute.Value);
    }

    private static int AttributeToInt(XAttribute? attribute, int defaultValue)
    {
        return string.IsNullOrEmpty(attribute?.Value)
            ? defaultValue
            : Convert.ToInt32(attribute.Value);
    }

    private static bool AttributeToBool(XAttribute? attribute, bool defaultValue)
    {
        return attribute == null
            ? defaultValue
            : Convert.ToBoolean(attribute.Value);
    }

    private static TimeSpan AttributeToDuration(XAttribute? attribute)
    {
        return attribute == null
            ? TimeSpan.Zero
            : StringToTimeSpan(attribute.Value);
    }

    private static TimeSpan StringToTimeSpan(string s)
    {
        return TimeSpan.TryParse(s, out var result)
            ? result
            : default;
    }
}
