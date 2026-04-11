namespace OnlyT.Avalonia.Services.Report;

using System;
using System.IO;
using System.Threading.Tasks;
using OnlyT.Common.Services.DateTime;
using OnlyT.Report.Pdf;
using OnlyT.Avalonia.Utils;
using Serilog;

/// <summary>
/// Generates PDF timing reports from meeting timing data
/// </summary>
internal static class TimingReportGeneration
{
    /// <summary>
    /// Generate a timing report and return the file path (or null on failure)
    /// </summary>
    public static Task<string?> ExecuteAsync(
        ILocalTimingDataStoreService dataService,
        IDateTimeService dateTimeService,
        IQueryWeekendService queryWeekendService,
        bool weekendIncludesFriday,
        string? commandLineIdentifier = null)
    {
        return Task.Run(() =>
        {
            if (!dataService.ValidCurrentMeetingTimes())
            {
                dataService.PurgeCurrentMeetingTimes();
                Log.Information("Meeting times invalid so not stored");
                return null;
            }

            var outputFolder = FileUtils.GetTimingReportsFolder(commandLineIdentifier);

            Log.Information("Timer report output folder = {OutputFolder}", outputFolder);

            if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
            {
                return null;
            }

            var yearFolder = GetDatedOutputFolder(outputFolder, dateTimeService.Now());
            Directory.CreateDirectory(yearFolder);

            if (Directory.Exists(yearFolder))
            {
                var data = dataService.GetCurrentMeetingTimes();
                if (data != null)
                {
                    var historicalTimes = dataService.GetHistoricalMeetingTimes();

                    var report = new PdfTimingReport(
                        data,
                        historicalTimes,
                        queryWeekendService,
                        dateTimeService,
                        weekendIncludesFriday,
                        yearFolder);

                    return report.Execute();
                }
            }

            return null;
        });
    }

    private static string GetDatedOutputFolder(string outputFolder, DateTime now)
    {
        var monthFolderName = $"{now.Year}-{now.Month:D2}";
        return Path.Combine(outputFolder, now.Year.ToString(), monthFolderName);
    }
}
