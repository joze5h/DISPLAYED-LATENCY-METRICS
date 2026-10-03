using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayedLatencyMetrics.App;

internal sealed unsafe class StartupSplashWindow : IDisposable
{
    private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.StartupSplash";
    private const string ImageResourceName = "DisplayedLatencyMetrics.Assets.image.png";
    private const nuint TimerId = 1;

    private const int CanvasWidth = 600;
    private const int CanvasHeight = 400;
    private const double InitialScale = 0.85d;
    private const double FinalScale = 0.99d;

    private const long FadeInMilliseconds = 120;
    private const long ScaleMilliseconds = 1_650;
    private const long FadeOutMilliseconds = 230;
    private const long TotalMilliseconds = FadeInMilliseconds + ScaleMilliseconds + FadeOutMilliseconds;

    private const int UnitPixel = 2;
    private const int InterpolationModeHighQualityBicubic = 7;
    private const int PixelOffsetModeHighQuality = 2;

    private static StartupSplashWindow? s_current;

    private nint _instance;
    private nint _className;
    private nint _window;

    private nint _gdiplusToken;
    private nint _image;
    private uint _imageWidth;
    private uint _imageHeight;
    private string? _imagePath;

    private nint _screenDc;
    private nint _memoryDc;
    private nint _bitmap;
    private nint _previousBitmap;
    private nint _bits;
    private nint _graphics;

    private readonly Stopwatch _clock = new();
    private int _screenCenterX;
    private int _screenCenterY;
    private double _currentScale = InitialScale;
    private byte _currentOpacity;
    private bool _animationFailureLogged;
    private bool _graphicsFailureLogged;
    private bool _timerResolutionActive;

    internal static void Show()
    {
        try
        {
            using var splash = new StartupSplashWindow();
            splash.Run();
        }
        catch (Exception exception)
        {

            Logger.Warning("Startup splash could not be shown.", exception);
        }
    }

    private void Run()
    {
        if (!LoadImage())
        {
            return;
        }

        s_current = this;
        _instance = Win32.GetModuleHandle(null);
        _className = Marshal.StringToHGlobalUni(WindowClassName);

        Win32.WindowClassEx windowClass = new()
        {
            Size = (uint)sizeof(Win32.WindowClassEx),
            Instance = _instance,
            Cursor = Win32.LoadCursor(0, Win32.IDC_ARROW),
            WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
            ClassName = _className,
        };
        if (Win32.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"Unable to register splash window. Win32={Marshal.GetLastWin32Error()}");
        }

        _screenCenterX = Win32.GetSystemMetrics(Win32.SM_CXSCREEN) / 2;
        _screenCenterY = Win32.GetSystemMetrics(Win32.SM_CYSCREEN) / 2;
        int x = _screenCenterX - (CanvasWidth / 2);
        int y = _screenCenterY - (CanvasHeight / 2);

        _window = Win32.CreateWindowEx(
            Win32.WS_EX_LAYERED | Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE,
            WindowClassName,
            "DISPLAYED-LATENCY-METRICS",
            Win32.WS_POPUP,
            x,
            y,
            CanvasWidth,
            CanvasHeight,
            0,
            0,
            _instance,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Unable to create splash window. Win32={Marshal.GetLastWin32Error()}");
        }

        CreateRenderSurface();

        RenderFrame();
        Win32.ShowWindow(_window, Win32.SW_SHOWNOACTIVATE);

        _timerResolutionActive = Win32.TimeBeginPeriod(1) == 0;
        _clock.Start();
        if (Win32.SetTimer(_window, TimerId, 16, 0) == 0)
        {
            throw new InvalidOperationException(
                $"Unable to start splash animation timer. Win32={Marshal.GetLastWin32Error()}");
        }

