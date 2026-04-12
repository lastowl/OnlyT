using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using OnlyT.Avalonia.Models;
using Serilog;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// Serializes a talk schedule to the file-based XML format so that the
/// current Automatic/Manual schedule can be saved as a reusable template.
/// </summary>
public static class ScheduleExporter
{
    public static void Export(IEnumerable<TalkScheduleItem> talks, string filePath)
    {
        var items = new XElement("items");

        foreach (var talk in talks)
        {
            var item = new XElement("item",
                new XAttribute("name", talk.Name ?? string.Empty),
                new XAttribute("duration", talk.OriginalDuration.ToString(@"hh\:mm\:ss")),
                new XAttribute("editable", talk.Editable.ToString().ToLower()),
                new XAttribute("bell", talk.BellApplicable.ToString().ToLower()));

            if (!string.IsNullOrEmpty(talk.MeetingSectionNameInternal))
            {
                item.Add(new XAttribute("section", talk.MeetingSectionNameInternal));
            }

            if (talk.CountUp.HasValue)
            {
                item.Add(new XAttribute("countup", talk.CountUp.Value.ToString().ToLower()));
            }

            if (talk.PersistFinalTimerValue)
            {
                item.Add(new XAttribute("persist", "true"));
            }

            if (talk.ClosingSecs != 30)
            {
                item.Add(new XAttribute("closing", talk.ClosingSecs));
            }

            items.Add(item);
        }

        var doc = new XDocument(new XElement("meeting", items));
        doc.Save(filePath);

        Log.Information("Exported schedule template to {Path} ({Count} talks)",
            filePath, items.Elements().Count());
    }

    public static string[] GetAvailableTemplates()
    {
        var folder = FileUtils.GetScheduleTemplatesFolder();
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.GetFiles(folder, "*.xml");
    }
}
