using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using OnlyT.Avalonia.Services.Options;
using Path = Avalonia.Controls.Shapes.Path;

namespace OnlyT.Avalonia.Controls;

public partial class CountdownControl : UserControl
{
    public static readonly StyledProperty<int> CountdownTotalSecondsProperty =
        AvaloniaProperty.Register<CountdownControl, int>(nameof(CountdownTotalSeconds), 300);

    public static readonly StyledProperty<ElementsToShow> ElementsToShowProperty =
        AvaloniaProperty.Register<CountdownControl, ElementsToShow>(nameof(ElementsToShow), ElementsToShow.DialAndDigital);

    private const int DefaultCountdownTotalSeconds = 300;

    private readonly DispatcherTimer _timer;

    private Path? _donut;
    private Path? _pie;
    private Ellipse? _secondsBall;
    private TextBlock? _time;

    private double _canvasWidth;
    private double _canvasHeight;
    private int _innerRadius;
    private int _outerRadius;
    private Point _centrePointOfDial;
    private DateTime _start;
    private int _countdownTotalSeconds = DefaultCountdownTotalSeconds;
    private bool _twoDigitMins;
    private bool _showHours;
    private ElementsToShow _elementsToShow = ElementsToShow.DialAndDigital;
    private Canvas? _canvas;

