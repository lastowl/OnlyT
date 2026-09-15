using System.Xml.Linq;
using JwTranslationExtractor.Models;
using JwTranslationExtractor.Services;
using Serilog;

namespace JwTranslationExtractor;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("OnlyT Translation Extractor");
            Log.Information("===========================");

            // Parse command line arguments
            var options = ParseArguments(args);

            if (options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            // Initialize services
            var languageService = new LanguageDiscoveryService();
            var scraperService = new WorkbookScraperService { Issue = options.Issue };

            if (options.InPlace)
            {
                return await UpdateInPlaceAsync(options, scraperService);
            }

            // Initialize tracking and translation services
            var trackingDirectory = options.TrackingDirectory ??
                Path.Combine(options.OutputDirectory ?? ".", ".translation-tracking");
            var trackingService = new TranslationTrackingService(trackingDirectory);

            // Initialize translation services
            GoogleTranslateService? googleTranslateService = null;
            FreeTranslationService? freeTranslationService = null;

            if (options.UseThirdPartyTranslation)
            {
                if (!string.IsNullOrEmpty(options.GoogleApiKey))
                {
                    googleTranslateService = new GoogleTranslateService(options.GoogleApiKey);
                    Log.Information("Using Google Translate API");
                }
                else
                {
                    freeTranslationService = new FreeTranslationService();
                    Log.Information("Using free translation services (LibreTranslate/MyMemory)");
                }
            }

            var resxGenerator = new ResxGeneratorService(trackingService, googleTranslateService, freeTranslationService);

            // Step 1: Discover languages
            List<JwLanguage> languages;
            if (options.UseOfflineMode)
            {
                Log.Information("Step 1: Using offline language list...");
                languages = languageService.GetOfflineLanguages();
            }
            else
            {
                Log.Information("Step 1: Discovering languages from jw.org...");
                languages = await languageService.GetSupportedLanguagesAsync();
            }
            Log.Information("Found {Count} supported languages", languages.Count);

            if (options.ListOnly)
            {
                ListLanguages(languages);
                return 0;
            }

            var targetLanguages = options.TargetLanguages.Count > 0
                ? languages.Where(l => options.TargetLanguages.Contains(l.LangCode, StringComparer.OrdinalIgnoreCase) ||
                                       options.TargetLanguages.Contains(l.CultureCode ?? "", StringComparer.OrdinalIgnoreCase)).ToList()
                : languages.Take(options.MaxLanguages).ToList();

            // Step 2: Extract translations from jw.org (unless skipped)
            var translations = new Dictionary<string, LanguageTranslations>();

            if (!options.SkipExtraction)
            {
                Log.Information("Step 2: Extracting translations from jw.org...");

                var count = 0;
                foreach (var language in targetLanguages)
                {
                    count++;
                    Log.Information("[{Current}/{Total}] Extracting: {Lang} ({Name})",
                        count, targetLanguages.Count, language.LangCode, language.VernacularName);

                    try
                    {
                        var langTranslations = await scraperService.ExtractTranslationsAsync(language);
                        translations[language.LangCode] = langTranslations;

                        Log.Information("  Found {Count} translations", langTranslations.Translations.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "  Failed to extract translations");
                    }
                }
            }
            else
            {
                Log.Information("Step 2: Skipping jw.org extraction (--skip-extraction)");
            }

            // Step 3: Generate resx files with translation hierarchy
            if (!string.IsNullOrEmpty(options.BaseResxPath) && !string.IsNullOrEmpty(options.OutputDirectory))
            {
                Log.Information("Step 3: Generating resx files with translation hierarchy...");
                Log.Information("  Priority: jw.org > Manual > Third-party > English fallback");

                if (options.UseThirdPartyTranslation)
                {
                    Log.Information("  Third-party translation enabled for missing keys");
                }

                await resxGenerator.GenerateAllLanguageFilesWithHierarchyAsync(
                    options.BaseResxPath,
                    options.OutputDirectory,
                    targetLanguages,
                    translations,
                    options.UseThirdPartyTranslation);
            }
            else
            {
                Log.Warning("Skipping resx generation - specify --base-resx and --output to generate files");
            }

            // Cleanup
            googleTranslateService?.Dispose();
            freeTranslationService?.Dispose();

            Log.Information("Done!");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Updates existing resx files in place, leaving everything else untouched. For each string the
    /// official jw.org wording wins, then the upstream translation (with --upstream), and otherwise
    /// the file keeps its existing translation.
    /// </summary>
    static async Task<int> UpdateInPlaceAsync(CommandLineOptions options, WorkbookScraperService scraperService)
    {
        if (string.IsNullOrEmpty(options.BaseResxPath) || string.IsNullOrEmpty(options.OutputDirectory))
        {
            Log.Error("--in-place requires --base-resx and --output");
            return 1;
        }

        var baseValues = XDocument.Load(options.BaseResxPath).Root?.Elements("data")
            .Where(d => d.Attribute("name") != null)
            .GroupBy(d => d.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.Last().Element("value")?.Value ?? string.Empty)
            ?? new Dictionary<string, string>();

        var upstream = string.IsNullOrEmpty(options.UpstreamResxPath)
            ? null
            : new UpstreamTranslations(options.UpstreamResxPath);

        var prefix = Path.GetFileNameWithoutExtension(options.BaseResxPath) + ".";
        var files = Directory.GetFiles(options.OutputDirectory, prefix + "*.resx")
            .Select(filePath => (FilePath: filePath, Culture: Path.GetFileNameWithoutExtension(filePath)[prefix.Length..]))
            .Where(f => f.Culture.Length > 0 &&
                        (options.TargetLanguages.Count == 0 ||
                         options.TargetLanguages.Contains(f.Culture, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(f => f.Culture, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Log.Information("Updating {Count} files{DryRun}", files.Count, options.DryRun ? " (dry run)" : "");

        var catalog = await JwLanguageCatalog.LoadAsync();
        var updater = new ResxInPlaceUpdater();
        var noWebsite = new List<string>();
        var skipped = new List<string>();
        var changedFiles = 0;
        var changedValues = 0;

        foreach (var (filePath, culture) in files)
        {
            var fileScript = updater.DetectTranslationScript(filePath, baseValues);

            // 1. Official wording from jw.org
            var website = new Dictionary<string, string>();
            var language = catalog.Resolve(culture, fileScript);
            if (language == null)
            {
                noWebsite.Add($"{culture} (no jw.org language with web content)");
            }
            else
            {
                Log.Information("{Culture}: {Name} ({Code}, {Locale})", culture, language.Name, language.LangCode, language.Locale);
                website = (await scraperService.ExtractTranslationsAsync(language)).Translations
                    .ToDictionary(t => t.Key, t => t.Value);

                if (website.Count == 0)
                {
                    noWebsite.Add($"{culture} (no workbook text)");
                }
            }

            // 2. Upstream translations, except where the file already has official jw.org wording
            var values = new Dictionary<string, string>();
            if (upstream != null)
            {
                var official = website.Values.Select(NormalizeForComparison).ToHashSet();
                var current = updater.ReadCurrentValues(filePath);
                var upstreamValues = upstream.GetTranslations(culture, baseValues)
                    .Where(kvp => !(current.TryGetValue(kvp.Key, out var existing) &&
                                    official.Contains(NormalizeForComparison(existing))))
                    .Where(kvp => fileScript is null or "Latin" || !ContainsEnglishWords(kvp.Value, baseValues[kvp.Key]))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                var upstreamScript = ScriptDetector.Dominant(string.Concat(upstreamValues.Values));
                if (fileScript != null && upstreamScript != null && upstreamScript != fileScript)
                {
                    Log.Warning("Not using upstream for {Culture}: upstream is {UpstreamScript} script but the file is {FileScript}",
                        culture, upstreamScript, fileScript);
                }
                else
                {
                    values = upstreamValues;
                }
            }

            // 3. jw.org wording overrides both
            foreach (var (key, value) in website)
            {
                values[key] = value;
            }

            if (values.Count == 0)
            {
                continue;
            }

            var changes = updater.Update(filePath, values, baseValues, options.DryRun);
            if (changes < 0)
            {
                skipped.Add($"{culture} (script mismatch)");
            }
            else if (changes > 0)
            {
                changedFiles++;
                changedValues += changes;
            }
        }

        Log.Information("{Action} {Values} values in {Files} files",
            options.DryRun ? "Would update" : "Updated", changedValues, changedFiles);

        if (noWebsite.Count > 0)
        {
            Log.Information("No jw.org wording for {Count}: {Languages}", noWebsite.Count, string.Join(", ", noWebsite));
        }

        if (skipped.Count > 0)
        {
            Log.Information("Skipped {Count}: {Skipped}", skipped.Count, string.Join(", ", skipped));
        }

        return 0;
    }

    // Compares wording regardless of case and apostrophe style
    static string NormalizeForComparison(string text) => text.Replace('’', '\'').Trim().ToUpperInvariant();

    // Whether text in a non-Latin language still contains words from the English original, e.g.
    // "12-часовой (leading zero)". Key names and acronyms (Ctrl, NDI) are expected to stay in English.
    static bool ContainsEnglishWords(string text, string english)
    {
        var keepInEnglish = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ctrl", "Shift", "Alt" };
        var englishWords = System.Text.RegularExpressions.Regex.Matches(english, "[A-Za-z]{3,}")
            .Select(m => m.Value)
            .Where(w => !keepInEnglish.Contains(w) && w != w.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return System.Text.RegularExpressions.Regex.Matches(text, "[A-Za-z]{3,}").Any(m => englishWords.Contains(m.Value));
    }

    static void ShowHelp()
    {
        Console.WriteLine(@"
OnlyT Translation Extractor
============================

Extracts meeting names from jw.org (the meeting workbook and Watchtower study articles),
and generates or updates .resx resource files for OnlyT multi-language support.

Translation Priority (highest to lowest):
  1. jw.org translations - Official translations, always overwrites
  2. Manual translations - Preserved unless jw.org translation exists
  3. Third-party translations - Only used initially, never updated after
  4. English fallback - Used when no translation available

Usage:
  JwTranslationExtractor [options]

Options:
  --help, -h              Show this help message
  --list                  List available languages and exit
  --offline               Use offline language list (no network required)
  --skip-extraction       Skip scraping jw.org, use existing/third-party translations
  --translate             Enable third-party translation for missing keys
  --google-api-key <key>  Google Cloud Translation API key (preferred)
  --base-resx <path>      Path to base Resources.resx file (required for generation)
  --output <path>         Output directory for generated .resx files
  --tracking <path>       Directory for translation tracking data (default: .translation-tracking)
  --languages <codes>     Comma-separated language codes (e.g., en,es,fr)
  --max <number>          Maximum number of languages to process (default: 10)
  --in-place              Update jw.org meeting names in the existing Resources.<culture>.resx
                          files in --output, leaving all other strings and formatting untouched
                          (--languages then takes culture names, e.g. de-DE,pt-PT)
  --upstream <path>       With --in-place, also take translations from another base resx and its
                          culture files (e.g. the WPF OnlyT/Properties/Resources.resx). Priority:
                          jw.org wording, then upstream, then the file's existing translation
  --issue <yyyymm>        Workbook issue to read (default: the current issue)
  --dry-run               With --in-place, report changes without writing files

Examples:
  # List all available languages
  JwTranslationExtractor --list

  # Refresh official meeting names in every existing language file
  JwTranslationExtractor --in-place --base-resx ./Resources.resx --output ./Properties

  # Official wording first, then upstream translations, for every language file
  JwTranslationExtractor --in-place --upstream ../OnlyT/Properties/Resources.resx --base-resx ./Resources.resx --output ./Properties

  # Preview the changes for German and Polish only
  JwTranslationExtractor --in-place --dry-run --languages de-DE,pl-PL --base-resx ./Resources.resx --output ./Properties

  # Extract jw.org translations for Spanish and French
  JwTranslationExtractor --languages es,fr --base-resx ./Resources.resx --output ./Properties

  # Use Google Translate for missing keys (recommended)
  JwTranslationExtractor --translate --google-api-key YOUR_API_KEY --base-resx ./Resources.resx --output ./Properties

  # Skip jw.org extraction, only apply translations to missing keys
  JwTranslationExtractor --skip-extraction --translate --google-api-key YOUR_API_KEY --base-resx ./Resources.resx --output ./Properties

  # Process all 150+ languages with Google Translate
  JwTranslationExtractor --offline --max 200 --translate --google-api-key YOUR_API_KEY --base-resx ./Resources.resx --output ./Properties
");
    }

    static void ListLanguages(List<JwLanguage> languages)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Code",-8} {"Culture",-12} {"Name",-30} {"Vernacular Name"}");
        Console.WriteLine(new string('-', 80));

        foreach (var lang in languages.OrderBy(l => l.LangCode))
        {
            Console.WriteLine($"{lang.LangCode,-8} {lang.CultureCode ?? "?",-12} {lang.Name,-30} {lang.VernacularName}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {languages.Count} languages");
    }

    static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;

                case "--list":
                    options.ListOnly = true;
                    break;

                case "--offline":
                    options.UseOfflineMode = true;
                    break;

                case "--skip-extraction":
                    options.SkipExtraction = true;
                    break;

                case "--translate":
                    options.UseThirdPartyTranslation = true;
                    break;

                case "--in-place":
                    options.InPlace = true;
                    break;

                case "--dry-run":
                    options.DryRun = true;
                    break;

                case "--issue":
                    if (i + 1 < args.Length)
                        options.Issue = args[++i];
                    break;

                case "--upstream":
                    if (i + 1 < args.Length)
                        options.UpstreamResxPath = args[++i];
                    break;

                case "--base-resx":
                    if (i + 1 < args.Length)
                        options.BaseResxPath = args[++i];
                    break;

                case "--output":
                    if (i + 1 < args.Length)
                        options.OutputDirectory = args[++i];
                    break;

                case "--tracking":
                    if (i + 1 < args.Length)
                        options.TrackingDirectory = args[++i];
                    break;

                case "--languages":
                    if (i + 1 < args.Length)
                    {
                        var codes = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        options.TargetLanguages.AddRange(codes.Select(c => c.Trim()));
                    }
                    break;

                case "--max":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var max))
                        options.MaxLanguages = max;
                    break;

                case "--google-api-key":
                    if (i + 1 < args.Length)
                        options.GoogleApiKey = args[++i];
                    break;
            }
        }

        return options;
    }
}

class CommandLineOptions
{
    public bool ShowHelp { get; set; }
    public bool ListOnly { get; set; }
    public bool UseOfflineMode { get; set; }
    public bool SkipExtraction { get; set; }
    public bool UseThirdPartyTranslation { get; set; }
    public bool InPlace { get; set; }
    public bool DryRun { get; set; }
    public string? Issue { get; set; }
    public string? UpstreamResxPath { get; set; }
    public string? BaseResxPath { get; set; }
    public string? OutputDirectory { get; set; }
    public string? TrackingDirectory { get; set; }
    public string? GoogleApiKey { get; set; }
    public List<string> TargetLanguages { get; set; } = new();
    public int MaxLanguages { get; set; } = 10;
}
