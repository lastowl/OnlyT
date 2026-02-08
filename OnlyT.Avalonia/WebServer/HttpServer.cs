using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Timer;
using OnlyT.Avalonia.Services.TalkSchedule;
using OnlyT.Avalonia.WebServer.Models;
using Serilog;

namespace OnlyT.Avalonia.WebServer;

/// <summary>
/// HTTP server for remote control API
/// </summary>
public sealed class HttpServer : IHttpServer
{
    private const int OldestSupportedApiVer = 1;
    private const int CurrentApiVer = 4;
    private static readonly Guid SessionId = Guid.NewGuid();

    private readonly ITalkTimerService _timerService;
    private readonly ITalkScheduleService _scheduleService;
    private readonly IOptionsService _optionsService;
    private readonly IBellService? _bellService;
    private readonly IApiThrottler _throttler;
    private HttpListener? _listener;
    private int _port;
    private bool _disposed;

    public HttpServer(
        ITalkTimerService timerService,
        ITalkScheduleService scheduleService,
        IOptionsService optionsService,
        IBellService? bellService = null)
    {
        _timerService = timerService;
        _scheduleService = scheduleService;
        _optionsService = optionsService;
        _bellService = bellService;
        _throttler = new ApiThrottler(maxRequestsPerWindow: 100, windowSeconds: 60);
    }

    public bool IsRunning => _listener?.IsListening ?? false;

