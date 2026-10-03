using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics.App;

internal sealed class UiTheme : IDisposable
{
    internal static readonly uint BackgroundColor = Win32.Color(10, 14, 19);
    internal static readonly uint TextColor = Win32.Color(226, 233, 239);
    internal static readonly uint BorderColor = Win32.Color(46, 61, 75);

    private readonly uint _dpi;
    private bool _disposed;

    internal UiTheme()
    {
        _dpi = Math.Max(96U, Win32.GetDpiForSystem());
        UiSemiboldFont = CreateFont(10, Win32.FW_SEMIBOLD);
        UiSmallFont = CreateFont(8, Win32.FW_NORMAL);

        BackgroundBrush = Win32.CreateSolidBrush(BackgroundColor);
    }

    internal nint UiSemiboldFont { get; }
    internal nint UiSmallFont { get; }
    internal nint BackgroundBrush { get; }
    private const string FontFace = "Consolas";

    internal int Scale(int value) => checked((int)Math.Round(value * _dpi / 96.0));

    internal static nint CreateOverlayFont(int sizeDip, uint targetDpi)
    {
        int height = -Math.Max(1, checked((int)Math.Round(sizeDip * Math.Max(96U, targetDpi) / 96.0)));
        return CreateFontCore(height, Win32.FW_NORMAL);
    }

    internal static void DeleteOverlayFont(nint font) => DeleteOwnedFont(font);

    internal static unsafe void ApplyTopLevelStyle(nint window)
    {
        int enabled = 1;
        int darkModeResult = Win32.DwmSetWindowAttribute(
            window,
            Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
            &enabled,
            sizeof(int));

        int corners = Win32.DWMWCP_DONOTROUND;
        int cornersResult = Win32.DwmSetWindowAttribute(
            window,
            Win32.DWMWA_WINDOW_CORNER_PREFERENCE,
            &corners,
            sizeof(int));

        uint caption = BackgroundColor;
        uint text = TextColor;
        uint border = BorderColor;
        int captionResult = Win32.DwmSetWindowAttribute(
            window,
            Win32.DWMWA_CAPTION_COLOR,
            &caption,
            sizeof(uint));
        int textResult = Win32.DwmSetWindowAttribute(
            window,
            Win32.DWMWA_TEXT_COLOR,
            &text,
            sizeof(uint));
        int borderResult = Win32.DwmSetWindowAttribute(
            window,
            Win32.DWMWA_BORDER_COLOR,
            &border,
            sizeof(uint));

        if (darkModeResult < 0 ||
            cornersResult < 0 ||
            captionResult < 0 ||
            textResult < 0 ||
            borderResult < 0)
        {
            Logger.Warning(
                "DWM rejected one or more window style attributes. " +
                $"Dark=0x{darkModeResult:X8}; Corners=0x{cornersResult:X8}; " +
                $"Caption=0x{captionResult:X8}; Text=0x{textResult:X8}; Border=0x{borderResult:X8}");
        }
    }

    internal static void ApplyControl(nint control, nint font)
    {
        if (control == 0)
        {
            return;
        }
        Win32.SetControlFont(control, font);
        Win32.SetWindowTheme(control, "DarkMode_Explorer", null);
    }

    internal unsafe (int X, int Y, int Width, int Height) CenteredWindow(nint owner, int widthDip, int heightDip)
    {
        int width = Scale(widthDip);
        int height = Scale(heightDip);
        nint monitor = Win32.MonitorFromWindow(owner != 0 ? owner : Win32.GetForegroundWindow(), Win32.MONITOR_DEFAULTTONEAREST);
        Win32.MonitorInfo info = new() { Size = (uint)sizeof(Win32.MonitorInfo) };
        if (monitor != 0 && Win32.GetMonitorInfo(monitor, ref info))
        {
            width = Math.Min(width, Math.Max(Scale(480), info.Work.Width - Scale(24)));
            height = Math.Min(height, Math.Max(Scale(360), info.Work.Height - Scale(24)));
            int x = info.Work.Left + Math.Max(0, (info.Work.Width - width) / 2);
            int y = info.Work.Top + Math.Max(0, (info.Work.Height - height) / 2);
            return (x, y, width, height);
        }

        return (
            Math.Max(0, (Win32.GetSystemMetrics(Win32.SM_CXSCREEN) - width) / 2),
            Math.Max(0, (Win32.GetSystemMetrics(Win32.SM_CYSCREEN) - height) / 2),
            width,
            height);
    }

    private nint CreateFont(int pointSize, uint weight)
    {
        int height = -Math.Max(1, checked((int)Math.Round(pointSize * _dpi / 72.0)));
        return CreateFontCore(height, weight);
    }

    private static nint CreateFontCore(int height, uint weight)
    {
        nint font = Win32.CreateFont(
            height,
            0,
            0,
            0,
            weight,
            0,
            0,
            0,
            Win32.DEFAULT_CHARSET,
            Win32.OUT_DEFAULT_PRECIS,
            Win32.CLIP_DEFAULT_PRECIS,
            Win32.CLEARTYPE_QUALITY,
            Win32.DEFAULT_PITCH | Win32.FF_DONTCARE,
            FontFace);
        return font != 0 ? font : Win32.GetStockObject(Win32.DEFAULT_GUI_FONT);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        DeleteOwnedFont(UiSemiboldFont);
        DeleteOwnedFont(UiSmallFont);
        DeleteOwnedObject(BackgroundBrush);
        GC.SuppressFinalize(this);
    }

    private static void DeleteOwnedFont(nint font)
    {
        if (font != 0 && font != Win32.GetStockObject(Win32.DEFAULT_GUI_FONT))
        {
            Win32.DeleteObject(font);
        }
    }

    private static void DeleteOwnedObject(nint value)
    {
        if (value != 0)
        {
            Win32.DeleteObject(value);
        }
    }
}
