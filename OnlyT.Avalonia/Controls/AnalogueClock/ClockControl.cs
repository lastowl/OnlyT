using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using AvPath = Avalonia.Controls.Shapes.Path;

namespace OnlyT.Avalonia.Controls.AnalogueClock;

/// <summary>
/// An analogue clock control for Avalonia
/// </summary>
public class ClockControl : TemplatedControl
{
    private const double ClockRadius = 250;
    private const double SectorRadius = 230;
    private static readonly Point ClockOrigin = new(ClockRadius, ClockRadius);
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);

    private readonly DispatcherTimer _timer;

    private Ellipse? _outerDial;
    private Ellipse? _middleDial;
    private Ellipse? _centrePointDial;
    private Line? _minuteHand;
    private Line? _hourHand;
    private Line? _secondHand;
    private AvPath? _sectorPath1;
    private AvPath? _sectorPath2;
    private AvPath? _sectorPath3;
    private bool _elementsAvailable;
    private bool _digitalFormatLeadingZero;
    private bool _digitalFormat24Hours;
    private bool _digitalFormatAMPM;

    public static readonly StyledProperty<bool> IsFlatProperty =
        AvaloniaProperty.Register<ClockControl, bool>(nameof(IsFlat));

    public static readonly StyledProperty<bool> DigitalTimeFormatShowLeadingZeroProperty =
        AvaloniaProperty.Register<ClockControl, bool>(nameof(DigitalTimeFormatShowLeadingZero));

    public static readonly StyledProperty<bool> DigitalTimeFormat24HoursProperty =
        AvaloniaProperty.Register<ClockControl, bool>(nameof(DigitalTimeFormat24Hours));

    public static readonly StyledProperty<bool> DigitalTimeFormatAMPMProperty =
        AvaloniaProperty.Register<ClockControl, bool>(nameof(DigitalTimeFormatAMPM));

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ClockControl, bool>(nameof(IsRunning));

    public static readonly StyledProperty<string> CurrentTimeHrMinProperty =
        AvaloniaProperty.Register<ClockControl, string>(nameof(CurrentTimeHrMin), "12:00");

    public static readonly StyledProperty<string> CurrentTimeSecProperty =
        AvaloniaProperty.Register<ClockControl, string>(nameof(CurrentTimeSec), "00");

    public static readonly StyledProperty<DurationSector?> DurationSectorProperty =
        AvaloniaProperty.Register<ClockControl, DurationSector?>(nameof(DurationSector));

    static ClockControl()
    {
        IsRunningProperty.Changed.AddClassHandler<ClockControl>((c, e) => c.OnIsRunningChanged(e));
        DurationSectorProperty.Changed.AddClassHandler<ClockControl>((c, e) => c.OnDurationSectorChanged(e));
        IsFlatProperty.Changed.AddClassHandler<ClockControl>((c, e) => c.SetFlatOrNonFlatStyle());
        DigitalTimeFormatShowLeadingZeroProperty.Changed.AddClassHandler<ClockControl>((c, e) => c._digitalFormatLeadingZero = (bool)e.NewValue!);
        DigitalTimeFormat24HoursProperty.Changed.AddClassHandler<ClockControl>((c, e) => c._digitalFormat24Hours = (bool)e.NewValue!);
        DigitalTimeFormatAMPMProperty.Changed.AddClassHandler<ClockControl>((c, e) => c._digitalFormatAMPM = (bool)e.NewValue!);
    }

    public ClockControl()
    {
        _timer = new DispatcherTimer { Interval = TimerInterval };
        _timer.Tick += TimerCallback;
    }

    public event EventHandler<DateTimeQueryEventArgs>? QueryDateTimeEvent;

    public bool IsFlat
    {
        get => GetValue(IsFlatProperty);
        set => SetValue(IsFlatProperty, value);
    }

    public bool DigitalTimeFormatShowLeadingZero
    {
        get => GetValue(DigitalTimeFormatShowLeadingZeroProperty);
        set => SetValue(DigitalTimeFormatShowLeadingZeroProperty, value);
    }

    public bool DigitalTimeFormat24Hours
    {
        get => GetValue(DigitalTimeFormat24HoursProperty);
        set => SetValue(DigitalTimeFormat24HoursProperty, value);
    }

    public bool DigitalTimeFormatAMPM
    {
        get => GetValue(DigitalTimeFormatAMPMProperty);
        set => SetValue(DigitalTimeFormatAMPMProperty, value);
    }

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public string CurrentTimeHrMin
    {
        get => GetValue(CurrentTimeHrMinProperty);
        set => SetValue(CurrentTimeHrMinProperty, value);
    }

    public string CurrentTimeSec
    {
        get => GetValue(CurrentTimeSecProperty);
        set => SetValue(CurrentTimeSecProperty, value);
    }

    public DurationSector? DurationSector
    {
        get => GetValue(DurationSectorProperty);
        set => SetValue(DurationSectorProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _outerDial = e.NameScope.Find<Ellipse>("OuterDial");
        _middleDial = e.NameScope.Find<Ellipse>("MiddleDial");
        _centrePointDial = e.NameScope.Find<Ellipse>("CentrePointDial");
        _minuteHand = e.NameScope.Find<Line>("MinuteHand");
        _hourHand = e.NameScope.Find<Line>("HourHand");
        _secondHand = e.NameScope.Find<Line>("SecondHand");
        _sectorPath1 = e.NameScope.Find<AvPath>("SectorPath1");
        _sectorPath2 = e.NameScope.Find<AvPath>("SectorPath2");
        _sectorPath3 = e.NameScope.Find<AvPath>("SectorPath3");

        _elementsAvailable = _outerDial != null;

        SetFlatOrNonFlatStyle();

        // Generate hour markers and numbers
        if (e.NameScope.Find<Canvas>("ClockCanvas") is Canvas cc)
        {
            GenerateHourMarkers(cc);
            GenerateHourNumbers(cc);
        }

        // Start with current time
        UpdateClockHands(DateTime.Now);
    }

    private void OnIsRunningChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var running = (bool)e.NewValue!;
        _timer.IsEnabled = running;
        if (running)
        {
            UpdateClockHands(GetCurrentDateTime());
        }
    }

    private void OnDurationSectorChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var sector = e.NewValue as DurationSector;
        if (sector == null)
        {
            ClearSectors();
        }
        else
        {
            DrawSector(sector);
        }
    }

    private void TimerCallback(object? sender, EventArgs e)
    {
        var now = GetCurrentDateTime();
        UpdateClockHands(now);
        UpdateDigitalTime(now);
    }

    private DateTime GetCurrentDateTime()
    {
        var args = new DateTimeQueryEventArgs();
        QueryDateTimeEvent?.Invoke(this, args);
        return args.DateTime ?? DateTime.Now;
    }

    private void UpdateClockHands(DateTime dt)
    {
        if (!_elementsAvailable) return;

        var hourAngle = CalculateAngleHours(dt);
        var minuteAngle = CalculateAngleMinutes(dt);
        var secondAngle = CalculateAngleSeconds(dt);

        SetHandAngle(_hourHand, hourAngle);
        SetHandAngle(_minuteHand, minuteAngle);
        SetHandAngle(_secondHand, secondAngle);
    }

    private void UpdateDigitalTime(DateTime dt)
    {
        string hourStr;
        if (_digitalFormat24Hours)
        {
            hourStr = _digitalFormatLeadingZero ? dt.ToString("HH") : dt.Hour.ToString();
        }
        else
        {
            var hour12 = dt.Hour % 12;
            if (hour12 == 0) hour12 = 12;
            hourStr = _digitalFormatLeadingZero ? hour12.ToString("00") : hour12.ToString();
        }

        var minStr = dt.Minute.ToString("00");
        CurrentTimeHrMin = $"{hourStr}:{minStr}";
        CurrentTimeSec = dt.Second.ToString("00");

        if (_digitalFormatAMPM && !_digitalFormat24Hours)
        {
            CurrentTimeHrMin += dt.Hour >= 12 ? " PM" : " AM";
        }
    }

    private static void SetHandAngle(Line? hand, double angle)
    {
        if (hand?.RenderTransform is RotateTransform rt)
        {
            rt.Angle = angle;
        }
    }

    private static double CalculateAngleSeconds(DateTime dt) => dt.Second * 6;

    private static double CalculateAngleMinutes(DateTime dt) => (dt.Minute * 6) + (dt.Second * 0.1);

    private static double CalculateAngleHours(DateTime dt) => (dt.Hour % 12 * 30) + (dt.Minute * 0.5);

    private void SetFlatOrNonFlatStyle()
    {
        if (!_elementsAvailable) return;

        if (IsFlat)
        {
            // Flat style - solid colors
            _outerDial?.SetValue(Shape.FillProperty, new SolidColorBrush(Color.Parse("#2C2C2C")));
            _middleDial?.SetValue(Shape.FillProperty, new SolidColorBrush(Color.Parse("#1E1E1E")));
        }
        else
        {
            // Gradient style
            var gradient = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.Parse("#3C3C3C"), 0),
                    new GradientStop(Color.Parse("#1C1C1C"), 1)
                }
            };
            _outerDial?.SetValue(Shape.FillProperty, gradient);
            _middleDial?.SetValue(Shape.FillProperty, new SolidColorBrush(Color.Parse("#1E1E1E")));
        }
    }

    private void GenerateHourMarkers(Canvas canvas)
    {
        // Generate 60 minute markers
        for (int i = 0; i < 60; i++)
        {
            var angle = i * 6; // 360/60 = 6 degrees per minute
            var isHourMarker = i % 5 == 0;

            var innerRadius = isHourMarker ? 200 : 215;
            var outerRadius = 225;

            var startPoint = PointOnCircle(innerRadius, angle, ClockOrigin);
            var endPoint = PointOnCircle(outerRadius, angle, ClockOrigin);

            var marker = new Line
            {
                StartPoint = startPoint,
                EndPoint = endPoint,
                Stroke = Brushes.Black,
                StrokeThickness = isHourMarker ? 3 : 1
            };

            canvas.Children.Add(marker);
        }
    }

    private void GenerateHourNumbers(Canvas canvas)
    {
        var numbers = new[] { "12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" };
        for (int i = 0; i < 12; i++)
        {
            var angle = i * 30;
            var position = PointOnCircle(175, angle, ClockOrigin);

            var text = new TextBlock
            {
                Text = numbers[i],
                Foreground = Brushes.Black,
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center
            };

            // Adjust position based on text width (approximate)
            var xOffset = numbers[i].Length == 2 ? 16 : 8;
            Canvas.SetLeft(text, position.X - xOffset);
            Canvas.SetTop(text, position.Y - 16);
            canvas.Children.Add(text);
        }
    }

    private static Point PointOnCircle(double radius, double angleInDegrees, Point origin)
    {
        var x = (radius * Math.Cos((angleInDegrees - 90) * (Math.PI / 180))) + origin.X;
        var y = (radius * Math.Sin((angleInDegrees - 90) * (Math.PI / 180))) + origin.Y;
        return new Point(x, y);
    }

    private void ClearSectors()
    {
        if (_sectorPath1 != null) _sectorPath1.Data = null;
        if (_sectorPath2 != null) _sectorPath2.Data = null;
        if (_sectorPath3 != null) _sectorPath3.Data = null;
    }

    private void DrawSector(DurationSector sector)
    {
        // Green sector (remaining time)
        if (!sector.IsOvertime && _sectorPath1 != null)
        {
            DrawSectorPath(_sectorPath1, sector.StartAngle, sector.EndAngle,
                IsLargeArc(sector.StartAngle, sector.EndAngle));
        }

        // Light green sector (elapsed time)
        if (!sector.IsOvertime && sector.ShowElapsedSector && _sectorPath2 != null)
        {
            DrawSectorPath(_sectorPath2, sector.StartAngle, sector.CurrentAngle,
                IsLargeArc(sector.StartAngle, sector.CurrentAngle));
        }

        // Red sector (overtime)
        if (sector.IsOvertime && _sectorPath3 != null)
        {
            DrawSectorPath(_sectorPath3, sector.EndAngle, sector.CurrentAngle,
                IsLargeArc(sector.EndAngle, sector.CurrentAngle));
        }
    }

    private static void DrawSectorPath(AvPath sectorPath, double startAngle, double endAngle, bool isLargeArc)
    {
        var startPoint = PointOnCircle(SectorRadius, startAngle, ClockOrigin);
        var endPoint = PointOnCircle(SectorRadius, endAngle, ClockOrigin);
        var largeArc = isLargeArc ? "1" : "0";

        var pathData = $"M{ClockRadius.ToString(CultureInfo.InvariantCulture)},{ClockRadius.ToString(CultureInfo.InvariantCulture)} " +
                      $"L{startPoint.X.ToString(CultureInfo.InvariantCulture)},{startPoint.Y.ToString(CultureInfo.InvariantCulture)} " +
                      $"A{SectorRadius.ToString(CultureInfo.InvariantCulture)},{SectorRadius.ToString(CultureInfo.InvariantCulture)} 0 {largeArc} 1 " +
                      $"{endPoint.X.ToString(CultureInfo.InvariantCulture)},{endPoint.Y.ToString(CultureInfo.InvariantCulture)} z";

        sectorPath.Data = StreamGeometry.Parse(pathData);
    }

    private static bool IsLargeArc(double startAngle, double endAngle)
    {
        if (endAngle < startAngle)
        {
            return (360 - startAngle + endAngle) > 180;
        }
        return endAngle - startAngle >= 180.0;
    }
}

/// <summary>
/// Event args for querying the current date/time
/// </summary>
public class DateTimeQueryEventArgs : EventArgs
{
    public DateTime? DateTime { get; set; }
}