    public void Start(int port)
    {
        if (port <= 0) return;

        _port = port;
        _listener = new HttpListener();
        Task.Factory.StartNew(StartListening, TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        if (_listener?.IsListening ?? false)
        {
            _listener.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _listener?.Close();
        _listener = null;
        _throttler.Reset();
        (_throttler as IDisposable)?.Dispose();
        _disposed = true;
    }

    private void StartListening()
    {
        try
        {
            if (_listener == null) return;

            // Try different binding approaches - prioritize network-accessible bindings
            // Ports above 1024 don't require elevation on macOS/Linux
            var prefixesToTry = new[]
            {
                // Try * wildcard first (allows connections from any device on network)
                new[] {
                    $"http://*:{_port}/api/",
                    $"http://*:{_port}/data/",
                    $"http://*:{_port}/index/",
                    $"http://*:{_port}/timers/"
                },
                // Try + wildcard (Windows specific)
                new[] {
                    $"http://+:{_port}/api/",
                    $"http://+:{_port}/data/",
                    $"http://+:{_port}/index/",
                    $"http://+:{_port}/timers/"
                },
                // Fallback to localhost only (same machine access only)
                new[] {
                    $"http://localhost:{_port}/api/",
                    $"http://localhost:{_port}/data/",
                    $"http://localhost:{_port}/index/",
                    $"http://localhost:{_port}/timers/"
                }
            };

            bool started = false;
            string? boundPrefix = null;

            foreach (var prefixes in prefixesToTry)
            {
                try
                {
                    _listener.Prefixes.Clear();
                    foreach (var prefix in prefixes)
                    {
                        _listener.Prefixes.Add(prefix);
                    }

                    _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                    _listener.IgnoreWriteExceptions = true;
                    _listener.Start();

                    boundPrefix = prefixes[0].Replace("/api/", "");
                    started = true;
                    Log.Information("HTTP server started successfully on {Prefix}", boundPrefix);
                    break;
                }
                catch (HttpListenerException ex)
                {
                    Log.Warning("Failed to bind HTTP server to {Prefix}: {Message}", prefixes[0], ex.Message);
                    _listener.Close();
                    _listener = new HttpListener();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to start HTTP server with prefix {Prefix}", prefixes[0]);
                    _listener.Close();
                    _listener = new HttpListener();
                }
            }

            if (!started)
            {
                Log.Error("Failed to start HTTP server on any prefix. Port {Port} may be in use or blocked by firewall.", _port);
                return;
            }

            Log.Information("HTTP API available at: http://localhost:{Port}/index/", _port);

            while (_listener?.IsListening ?? false)
            {
                try
                {
                    var result = _listener.BeginGetContext(ListenerCallback, _listener);
                    result.AsyncWaitHandle.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    // Listener was disposed - exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    if (_listener?.IsListening ?? false)
                    {
                        Log.Warning(ex, "Error in HTTP listener loop");
                    }
                }
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            Log.Error("Access denied starting HTTP server. The port {Port} may require elevated permissions.", _port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting HTTP server on port {Port}", _port);
        }
    }

    private void ListenerCallback(IAsyncResult result)
    {
        if (!(_listener?.IsListening ?? false)) return;

        HttpListenerResponse? response = null;
        try
        {
            var context = _listener.EndGetContext(result);
            response = context.Response;

            // Check throttling if enabled
            if (_optionsService.IsApiThrottled)
            {
                var clientIp = GetClientIpAddress(context.Request);
                if (!_throttler.IsRequestAllowed(clientIp))
                {
                    WriteTooManyRequestsResponse(response, clientIp);
                    return;
                }
            }

            // Check API access code if configured (skip for index page)
            var segment = context.Request.Url?.Segments.Length > 1
                ? context.Request.Url.Segments[1].TrimEnd('/').ToLower()
                : "";

            if (!string.IsNullOrEmpty(_optionsService.ApiAccessCode) && segment != "index")
            {
                if (!IsValidAccessCode(context.Request))
                {
                    WriteUnauthorizedResponse(response);
                    return;
                }
            }

            if (context.Request.Url?.Segments.Length > 1)
            {
                switch (segment)
                {
                    case "api":
                        HandleApiRequest(context.Request, response);
                        break;
                    case "data":
                        HandleDataRequest(context.Request, response);
                        break;
                    case "index":
                        HandleIndexRequest(response);
                        break;
                    case "timers":
                        HandleTimersPageRequest(response);
                        break;
                }
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 995)
        {
            // Listener was stopped - ignore
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error handling HTTP request");
            WriteErrorResponse(response, HttpStatusCode.InternalServerError, ex.Message);
        }
        finally
        {
            response?.Close();
        }
    }

    private void HandleApiRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        // Handle CORS preflight
        if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            HandleOptionsMethod(request, response);
            return;
        }

        AddCorsHeaders(response);

        // Gate API on IsApiEnabled setting
        if (!_optionsService.IsApiEnabled)
        {
            WriteErrorResponse(response, HttpStatusCode.ServiceUnavailable, "API is disabled");
            return;
        }

        if (request.Url?.Segments.Length == 2)
        {
            // GET /api/ - return API version
            WriteJsonResponse(response, new ApiVersion
            {
                LowVersion = OldestSupportedApiVer,
                HighVersion = CurrentApiVer
            });
            return;
        }

        if (request.Url?.Segments.Length > 3)
        {
            var segment = request.Url.Segments[3].TrimEnd('/').ToLower();

            switch (segment)
            {
                case "timers":
                    HandleTimersApi(request, response);
                    break;
                case "datetime":
                    HandleDateTimeApi(response);
                    break;
                case "system":
                    HandleSystemApi(response);
                    break;
                case "bell":
                    HandleBellApi(request, response);
                    break;
                default:
                    WriteErrorResponse(response, HttpStatusCode.NotFound, "Unknown endpoint");
                    break;
            }
        }
    }

    private void HandleTimersApi(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            // Check if requesting a single timer: GET /api/v1/timers/{id}
            if (request.Url?.Segments.Length > 4 &&
                int.TryParse(request.Url.Segments[4].TrimEnd('/'), out var singleTalkId))
            {
                var talk = _scheduleService.GetTalkScheduleItem(singleTalkId);
                if (talk == null)
                {
                    WriteErrorResponse(response, HttpStatusCode.NotFound, "Timer does not exist");
                    return;
                }
                var timerData = GetTimersData();
                timerData.TimerInfo.RemoveAll(t => t.TalkId != singleTalkId);
                WriteJsonResponse(response, timerData);
            }
            else
            {
                // GET /api/v1/timers/ - get all timer info
                var timerData = GetTimersData();
                WriteJsonResponse(response, timerData);
            }
        }
        else if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            // POST /api/v1/timers/{id} - start timer
            if (request.Url?.Segments.Length > 4 &&
                int.TryParse(request.Url.Segments[4].TrimEnd('/'), out var talkId))
            {
                var result = _timerService.StartTalkTimerFromApi(talkId);
                WriteJsonResponse(response, new TimerStartStopResponse
                {
                    Success = result.Success,
                    TalkId = talkId,
                    Command = "Start",
                    Status = result.Success ? "started" : "failed",
                    CurrentStatus = GetCurrentTimerStatus()
                });
            }
            else
            {
                WriteErrorResponse(response, HttpStatusCode.BadRequest, "Invalid talk ID");
            }
        }
        else if (request.HttpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            // DELETE /api/v1/timers/{id} - stop timer
            if (request.Url?.Segments.Length > 4 &&
                int.TryParse(request.Url.Segments[4].TrimEnd('/'), out var talkId))
            {
                var result = _timerService.StopTalkTimerFromApi(talkId);
                WriteJsonResponse(response, new TimerStartStopResponse
                {
                    Success = result.Success,
                    TalkId = talkId,
                    Command = "Stop",
                    Status = result.Success ? "stopped" : "failed",
                    CurrentStatus = GetCurrentTimerStatus()
                });
            }
            else
            {
                WriteErrorResponse(response, HttpStatusCode.BadRequest, "Invalid talk ID");
            }
        }
    }

    private void HandleDateTimeApi(HttpListenerResponse response)
    {
        var now = DateTime.Now;
        var localTime = new LocalTime
        {
            Year = now.Year,
            Month = now.Month,
            Day = now.Day,
            Hour = now.Hour,
            Min = now.Minute,
            Sec = now.Second,
            TimeString24 = now.ToString("HH:mm:ss"),
            TimeString12 = now.ToString("hh:mm:ss tt")
        };
        WriteJsonResponse(response, localTime);
    }

    private void HandleSystemApi(HttpListenerResponse response)
    {
        AddCorsHeaders(response);
        response.AddHeader("Cache-Control", "no-cache");

        var currentCulture = Thread.CurrentThread.CurrentUICulture;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        var systemData = new ApiSystemData
        {
            AccountName = Environment.UserName,
            WorkingSet = Environment.WorkingSet,
            MachineName = Environment.MachineName,
            OnlyTVersion = version,
            SessionId = SessionId.ToString(),
            ApiEnabled = _optionsService.IsApiEnabled,
            ApiThrottled = _optionsService.IsApiThrottled,
            ApiCodeRequired = !string.IsNullOrEmpty(_optionsService.ApiAccessCode),
            Culture = new ApiCultureData
            {
                Name = currentCulture.Name,
                IsoCode2 = currentCulture.TwoLetterISOLanguageName,
                IsoCode3 = currentCulture.ThreeLetterISOLanguageName
            },
            ApiVersion = new ApiVersion
            {
                LowVersion = OldestSupportedApiVer,
                HighVersion = CurrentApiVer
            }
        };
        WriteJsonResponse(response, systemData);
    }

    private void HandleBellApi(HttpListenerRequest request, HttpListenerResponse response)
    {
        AddCorsHeaders(response);

        // Only POST method allowed for bell
        if (!request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteErrorResponse(response, HttpStatusCode.MethodNotAllowed, "Only POST method allowed");
            return;
        }

        var responseData = new BellResponseData();

        if (_bellService == null)
        {
            responseData.Success = false;
            responseData.Message = "Bell service not available";
            WriteJsonResponse(response, responseData);
            return;
        }

        if (!_optionsService.IsBellEnabled)
        {
            responseData.Success = false;
            responseData.Message = "Bell is disabled in settings";
            WriteJsonResponse(response, responseData);
            return;
        }

        if (!_bellService.IsPlaying)
        {
            responseData.Success = true;
            responseData.Message = "Bell triggered";
            _bellService.Play(_optionsService.BellVolumePercent);
            Log.Information("Bell triggered via API");
        }
        else
        {
            responseData.Success = false;
            responseData.Message = "Bell is already playing";
        }

        WriteJsonResponse(response, responseData);
    }

    private void HandleDataRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        AddCorsHeaders(response);

        // Return timer data for web clock display
        var status = _timerService.GetStatus();
        var now = DateTime.Now;

        // Calculate countdown to meeting start if timer is not running
        int? countdownSecs = null;
        bool showCountdown = false;
        var countdownDurationMins = _optionsService.CountdownDurationMins;

        if (!status.IsRunning)
        {
            var todayStartTime = _optionsService.MeetingStartTimes.GetStartTimeForDay(now.DayOfWeek);
            if (todayStartTime.HasValue)
            {
                var meetingStartDateTime = now.Date.Add(todayStartTime.Value);
                var timeUntilMeeting = meetingStartDateTime - now;

                // Show countdown if within configured duration before meeting and meeting hasn't started
                if (timeUntilMeeting.TotalSeconds > 0 && timeUntilMeeting.TotalMinutes <= countdownDurationMins)
                {
                    countdownSecs = (int)timeUntilMeeting.TotalSeconds;
                    showCountdown = true;
                }
            }
        }

        var data = new
        {
            time = now.ToString("HH:mm:ss"),
            isRunning = status.IsRunning,
            remainingSecs = status.IsRunning
                ? (int)(status.TargetSeconds - status.TimeElapsed.TotalSeconds)
                : 0,
            targetSecs = status.TargetSeconds,
            talkId = status.TalkId,
            showCountdown = showCountdown,
            countdownSecs = countdownSecs,
            countdownDurationMins = countdownDurationMins
        };

        WriteJsonResponse(response, data);
    }