    public CountdownControl()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        _timer.Tick += TimerFire;
    }

    public event EventHandler? TimeUpEvent;

    public int CountdownTotalSeconds
    {
        get => GetValue(CountdownTotalSecondsProperty);
        set => SetValue(CountdownTotalSecondsProperty, value);
    }

    public ElementsToShow ElementsToShow
    {
        get => GetValue(ElementsToShowProperty);
        set => SetValue(ElementsToShowProperty, value);
    }

    public void Start(int totalSeconds)
    {
        _countdownTotalSeconds = totalSeconds;
        CountdownTotalSeconds = totalSeconds;
        _showHours = totalSeconds >= 3600;
        _twoDigitMins = totalSeconds >= 600;
        _start = DateTime.UtcNow;
        if (_canvas != null) InitLayout();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CountdownTotalSecondsProperty)
        {
            _countdownTotalSeconds = (int)(change.NewValue ?? DefaultCountdownTotalSeconds);
        }
        else if (change.Property == ElementsToShowProperty)
        {
            _elementsToShow = (ElementsToShow)(change.NewValue ?? ElementsToShow.DialAndDigital);
            if (_canvas != null)
            {
                InitLayout();
            }
        }
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _canvas = this.FindControl<Canvas>("CountdownCanvas");
        if (_canvas != null)
        {
            _canvas.SizeChanged += OnCanvasSizeChanged;
            AddGeometryToCanvas(_canvas);
            InitLayout();
        }
    }

    protected override void OnUnloaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _timer.Stop();
        _timer.Tick -= TimerFire;
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        InitLayout();
    }

    private void InitLayout()
    {
        if (_canvas == null || _donut == null || _pie == null || _secondsBall == null || _time == null)
            return;

        _canvasWidth = _canvas.Bounds.Width;
        _canvasHeight = _canvas.Bounds.Height;

        if (_canvasWidth <= 0 || _canvasHeight <= 0)
            return;

        _showHours = _countdownTotalSeconds >= 3600;
        _twoDigitMins = _countdownTotalSeconds >= 600;

        var outerRadiusFactor = 0.24;
        var innerRadiusFactor = 0.15;

        if (_elementsToShow == ElementsToShow.Dial)
        {
            outerRadiusFactor *= 1.75;
            innerRadiusFactor *= 1.75;
        }

        _innerRadius = (int)(_canvasHeight * innerRadiusFactor);
        _outerRadius = (int)(_canvasHeight * outerRadiusFactor);

        switch (_elementsToShow)
        {
            case ElementsToShow.DialAndDigital:
                InitLayoutDigitalAndDial();
                break;
            case ElementsToShow.Dial:
                InitLayoutJustDial();
                break;
            case ElementsToShow.Digital:
                InitLayoutJustDigital();
                break;
        }
    }

    private void InitLayoutDigitalAndDial()
    {
        var outerDiameter = _outerRadius * 2;
        var textWidthFactor = _showHours ? 4.5 : (_twoDigitMins ? 3.25 : 2.75);
        var totalWidthNeeded = textWidthFactor * outerDiameter;

        // Clamp to canvas width
        if (totalWidthNeeded > _canvasWidth * 0.95)
            totalWidthNeeded = _canvasWidth * 0.95;

        _centrePointOfDial = CalcCentrePointOfDial(totalWidthNeeded);

        _donut!.Data = PieSlice.Get(0.1, _centrePointOfDial, _innerRadius, _outerRadius);

        _secondsBall!.Width = (double)_innerRadius / 6;
        _secondsBall.Height = (double)_innerRadius / 6;

        SetElementsVisibility();

        _time!.Text = GetTimeText();

        // Position time to the right of the dial
        var fontSize = CalculateFontSizeWithDial();
        _time.FontSize = fontSize;

        var timeX = _centrePointOfDial.X + _outerRadius + 20;
        var timeY = _centrePointOfDial.Y - fontSize * 0.6;

        Canvas.SetLeft(_time, timeX);
        Canvas.SetTop(_time, timeY);

        RenderPieSliceAndBall(0.1, 0);
    }

    private void InitLayoutJustDial()
    {
        var outerDiameter = _outerRadius * 2;
        _centrePointOfDial = CalcCentrePointOfDial(outerDiameter);

        _donut!.Data = PieSlice.Get(0.1, _centrePointOfDial, _innerRadius, _outerRadius);

        _secondsBall!.Width = (double)_innerRadius / 6;
        _secondsBall.Height = (double)_innerRadius / 6;

        SetElementsVisibility();
        RenderPieSliceAndBall(0.1, 0);
    }

    private void InitLayoutJustDigital()
    {
        SetElementsVisibility();

        _time!.Text = GetTimeText();

        var widthFactor = _showHours ? 0.2 : 0.3;
        var fontSize = Math.Min(_canvasWidth * widthFactor, _canvasHeight * 0.6);
        _time.FontSize = fontSize;

        // Center the text - estimate char count for positioning
        var charCount = _time.Text.Length;
        var estimatedWidth = charCount * fontSize * 0.6;
        var leftOffset = (_canvasWidth - estimatedWidth) / 2;

        Canvas.SetLeft(_time, Math.Max(0, leftOffset));
        Canvas.SetTop(_time, (_canvasHeight - fontSize) / 2);
    }

    private double CalculateFontSizeWithDial()
    {
        var outerDiameter = _outerRadius * 2;
        double szFactor;
        if (_showHours)
            szFactor = 0.35;
        else if (_twoDigitMins)
            szFactor = 0.50;
        else
            szFactor = 0.60;
        return outerDiameter * szFactor;
    }

    private Point CalcCentrePointOfDial(double totalWidthNeeded)
    {
        var margin = (_canvasWidth - totalWidthNeeded) / 2;
        return new Point(margin + _outerRadius, _canvasHeight / 2);
    }

    private void RenderPieSliceAndBall(double angle, double secondsElapsed)
    {
        if (_pie == null || _secondsBall == null || _time == null)
            return;

        _pie.Data = PieSlice.Get(angle, _centrePointOfDial, _innerRadius, _outerRadius);

        var ballPt = SecondsBall.GetPos(
            _centrePointOfDial,
            (int)secondsElapsed % 60,
            _secondsBall.Width / 2,
            _innerRadius);

        Canvas.SetLeft(_secondsBall, ballPt.X);
        Canvas.SetTop(_secondsBall, ballPt.Y);

        _time.Text = GetTimeText();
    }

    private void TimerFire(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_start != default)
        {
            var secsInCountdown = _countdownTotalSeconds;
            var secondsElapsed = (DateTime.UtcNow - _start).TotalSeconds;
            var secondsLeft = secsInCountdown - secondsElapsed;

            if (secondsLeft >= 0)
            {
                var angle = 360 - (((double)360 / secsInCountdown) * secondsLeft);
                RenderPieSliceAndBall(angle, secondsElapsed);
                _timer.Start();
            }
            else
            {
                RenderPieSliceAndBall(0, 0);
                TimeUpEvent?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _timer.Start();
        }
    }

    private void SetElementsVisibility()
    {
        if (_donut == null || _pie == null || _secondsBall == null || _time == null)
            return;

        switch (_elementsToShow)
        {
            case ElementsToShow.DialAndDigital:
                _secondsBall.IsVisible = true;
                _donut.IsVisible = true;
                _time.IsVisible = true;
                _pie.IsVisible = true;
                break;

            case ElementsToShow.Dial:
                _secondsBall.IsVisible = true;
                _donut.IsVisible = true;
                _pie.IsVisible = true;
                _time.IsVisible = false;
                break;

            case ElementsToShow.Digital:
                _time.IsVisible = true;
                _secondsBall.IsVisible = false;
                _donut.IsVisible = false;
                _pie.IsVisible = false;
                break;
        }

        if (_countdownTotalSeconds <= 60)
        {
            _secondsBall.IsVisible = false;
        }
    }

    private string GetTimeText(int? secsLeft = null)
    {
        double secondsLeft = secsLeft ?? 0;

        if (secsLeft == null)
        {
            if (_start == default)
            {
                secondsLeft = _countdownTotalSeconds;
            }
            else
            {
                var secsInCountdown = _countdownTotalSeconds;
                var secondsElapsed = (DateTime.UtcNow - _start).TotalSeconds;
                secondsLeft = secsInCountdown - secondsElapsed + 1;
            }
        }

        if (secondsLeft < 0) secondsLeft = 0;

        var totalMins = (int)secondsLeft / 60;
        var secs = (int)(secondsLeft % 60);

        if (totalMins >= 60)
        {
            var hours = totalMins / 60;
            var mins = totalMins % 60;
            return $"{hours}:{mins:D2}:{secs:D2}";
        }

        return _twoDigitMins
            ? $"{totalMins:D2}:{secs:D2}"
            : $"{totalMins}:{secs:D2}";
    }

    private static Color ToColor(string htmlColor)
    {
        return Color.Parse(htmlColor);
    }

    private void AddGeometryToCanvas(Canvas canvas)
    {
        var revealedColor = ToColor("#c0c5c1");
        var externalRingColor = ToColor("#74546a");
        var ballColor = ToColor("#eaf0ce");
        var ringStrokeColor = ToColor("#473341");
        var innerHighlightColor = Colors.White;

        _donut = new Path
        {
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(innerHighlightColor, 0.7),
                    new GradientStop(revealedColor, 1.0)
                }
            }
        };

        _pie = new Path
        {
            Stroke = new SolidColorBrush(ringStrokeColor),
            StrokeThickness = 1,
            Fill = new SolidColorBrush(externalRingColor)
        };

        _secondsBall = new Ellipse
        {
            Fill = new SolidColorBrush(ballColor)
        };

        var textColor = ToColor("#eaf0ce");
        _time = new TextBlock
        {
            Foreground = new SolidColorBrush(textColor),
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 12
        };

        canvas.Children.Add(_donut);
        canvas.Children.Add(_pie);
        canvas.Children.Add(_secondsBall);
        canvas.Children.Add(_time);
    }
}
