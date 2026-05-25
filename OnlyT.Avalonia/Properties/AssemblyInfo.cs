using System.Runtime.CompilerServices;

// Expose internal types (e.g. TalkTimerService, TalkScheduleFileBased and the
// HttpServer data-shaping helpers) to the unit test project so they can be
// exercised directly with mocked dependencies.
[assembly: InternalsVisibleTo("OnlyT.Avalonia.Tests")]