        while (true)
        {
            int result = Win32.GetMessage(out Win32.Message message, 0, 0, 0);
            if (result == 0)
            {
                break;
            }
            if (result < 0)
            {
                throw new InvalidOperationException(
                    $"Startup splash message loop failed. Win32={Marshal.GetLastWin32Error()}");
            }

            Win32.TranslateMessage(ref message);
            Win32.DispatchMessage(ref message);
        }
    }

    private bool LoadImage()
    {
        Win32.GdiplusStartupInput input = new() { Version = 1 };
        if (Win32.GdiplusStartup(out _gdiplusToken, ref input, 0) != 0)
        {
            return false;
        }

        try
        {
            using Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ImageResourceName);
            if (source is null)
            {
                Logger.Warning($"Startup splash image resource '{ImageResourceName}' was not found.");
                return false;
            }

            _imagePath = Path.Combine(Path.GetTempPath(), $"DLM-splash-{Guid.NewGuid():N}.png");
            using (FileStream destination = new(_imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                source.CopyTo(destination);
            }

            if (Win32.GdipCreateBitmapFromFile(_imagePath, out _image) != 0 || _image == 0)
            {
                Logger.Warning("GDI+ could not decode the embedded startup splash image.");
                return false;
            }

            if (Win32.GdipGetImageWidth(_image, out _imageWidth) != 0 ||
                Win32.GdipGetImageHeight(_image, out _imageHeight) != 0 ||
                _imageWidth == 0 || _imageHeight == 0)
            {
                Logger.Warning("GDI+ could not read the startup splash dimensions.");
                return false;
            }

            return true;
        }
        catch
        {
            DisposeImage();
            throw;
        }
    }

    private void CreateRenderSurface()
    {
        _screenDc = Win32.GetDc(0);
        if (_screenDc == 0)
        {
            throw new InvalidOperationException($"Unable to obtain the splash screen DC. Win32={Marshal.GetLastWin32Error()}");
        }

        _memoryDc = Win32.CreateCompatibleDc(_screenDc);
        if (_memoryDc == 0)
        {
            throw new InvalidOperationException($"Unable to create the splash memory DC. Win32={Marshal.GetLastWin32Error()}");
        }

        Win32.BitmapInfo bitmapInfo = new()
        {
            Header = new Win32.BitmapInfoHeader
            {
                Size = (uint)sizeof(Win32.BitmapInfoHeader),
                Width = CanvasWidth,
                Height = -CanvasHeight,
                Planes = 1,
                BitCount = 32,
                Compression = Win32.BI_RGB,
                SizeImage = (uint)(CanvasWidth * CanvasHeight * 4),
            },
        };

        _bitmap = Win32.CreateDibSection(_memoryDc, &bitmapInfo, Win32.DIB_RGB_COLORS, out _bits, 0, 0);
        if (_bitmap == 0 || _bits == 0)
        {
            throw new InvalidOperationException($"Unable to create the splash DIB. Win32={Marshal.GetLastWin32Error()}");
        }

        _previousBitmap = Win32.SelectObject(_memoryDc, _bitmap);

        if (Win32.GdipCreateFromHDC(_memoryDc, out _graphics) != 0 || _graphics == 0)
        {
            throw new InvalidOperationException("GDI+ could not create the persistent splash graphics surface.");
        }

        _ = Win32.GdipSetInterpolationMode(_graphics, InterpolationModeHighQualityBicubic);
        _ = Win32.GdipSetPixelOffsetMode(_graphics, PixelOffsetModeHighQuality);
    }

    private void RenderFrame()
    {
        if (_window == 0 || _image == 0 || _bits == 0 || _graphics == 0)
        {
            return;
        }

        new Span<byte>((void*)_bits, CanvasWidth * CanvasHeight * 4).Clear();

        double fitScale = Math.Min(
            CanvasWidth / (double)_imageWidth,
            CanvasHeight / (double)_imageHeight);

        int drawWidth = ToCenteredEvenPixels(_imageWidth * fitScale * _currentScale, CanvasWidth);
        int drawHeight = ToCenteredEvenPixels(_imageHeight * fitScale * _currentScale, CanvasHeight);
        int drawX = (CanvasWidth - drawWidth) / 2;
        int drawY = (CanvasHeight - drawHeight) / 2;

        int drawStatus = Win32.GdipDrawImageRectRect(
            _graphics,
            _image,
            drawX,
            drawY,
            drawWidth,
            drawHeight,
            0f,
            0f,
            _imageWidth,
            _imageHeight,
            UnitPixel,
            0,
            0,
            0);
        if (drawStatus != 0)
        {
            if (!_graphicsFailureLogged)
            {
                _graphicsFailureLogged = true;
                Logger.Warning($"GDI+ could not draw the startup splash image. Status={drawStatus}");
            }
            return;
        }

        Win32.Point destination = new(
            _screenCenterX - (CanvasWidth / 2),
            _screenCenterY - (CanvasHeight / 2));
        Win32.Size size = new(CanvasWidth, CanvasHeight);
        Win32.Point source = new(0, 0);
        Win32.BlendFunction blend = new()
        {
            BlendOp = Win32.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = _currentOpacity,
            AlphaFormat = Win32.AC_SRC_ALPHA,
        };

        if (!Win32.UpdateLayeredWindow(
                _window,
                _screenDc,
                &destination,
                &size,
                _memoryDc,
                &source,
                0,
                &blend,
                Win32.ULW_ALPHA))
        {
            LogAnimationFailure("update the layered startup splash");
        }
    }

    private void Tick()
    {
        long elapsed = _clock.ElapsedMilliseconds;
        if (elapsed >= TotalMilliseconds)
        {
            Win32.KillTimer(_window, TimerId);
            Win32.DestroyWindow(_window);
            return;
        }

        long scaleElapsed = Math.Clamp(elapsed - FadeInMilliseconds, 0L, ScaleMilliseconds);
        double scaleProgress = scaleElapsed / (double)ScaleMilliseconds;
        double easedScaleProgress = 1d - Math.Pow(1d - scaleProgress, 3d);
        _currentScale = InitialScale + ((FinalScale - InitialScale) * easedScaleProgress);

        double alpha;
        if (elapsed < FadeInMilliseconds)
        {
            double fadeInProgress = elapsed / (double)FadeInMilliseconds;
            alpha = 1d - Math.Pow(1d - fadeInProgress, 3d);
        }
        else if (elapsed < FadeInMilliseconds + ScaleMilliseconds)
        {
            alpha = 1d;
        }
        else
        {
            double fadeOutProgress = (elapsed - FadeInMilliseconds - ScaleMilliseconds) /
                (double)FadeOutMilliseconds;
            alpha = 1d - (fadeOutProgress * fadeOutProgress);
        }

        _currentOpacity = (byte)Math.Clamp((int)Math.Round(alpha * 255d), 0, 255);
        RenderFrame();
    }

    private static int ToCenteredEvenPixels(double value, int canvasExtent)
    {
        int pixels = Math.Max(2, checked((int)Math.Round(value)));
        if ((pixels & 1) != (canvasExtent & 1))
        {
            pixels++;
        }

        return pixels;
    }

    private void LogAnimationFailure(string operation)
    {
        if (_animationFailureLogged)
        {
            return;
        }

        _animationFailureLogged = true;
        Logger.Warning($"Unable to {operation}. Win32={Marshal.GetLastWin32Error()}");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        StartupSplashWindow? splash = s_current;
        if (splash is null)
        {
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        switch (message)
        {
            case Win32.WM_TIMER:
                splash.Tick();
                return 0;
            case Win32.WM_ERASEBKGND:
            case Win32.WM_PAINT:
                return 0;
            case Win32.WM_CLOSE:
                Win32.DestroyWindow(window);
                return 0;
            case Win32.WM_DESTROY:
                splash._window = 0;
                Win32.PostQuitMessage(0);
                return 0;
            default:
                return Win32.DefWindowProc(window, message, wParam, lParam);
        }
    }

    public void Dispose()
    {
        if (_window != 0)
        {
            Win32.KillTimer(_window, TimerId);
            Win32.DestroyWindow(_window);
            _window = 0;
        }

        if (_timerResolutionActive)
        {
            _ = Win32.TimeEndPeriod(1);
            _timerResolutionActive = false;
        }

        DisposeRenderSurface();

        if (_className != 0)
        {
            Win32.UnregisterClass(WindowClassName, _instance);
            Marshal.FreeHGlobal(_className);
            _className = 0;
        }

        s_current = null;
        DisposeImage();
    }

    private void DisposeRenderSurface()
    {
        if (_graphics != 0)
        {
            int status = Win32.GdipDeleteGraphics(_graphics);
            if (status != 0 && !_graphicsFailureLogged)
            {
                _graphicsFailureLogged = true;
                Logger.Warning($"GDI+ could not release the splash graphics surface. Status={status}");
            }
            _graphics = 0;
        }

        if (_previousBitmap != 0 && _memoryDc != 0)
        {
            _ = Win32.SelectObject(_memoryDc, _previousBitmap);
            _previousBitmap = 0;
        }
        if (_bitmap != 0)
        {
            _ = Win32.DeleteObject(_bitmap);
            _bitmap = 0;
        }
        _bits = 0;

        if (_memoryDc != 0)
        {
            _ = Win32.DeleteDc(_memoryDc);
            _memoryDc = 0;
        }
        if (_screenDc != 0)
        {
            _ = Win32.ReleaseDc(0, _screenDc);
            _screenDc = 0;
        }
    }

    private void DisposeImage()
    {
        if (_image != 0)
        {
            int status = Win32.GdipDisposeImage(_image);
            if (status != 0)
            {
                Logger.Warning($"GDI+ could not release the startup splash image. Status={status}");
            }
            _image = 0;
        }
        if (_gdiplusToken != 0)
        {
            Win32.GdiplusShutdown(_gdiplusToken);
            _gdiplusToken = 0;
        }
        if (!string.IsNullOrEmpty(_imagePath))
        {
            try
            {
                File.Delete(_imagePath);
            }
            catch (Exception exception)
            {
                Logger.Warning("Unable to delete the temporary startup splash image.", exception);
            }
            _imagePath = null;
        }
    }
}
