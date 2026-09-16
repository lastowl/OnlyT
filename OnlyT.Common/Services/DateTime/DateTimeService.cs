namespace OnlyT.Common.Services.DateTime
{
    using System;
    using System.Diagnostics;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class DateTimeService : IDateTimeService
    {
        private readonly DateTime? _forcedBaseTime;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public DateTimeService(DateTime? forceDateTimeAtStart)
        {
            _forcedBaseTime = forceDateTimeAtStart;
        }

        // In normal operation return the real wall clock rather than
        // baseTime + Stopwatch.Elapsed: Stopwatch doesn't advance during
        // system sleep (macOS/Linux, and unreliably on Windows) and never
        // sees NTP/DST corrections, so an offset clock drifts from the
        // wall clock that meeting start times are expressed in.
        public DateTime Now() =>
            _forcedBaseTime.HasValue ? _forcedBaseTime.Value + _stopwatch.Elapsed : DateTime.Now;

        public DateTime UtcNow() =>
            _forcedBaseTime.HasValue ? Now().ToUniversalTime() : DateTime.UtcNow;

        public DateTime Today() => Now().Date;
    }
}
