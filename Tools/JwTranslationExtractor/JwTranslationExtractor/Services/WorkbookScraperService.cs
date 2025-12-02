using HtmlAgilityPack;
using JwTranslationExtractor.Models;
using Polly;
using Polly.Retry;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for scraping translations from jw.org meeting workbook pages
/// </summary>
public class WorkbookScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private const int DelayBetweenRequestsMs = 1000;

    // Known workbook URL patterns for different languages
    private static readonly Dictionary<string, string> WorkbookBaseUrls = new()
    {
        { "en", "https://www.jw.org/en/library/jw-meeting-workbook/" },
        { "es", "https://www.jw.org/es/biblioteca/guia-actividades/" },
        { "pt", "https://www.jw.org/pt/biblioteca/apostila-reuniao/" },
        { "fr", "https://www.jw.org/fr/biblioth%C3%A8que/cahier-vie-ministere/" },
        { "de", "https://www.jw.org/de/bibliothek/arbeitshefte/" },
    };

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
    /// Extracts translations for a specific language from the meeting workbook
    /// </summary>
    public async Task<LanguageTranslations> ExtractTranslationsAsync(JwLanguage language)
    {
        var translations = new LanguageTranslations
        {
            LanguageCode = language.LangCode,
            CultureCode = language.CultureCode ?? language.LangCode,
            LanguageName = language.VernacularName
        };

        try
        {
            // Find a workbook page for this language
            var workbookUrl = await FindWorkbookUrlAsync(language);

            if (string.IsNullOrEmpty(workbookUrl))
            {
                Log.Warning("Could not find workbook URL for language {Lang}", language.LangCode);
                return translations;
            }

            Log.Information("Extracting translations from {Url}", workbookUrl);

            // Fetch the page
            var html = await FetchWithRetryAsync(workbookUrl);

            // Parse translations
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract section headers
            ExtractSectionHeaders(doc, translations, workbookUrl);

            // Extract talk names
            ExtractTalkNames(doc, translations, workbookUrl);

            // Add delay to be respectful to the server
            await Task.Delay(DelayBetweenRequestsMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract translations for language {Lang}", language.LangCode);
        }

        return translations;
    }

    private async Task<string?> FindWorkbookUrlAsync(JwLanguage language)
    {
        // First check if we have a known base URL
        if (WorkbookBaseUrls.TryGetValue(language.LangCode.ToLowerInvariant(), out var baseUrl))
        {
            // Try to find a recent workbook issue
            var currentDate = DateTime.Now;
            var monthYearPatterns = GenerateMonthYearPatterns(currentDate);

            foreach (var pattern in monthYearPatterns)
            {
                var testUrl = $"{baseUrl}{pattern}/";
                try
                {
                    var response = await _httpClient.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        return testUrl;
                    }
                }
                catch
                {
                    // Continue to next pattern
                }
            }
        }

        // Try the generic approach - construct URL based on language code
        var genericBaseUrl = $"https://www.jw.org/{language.LangCode}/library/jw-meeting-workbook/";
        try
        {
            var response = await _httpClient.GetAsync(genericBaseUrl, HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
            {
                // Parse the page to find actual workbook links
                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for links to specific workbook issues
                var workbookLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'mwb')]");
                if (workbookLinks?.Count > 0)
                {
                    var href = workbookLinks[0].GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        return href.StartsWith("http") ? href : $"https://www.jw.org{href}";
                    }
                }
            }
        }
        catch
        {
            // Continue
        }

        return null;
    }

    private static List<string> GenerateMonthYearPatterns(DateTime date)
    {
        var patterns = new List<string>();

        // Generate patterns for current and recent months
        for (int i = 0; i < 6; i++)
        {
            var targetDate = date.AddMonths(-i);
            var month = targetDate.ToString("MMMM").ToLowerInvariant();
            var year = targetDate.Year;

            // Pattern: january-february-2026-mwb, march-april-2026-mwb, etc.
            var startMonth = ((targetDate.Month - 1) / 2) * 2 + 1;
            var endMonth = startMonth + 1;

            var startMonthName = new DateTime(year, startMonth, 1).ToString("MMMM").ToLowerInvariant();
            var endMonthName = new DateTime(year, endMonth, 1).ToString("MMMM").ToLowerInvariant();

            patterns.Add($"{startMonthName}-{endMonthName}-{year}-mwb");
        }

        return patterns;
    }

    private async Task<string> FetchWithRetryAsync(string url)
    {
        var response = await _retryPipeline.ExecuteAsync(async ct =>
            await _httpClient.GetAsync(url, ct));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private void ExtractSectionHeaders(HtmlDocument doc, LanguageTranslations translations, string sourceUrl)
    {
        // Look for section headers
        // Common patterns: h2/h3 with specific classes, or divs with section markers

        // Treasures from God's Word section
        var treasuresHeader = doc.DocumentNode.SelectSingleNode(
            "//h2[contains(@class, 'treasure')] | //div[contains(@class, 'treasures')]//h2 | " +
            "//h2[@id='section1'] | //h2[contains(text(), 'TREASURES') or contains(text(), 'Treasure')]");

        if (treasuresHeader != null)
        {
            var text = CleanText(treasuresHeader.InnerText);
            if (!string.IsNullOrEmpty(text))
            {
                translations.Translations.Add(new ExtractedTranslation
                {
                    Key = "SECTION_TREASURES",
                    Value = AbbreviateSectionName(text),
                    LanguageCode = translations.LanguageCode,
                    SourceUrl = sourceUrl,
                    IsAbbreviated = true
                });
            }
        }

        // Apply Yourself to the Field Ministry section
        var ministryHeader = doc.DocumentNode.SelectSingleNode(
            "//h2[contains(@class, 'ministry')] | //div[contains(@class, 'ministry')]//h2 | " +
            "//h2[@id='section2'] | //h2[contains(text(), 'MINISTRY') or contains(text(), 'Ministry')]");

        if (ministryHeader != null)
        {
            var text = CleanText(ministryHeader.InnerText);
            if (!string.IsNullOrEmpty(text))
            {
                translations.Translations.Add(new ExtractedTranslation
                {
                    Key = "SECTION_MINISTRY",
                    Value = AbbreviateSectionName(text),
                    LanguageCode = translations.LanguageCode,
                    SourceUrl = sourceUrl,
                    IsAbbreviated = true
                });
            }
        }

        // Living as Christians section
        var livingHeader = doc.DocumentNode.SelectSingleNode(
            "//h2[contains(@class, 'living')] | //div[contains(@class, 'christian')]//h2 | " +
            "//h2[@id='section3'] | //h2[contains(text(), 'LIVING') or contains(text(), 'Living')]");

        if (livingHeader != null)
        {
            var text = CleanText(livingHeader.InnerText);
            if (!string.IsNullOrEmpty(text))
            {
                translations.Translations.Add(new ExtractedTranslation
                {
                    Key = "SECTION_LIVING",
                    Value = AbbreviateSectionName(text),
                    LanguageCode = translations.LanguageCode,
                    SourceUrl = sourceUrl,
                    IsAbbreviated = true
                });
            }
        }
    }

    private void ExtractTalkNames(HtmlDocument doc, LanguageTranslations translations, string sourceUrl)
    {
        // Look for talk/part listings
        // These are typically in list items or specific div structures

        var talkNodes = doc.DocumentNode.SelectNodes(
            "//li[contains(@class, 'so')] | //li[contains(@class, 'dx')] | " +
            "//div[@class='pGroup']//li | //article//li");

        if (talkNodes != null)
        {
            foreach (var node in talkNodes)
            {
                var text = CleanText(node.InnerText);

                // Try to identify specific talk types based on common patterns
                if (ContainsPattern(text, "opening", "comments", "introduction"))
                {
                    AddTranslationIfNotExists(translations, "TALK_OPENING_COMMENTS", text, sourceUrl);
                }
                else if (ContainsPattern(text, "spiritual", "gems", "digging"))
                {
                    AddTranslationIfNotExists(translations, "TALK_DIGGING", text, sourceUrl);
                }
                else if (ContainsPattern(text, "bible", "reading"))
                {
                    AddTranslationIfNotExists(translations, "TALK_READING", text, sourceUrl);
                }
                else if (ContainsPattern(text, "congregation", "study"))
                {
                    AddTranslationIfNotExists(translations, "TALK_CONG_STUDY", text, sourceUrl);
                }
                else if (ContainsPattern(text, "concluding", "conclusion", "closing"))
                {
                    AddTranslationIfNotExists(translations, "TALK_CONCLUDING_COMMENTS", text, sourceUrl);
                }
            }
        }
    }

    private static bool ContainsPattern(string text, params string[] patterns)
    {
        var lowerText = text.ToLowerInvariant();
        return patterns.Any(p => lowerText.Contains(p));
    }

    private static void AddTranslationIfNotExists(LanguageTranslations translations, string key, string value, string sourceUrl)
    {
        if (!translations.Translations.Any(t => t.Key == key))
        {
            translations.Translations.Add(new ExtractedTranslation
            {
                Key = key,
                Value = ExtractTalkName(value),
                LanguageCode = translations.LanguageCode,
                SourceUrl = sourceUrl
            });
        }
    }

    private static string ExtractTalkName(string fullText)
    {
        // Remove time indicators like "(5 min.)" or duration info
        var cleaned = System.Text.RegularExpressions.Regex.Replace(fullText, @"\(\d+\s*min\.?\)", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\d+:\d+", "");
        return cleaned.Trim();
    }

    private static string CleanText(string text)
    {
        // Remove HTML entities and extra whitespace
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string AbbreviateSectionName(string fullName)
    {
        // Extract the main section name, typically in capitals
        // e.g., "TREASURES FROM GOD'S WORD" -> "Treasures"
        //       "APPLY YOURSELF TO THE FIELD MINISTRY" -> "Ministry"
        //       "LIVING AS CHRISTIANS" -> "Living"

        var upper = fullName.ToUpperInvariant();

        if (upper.Contains("TREASURE"))
            return "Treasures";
        if (upper.Contains("MINISTRY"))
            return "Ministry";
        if (upper.Contains("LIVING") || upper.Contains("CHRISTIAN"))
            return "Living";

        // Return first word if we can't identify
        var words = fullName.Split(' ');
        return words.Length > 0 ? words[0] : fullName;
    }
}
