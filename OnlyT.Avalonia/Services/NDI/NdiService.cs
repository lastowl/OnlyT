namespace OnlyT.Avalonia.Services.NDI;

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using NewTek;
using NewTek.NDI;
using Serilog;

/// <summary>
/// NDI service implementation for streaming timer display via NDI protocol.
/// Uses RenderTargetBitmap to capture Avalonia visuals and sends via NDI SDK.
/// </summary>
public sealed class NdiService : INdiService
{
    private IntPtr _sendInstance = IntPtr.Zero;
    private Thread? _sendThread;
    private volatile bool _exitThread;
    private volatile bool _isPaused;
    private Control? _captureControl;
    private int _width = 1920;
    private int _height = 1080;
    private int _frameRate = 30;
    private readonly BlockingCollection<VideoFrame> _frameQueue = new(2);
    private DispatcherTimer? _captureTimer;

    public bool IsEnabled { get; private set; }
    public bool IsSending { get; private set; }
    public string SourceName { get; private set; } = "OnlyT Timer";

    public void Initialize(string sourceName)
    {
        if (IsEnabled)
            return;

        try
        {
            SourceName = sourceName;

            // Initialize NDI
            if (!NDIlib.initialize())
            {
                Log.Warning("NDI: Failed to initialize NDI library");
                return;
            }

            // Create NDI sender
            var sendDesc = new NDIlib.send_create_t
            {
                p_ndi_name = UTF.StringToUtf8(sourceName),
                clock_video = true,
                clock_audio = false
            };

            _sendInstance = NDIlib.send_create(ref sendDesc);

            if (_sendInstance == IntPtr.Zero)
            {
                Log.Warning("NDI: Failed to create NDI sender");
                NDIlib.destroy();
                return;
            }

            IsEnabled = true;
            Log.Information("NDI: Initialized with source name '{SourceName}'", sourceName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NDI: Initialization failed");
            IsEnabled = false;
        }
    }

    public void StartCapture(Control control, int width, int height, int frameRate = 30)
    {
        if (!IsEnabled || IsSending)
            return;

        _captureControl = control;
        _width = width;
        _height = height;
        _frameRate = frameRate;
        _exitThread = false;
        _isPaused = false;

        // Start the send thread
        _sendThread = new Thread(SendThreadProc)
        {
            IsBackground = true,
            Name = "NDI Send Thread"
        };
        _sendThread.Start();

        // Start capture timer on UI thread
        _captureTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / _frameRate)
        };
        _captureTimer.Tick += CaptureFrame;
        _captureTimer.Start();

        IsSending = true;
        Log.Information("NDI: Started capture at {Width}x{Height} @ {FrameRate}fps", width, height, frameRate);
    }

    public void StopCapture()
    {
        if (!IsSending)
            return;

        // Stop capture timer
        _captureTimer?.Stop();
        _captureTimer = null;

        // Signal thread to exit
        _exitThread = true;
        _frameQueue.CompleteAdding();

        // Wait for thread to finish
        _sendThread?.Join(1000);
        _sendThread = null;

        // Clear queue
        while (_frameQueue.TryTake(out var frame))
        {
            frame.Dispose();
        }

        IsSending = false;
        Log.Information("NDI: Stopped capture");
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
    }

    private void CaptureFrame(object? sender, EventArgs e)
    {
        if (_captureControl == null || _isPaused || _exitThread)
            return;

        try
        {
            // Create render target bitmap
            var pixelSize = new PixelSize(_width, _height);
            var dpi = new Vector(96, 96);

            using var rtb = new RenderTargetBitmap(pixelSize, dpi);
            rtb.Render(_captureControl);

            // Copy pixels to buffer
            var stride = _width * 4; // BGRA = 4 bytes per pixel
            var bufferSize = stride * _height;
            var buffer = new byte[bufferSize];

            // Pin buffer and copy pixels
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                rtb.CopyPixels(new PixelRect(0, 0, _width, _height), ptr, bufferSize, stride);
            }
            finally
            {
                handle.Free();
            }

            // Create video frame
            var frame = new VideoFrame(buffer, _width, _height, stride, _frameRate);

            // Try to add to queue (non-blocking)
            if (!_frameQueue.TryAdd(frame))
            {
                // Queue full, drop frame
                frame.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "NDI: Frame capture error");
        }
    }

    private void SendThreadProc()
    {
        while (!_exitThread)
        {
            try
            {
                if (_frameQueue.TryTake(out var frame, 100))
                {
                    SendVideoFrame(frame);
                    frame.Dispose();
                }
            }
            catch (InvalidOperationException)
            {
                // Queue completed
                break;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "NDI: Send thread error");
            }
        }
    }

    private void SendVideoFrame(VideoFrame frame)
    {
        if (_sendInstance == IntPtr.Zero)
            return;

        var videoFrame = new NDIlib.video_frame_v2_t
        {
            xres = frame.Width,
            yres = frame.Height,
            FourCC = NDIlib.FourCC_type_e.FourCC_type_BGRA,
            frame_rate_N = frame.FrameRate * 1000,
            frame_rate_D = 1001,
            picture_aspect_ratio = (float)frame.Width / frame.Height,
            frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
            timecode = NDIlib.send_timecode_synthesize,
            p_data = frame.DataPtr,
            line_stride_in_bytes = frame.Stride
        };

        NDIlib.send_send_video_v2(_sendInstance, ref videoFrame);
    }

    public void Dispose()
    {
        StopCapture();

        if (_sendInstance != IntPtr.Zero)
        {
            NDIlib.send_destroy(_sendInstance);
            _sendInstance = IntPtr.Zero;
        }

        NDIlib.destroy();
        IsEnabled = false;

        _frameQueue.Dispose();
        Log.Information("NDI: Disposed");
    }

    /// <summary>
    /// Internal class to hold video frame data
    /// </summary>
    private sealed class VideoFrame : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public int FrameRate { get; }
        public IntPtr DataPtr { get; private set; }
        private GCHandle _handle;

        public VideoFrame(byte[] data, int width, int height, int stride, int frameRate)
        {
            Width = width;
            Height = height;
            Stride = stride;
            FrameRate = frameRate;

            // Pin the data in memory
            _handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            DataPtr = _handle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
            DataPtr = IntPtr.Zero;
        }
    }
}