    private TimersResponseData GetTimersData()
    {
        var result = new TimersResponseData();
        var serviceStatus = _timerService.GetStatus();

        // Get closing secs from the current talk if available
        var currentTalk = serviceStatus.TalkId.HasValue
            ? _scheduleService.GetTalkScheduleItem(serviceStatus.TalkId.Value)
            : null;

        result.Status = new TimerStatus
        {
            TalkId = serviceStatus.TalkId,
            TargetSeconds = serviceStatus.TargetSeconds,
            IsRunning = serviceStatus.IsRunning,
            TimeElapsed = serviceStatus.TimeElapsed,
            ClosingSecs = currentTalk?.ClosingSecs ?? 30
        };

        var countUpByDefault = _optionsService.CountUp;
        var talks = _scheduleService.GetTalkScheduleItems();
        foreach (var talk in talks)
        {
            var timerInfo = new TimerInfo
            {
                TalkId = talk.Id,
                TalkTitle = talk.Name,
                MeetingSectionNameInternal = talk.MeetingSectionNameInternal,
                MeetingSectionNameLocalised = talk.MeetingSectionNameLocalised,
                OriginalDurationSecs = (int)talk.OriginalDuration.TotalSeconds,
                ModifiedDurationSecs = talk.ModifiedDuration.HasValue
                    ? (int?)talk.ModifiedDuration.Value.TotalSeconds
                    : null,
                AdaptedDurationSecs = talk.AdaptedDuration.HasValue
                    ? (int?)talk.AdaptedDuration.Value.TotalSeconds
                    : null,
                ActualDurationSecs = (int)talk.ActualDuration.TotalSeconds,
                UsesBell = talk.BellApplicable,
                CompletedTimeSecs = talk.CompletedTimeSecs,
                CountUp = talk.CountUp ?? countUpByDefault,
                ClosingSecs = talk.ClosingSecs
            };

            result.TimerInfo.Add(timerInfo);
        }

        return result;
    }

