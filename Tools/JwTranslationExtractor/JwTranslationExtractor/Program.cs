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
            var scraperService = new WorkbookScraperService();

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

    static void ShowHelp()
    {
        Console.WriteLine(@"
OnlyT Translation Extractor
============================

Extracts translations from jw.org meeting workbook pages and generates
.resx resource files for OnlyT multi-language support.

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

Examples:
  # List all available languages
  JwTranslationExtractor --list

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
    public string? BaseResxPath { get; set; }
    public string? OutputDirectory { get; set; }
    public string? TrackingDirectory { get; set; }
    public string? GoogleApiKey { get; set; }
    public List<string> TargetLanguages { get; set; } = new();
    public int MaxLanguages { get; set; } = 10;
}
