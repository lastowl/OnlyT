using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JwTranslationExtractor.Models;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for extracting official meeting part names from the jw.org meeting workbook
/// </summary>
/// <remarks>
/// The English workbook EPUB (from the pub-media API) identifies a weekly schedule document.
/// jw.org's finder serves that same document in any language, and the schedule's headings appear
/// in the same order in every language, so each part name is located by the position of its
/// English heading rather than by guessing localized page addresses or matching translated text.
/// </remarks>
public class WorkbookScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private readonly Dictionary<string, LanguageTranslations> _cache = new(StringComparer.OrdinalIgnoreCase);
    private ReferenceSchedule? _reference;
    private const int DelayBetweenRequestsMs = 1000;

    private const string PubMediaUrl =
        "https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS?pub=mwb&langwritten={0}&issue={1}&fileformat=EPUB&output=json&alllangs=0";
    private const string FinderUrl = "https://www.jw.org/finder?wtlocale={0}&docid={1}&srcid=share";

    // English part names as they appear in the workbook schedule, and the resource key each one fills
    private static readonly (string Key, string English)[] MeetingParts =
    {
        ("TALK_OPENING_COMMENTS", "Opening Comments"),
        ("TALK_DIGGING", "Spiritual Gems"),
        ("TALK_READING", "Bible Reading"),
        ("TALK_CONG_STUDY", "Congregation Bible Study"),
        ("TALK_CONCLUDING_COMMENTS", "Concluding Comments"),
    };

    /// <summary>
    /// Workbook issue to read (yyyyMM). When not set, the current issue is used,
    /// falling back to earlier issues.
    /// </summary>
    public string? Issue { get; set; }

    public WorkbookScraperService()
    {
        // Use SocketsHttpHandler with specific settings for better compatibility
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            ResponseDrainTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);

        // Configure retry with exponential backoff
        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                OnRetry = args =>
                {
                    Log.Warning("Retry {Attempt} for request, waiting {Delay}s",
                        args.AttemptNumber, args.RetryDelay.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Extracts meeting part names for a specific language from the meeting workbook
    /// </summary>
    public async Task<LanguageTranslations> ExtractTranslationsAsync(JwLanguage language)
    {
        if (_cache.TryGetValue(language.LangCode, out var cached))
        {
            return cached;
        }

        var translations = new LanguageTranslations
        {
            LanguageCode = language.LangCode,
            CultureCode = language.CultureCode ?? language.LangCode,
            LanguageName = language.VernacularName
        };

        try
        {
            var reference = await GetReferenceScheduleAsync();
            var url = string.Format(FinderUrl, language.LangCode, reference.DocId);
            var headings = GetScheduleHeadings(await LoadHtmlAsync(url));

            if (headings.Count != reference.HeadingCount)
            {
                Log.Warning("  Schedule layout differs for {Lang} ({Count} headings, expected {Expected}); skipping",
                    language.LangCode, headings.Count, reference.HeadingCount);
            }
            else
            {
                foreach (var part in reference.Parts)
                {
                    var name = ExtractPartName(headings, part);
                    if (string.IsNullOrEmpty(name))
                    {
                        Log.Warning("  {Key} not found for {Lang}", part.Key, language.LangCode);
                        continue;
                    }

                    translations.Translations.Add(new ExtractedTranslation
                    {
                        Key = part.Key,
                        Value = name,
                        LanguageCode = language.LangCode,
                        SourceUrl = url
                    });
                }
            }

            // jw.org serves the English page when a document isn't available in a language
            var isEnglish = language.LangCode.Equals("E", StringComparison.OrdinalIgnoreCase);
            if (!isEnglish && translations.Translations.Count > 0 &&
                translations.Translations.All(t => t.Value == reference.EnglishNames[t.Key]))
            {
                Log.Warning("  Schedule is not translated into {Lang}; ignoring", language.LangCode);
                translations.Translations.Clear();
            }

            // Add delay to be respectful to the server
            await Task.Delay(DelayBetweenRequestsMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract translations for language {Lang}", language.LangCode);
        }

        _cache[language.LangCode] = translations;
        return translations;
    }

    private async Task<ReferenceSchedule> GetReferenceScheduleAsync()
    {
        if (_reference != null)
        {
            return _reference;
        }

        foreach (var issue in GetCandidateIssues())
        {
            var epubUrl = await GetEpubUrlAsync("E", issue);
            var docId = epubUrl == null ? null : await FindScheduleDocIdAsync(epubUrl);
            if (docId == null)
            {
                continue;
            }

            var headings = GetScheduleHeadings(await LoadHtmlAsync(string.Format(FinderUrl, "E", docId)));
            var parts = new List<ReferencePart>();
            var englishNames = new Dictionary<string, string>();

            for (var index = 0; index < headings.Count; index++)
            {
                var segments = SplitSegments(headings[index].InnerText);
                for (var segment = 0; segment < segments.Count; segment++)
                {
                    var name = CleanPartName(segments[segment]);
                    var match = MeetingParts.FirstOrDefault(p => p.English.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (match.Key != null && !englishNames.ContainsKey(match.Key))
                    {
                        parts.Add(new ReferencePart(match.Key, index, headings[index].Name, segment, segments.Count));
                        englishNames[match.Key] = name;
                    }
                }
            }

            if (parts.Count == MeetingParts.Length)
            {
                Log.Information("Using workbook issue {Issue}, schedule document {DocId}", issue, docId);
                _reference = new ReferenceSchedule(docId, headings.Count, parts, englishNames);
                return _reference;
            }

            Log.Warning("Schedule document {DocId} matched only {Count} of {Total} parts",
                docId, parts.Count, MeetingParts.Length);
        }

        throw new InvalidOperationException("Could not find an English meeting schedule to use as a reference");
    }

    private IEnumerable<string> GetCandidateIssues()
    {
        if (!string.IsNullOrEmpty(Issue))
        {
            yield return Issue;
            yield break;
        }

        // Workbooks are bimonthly, with issues dated January, March, May, ...
        var today = DateTime.UtcNow;
        var current = new DateTime(today.Year, today.Month - (today.Month - 1) % 2, 1);
        for (var i = 0; i < 4; i++)
        {
            yield return current.AddMonths(-2 * i).ToString("yyyyMM", CultureInfo.InvariantCulture);
        }
    }

    private async Task<string?> GetEpubUrlAsync(string langCode, string issue)
    {
        try
        {
            var json = JObject.Parse(await FetchWithRetryAsync(string.Format(PubMediaUrl, langCode, issue)));
            return json["files"] is JObject files
                ? files[langCode]?["EPUB"]?.First?["file"]?["url"]?.Value<string>()
                : null;
        }
        catch (HttpRequestException)
        {
            // Issue not published yet
            return null;
        }
    }

    private async Task<string?> FindScheduleDocIdAsync(string epubUrl)
    {
        var bytes = await _httpClient.GetByteArrayAsync(epubUrl);
        using var epub = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        // Each weekly schedule is a separate document named by its document id
        foreach (var entry in epub.Entries.Where(e => Regex.IsMatch(e.Name, @"^\d+\.xhtml$")).OrderBy(e => e.Name))
        {
            using var reader = new StreamReader(entry.Open());
            var content = await reader.ReadToEndAsync();
            if (MeetingParts.All(p => content.Contains(p.English)))
            {
                return Path.GetFileNameWithoutExtension(entry.Name);
            }
        }

        return null;
    }

    // The schedule's own headings carry paragraph ids ("p3", "p11", ...). The ids can shift between
    // languages, but the headings appear in the same order, so they are matched by position.
    private static List<HtmlNode> GetScheduleHeadings(HtmlDocument doc) =>
        doc.DocumentNode.Descendants()
            .Where(n => n.Name is "h2" or "h3" && Regex.IsMatch(n.Id, @"^p\d+$"))
            .ToList();

    private static string? ExtractPartName(List<HtmlNode> headings, ReferencePart part)
    {
        var heading = headings[part.HeadingIndex];
        if (heading.Name != part.TagName)
        {
            return null;
        }

        // Only trust the heading if it is split the same way as the English one
        var segments = SplitSegments(heading.InnerText);
        return segments.Count == part.SegmentCount ? CleanPartName(segments[part.Segment]) : null;
    }

    // Headings such as "Song 1 and Prayer | Opening Comments (1 min.)" hold several items
    private static List<string> SplitSegments(string text) =>
        HtmlEntity.DeEntitize(text)
            .Split('|', '｜')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private static string CleanPartName(string text)
    {
        text = Regex.Replace(text, "[­​‎‏‪-‮⁦-⁩]", ""); // soft hyphens, zero-width spaces, bidi marks
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"^\d+\s*[.)．\-–။]\s*", ""); // "2. Spiritual Gems", "٢- جواهر روحية", "၂။ ..."
        text = Regex.Replace(text, @"^[(（][^()（）]*[)）]\s*|\s*[(（][^()（）]*[)）][.。]?$", ""); // "(1 min.)", "（3分）", "(1 min)."
        return text.Trim();
    }

    private async Task<HtmlDocument> LoadHtmlAsync(string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await FetchWithRetryAsync(url));

        // Drop ruby annotations (furigana, pinyin) so only the base text remains
        foreach (var annotation in doc.DocumentNode.Descendants().Where(n => n.Name is "rt" or "rp").ToList())
        {
            annotation.Remove();
        }

        return doc;
    }

    private async Task<string> FetchWithRetryAsync(string url)
    {
        var response = await _retryPipeline.ExecuteAsync(async ct =>
            await _httpClient.GetAsync(url, ct));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private sealed record ReferencePart(string Key, int HeadingIndex, string TagName, int Segment, int SegmentCount);

    private sealed record ReferenceSchedule(string DocId, int HeadingCount, List<ReferencePart> Parts, Dictionary<string, string> EnglishNames);
}