    private TimerStatus GetCurrentTimerStatus()
    {
        var serviceStatus = _timerService.GetStatus();
        var currentTalk = serviceStatus.TalkId.HasValue
            ? _scheduleService.GetTalkScheduleItem(serviceStatus.TalkId.Value)
            : null;

        return new TimerStatus
        {
            TalkId = serviceStatus.TalkId,
            TargetSeconds = serviceStatus.TargetSeconds,
            IsRunning = serviceStatus.IsRunning,
            TimeElapsed = serviceStatus.TimeElapsed,
            ClosingSecs = currentTalk?.ClosingSecs ?? 30
        };
    }

    private static void HandleOptionsMethod(HttpListenerRequest request, HttpListenerResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.OK;
        var corsHeaders = request.Headers["Access-Control-Request-Headers"];

        if (corsHeaders != null)
        {
            response.AddHeader("Access-Control-Allow-Headers", corsHeaders);
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        }

        AddCorsHeaders(response);
    }

    private static void AddCorsHeaders(HttpListenerResponse response)
    {
        response.AddHeader("Access-Control-Allow-Origin", "*");
    }

    private static void WriteJsonResponse(HttpListenerResponse response, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        var buffer = Encoding.UTF8.GetBytes(json);

        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    private static void WriteErrorResponse(HttpListenerResponse? response, HttpStatusCode statusCode, string message)
    {
        if (response == null) return;

        try
        {
            response.StatusCode = (int)statusCode;
            var error = new { error = message };
            WriteJsonResponse(response, error);
        }
        catch
        {
            // Ignore - response may already be closed
        }
    }

    private void HandleIndexRequest(HttpListenerResponse response)
    {
        AddCorsHeaders(response);

        // Return a simple HTML page for the web clock with countdown support
        var html = @"<!DOCTYPE html>
<html>
<head>
    <title>OnlyT Web Clock</title>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <style>
        body { font-family: Arial, sans-serif; background: #1a1a1a; color: white; text-align: center; padding-top: 50px; margin: 0; }
        #clock { font-size: 120px; font-family: 'Consolas', monospace; }
        #timer { font-size: 80px; font-family: 'Consolas', monospace; margin-top: 30px; }
        #countdown { font-size: 100px; font-family: 'Consolas', monospace; margin-top: 30px; display: none; }
        #countdown-label { font-size: 20px; color: #888; margin-bottom: 10px; display: none; }
        .running { color: #4CAF50; }
        .overtime { color: #F44336; }
        .idle { color: #888; }
        .countdown-active { color: #FFC107; }
        .countdown-soon { color: #FF9800; }
        .countdown-imminent { color: #F44336; }
        @media (max-width: 600px) {
            #clock { font-size: 60px; }
            #timer { font-size: 50px; }
            #countdown { font-size: 60px; }
        }
    </style>
</head>
<body>
    <div id=""clock"">--:--:--</div>
    <div id=""countdown-label"">Meeting starts in</div>
    <div id=""countdown"" class=""countdown-active"">--:--</div>
    <div id=""timer"" class=""idle"">00:00</div>
    <script>
        function updateClock() {
            var now = new Date();
            document.getElementById('clock').textContent =
                now.toTimeString().split(' ')[0];
        }
        function formatTime(totalSecs) {
            var hours = Math.floor(totalSecs / 3600);
            var mins = Math.floor((totalSecs % 3600) / 60);
            var secs = totalSecs % 60;
            if (hours > 0) {
                return hours + ':' + mins.toString().padStart(2,'0') + ':' + secs.toString().padStart(2,'0');
            }
            return mins.toString().padStart(2,'0') + ':' + secs.toString().padStart(2,'0');
        }
        function fetchData() {
            fetch('/data/')
                .then(r => r.json())
                .then(data => {
                    var timerEl = document.getElementById('timer');
                    var countdownEl = document.getElementById('countdown');
                    var countdownLabel = document.getElementById('countdown-label');

                    if (data.isRunning) {
                        // Show timer, hide countdown
                        timerEl.style.display = 'block';
                        countdownEl.style.display = 'none';
                        countdownLabel.style.display = 'none';

                        var mins = Math.floor(Math.abs(data.remainingSecs) / 60);
                        var secs = Math.abs(data.remainingSecs) % 60;
                        var sign = data.remainingSecs < 0 ? '-' : '';
                        timerEl.textContent = sign + mins.toString().padStart(2,'0') + ':' + secs.toString().padStart(2,'0');
                        timerEl.className = data.remainingSecs < 0 ? 'overtime' : 'running';
                    } else if (data.showCountdown && data.countdownSecs !== null) {
                        // Show countdown, hide timer
                        timerEl.style.display = 'none';
                        countdownEl.style.display = 'block';
                        countdownLabel.style.display = 'block';

                        countdownEl.textContent = formatTime(data.countdownSecs);

                        // Change color based on time remaining
                        if (data.countdownSecs <= 30) {
                            countdownEl.className = 'countdown-imminent';
                        } else if (data.countdownSecs <= 60) {
                            countdownEl.className = 'countdown-soon';
                        } else {
                            countdownEl.className = 'countdown-active';
                        }
                    } else {
                        // No timer, no countdown - show idle
                        timerEl.style.display = 'block';
                        countdownEl.style.display = 'none';
                        countdownLabel.style.display = 'none';
                        timerEl.textContent = '00:00';
                        timerEl.className = 'idle';
                    }
                })
                .catch(() => {});
        }
        setInterval(updateClock, 1000);
        setInterval(fetchData, 500);
        updateClock();
        fetchData();
    </script>
</body>
</html>";

        WriteHtmlResponse(response, html);
    }

    private void HandleTimersPageRequest(HttpListenerResponse response)
    {
        AddCorsHeaders(response);

        // Return JSON data for timers (same as /api/v1/timers/)
        var timerData = GetTimersData();
        WriteJsonResponse(response, timerData);
    }

    private static void WriteHtmlResponse(HttpListenerResponse response, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
    }

    private static string GetClientIpAddress(HttpListenerRequest request)
    {
        // Try to get IP from X-Forwarded-For header (if behind proxy)
        var forwardedFor = request.Headers["X-Forwarded-For"];
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the list
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                return firstIp;
            }
        }

        // Fall back to remote endpoint
        return request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
    }

    private void WriteTooManyRequestsResponse(HttpListenerResponse response, string clientIp)
    {
        try
        {
            response.StatusCode = 429; // Too Many Requests
            response.AddHeader("Retry-After", "60");
            AddCorsHeaders(response);

            var error = new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = 60
            };

            WriteJsonResponse(response, error);
            Log.Debug("Throttled request from {ClientIp}", clientIp);
        }
        catch
        {
            // Ignore - response may already be closed
        }
    }

    private bool IsValidAccessCode(HttpListenerRequest request)
    {
        var expectedCode = _optionsService.ApiAccessCode;
        if (string.IsNullOrEmpty(expectedCode))
        {
            return true; // No code required
        }

        // Check Authorization header (Bearer token)
        var authHeader = request.Headers["Authorization"];
        if (!string.IsNullOrEmpty(authHeader))
        {
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring(7).Trim();
                if (string.Equals(token, expectedCode, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // Check X-Api-Key header
        var apiKey = request.Headers["X-Api-Key"];
        if (!string.IsNullOrEmpty(apiKey) &&
            string.Equals(apiKey, expectedCode, StringComparison.Ordinal))
        {
            return true;
        }

        // Check query parameter "code"
        var queryCode = request.QueryString["code"];
        if (!string.IsNullOrEmpty(queryCode) &&
            string.Equals(queryCode, expectedCode, StringComparison.Ordinal))
        {
            return true;
        }

        // WPF compatibility: Check "ApiCode" header or query parameter
        var apiCode = request.Headers["ApiCode"] ?? request.QueryString["ApiCode"];
        if (!string.IsNullOrEmpty(apiCode) &&
            string.Equals(apiCode, expectedCode, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static void WriteUnauthorizedResponse(HttpListenerResponse response)
    {
        try
        {
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.AddHeader("WWW-Authenticate", "Bearer");
            AddCorsHeaders(response);

            var error = new
            {
                error = "Unauthorized",
                message = "Valid API access code required. Provide via Authorization header (Bearer token), X-Api-Key header, or 'code' query parameter."
            };

            WriteJsonResponse(response, error);
            Log.Debug("Unauthorized API request - invalid or missing access code");
        }
        catch
        {
            // Ignore - response may already be closed
        }
    }
}
