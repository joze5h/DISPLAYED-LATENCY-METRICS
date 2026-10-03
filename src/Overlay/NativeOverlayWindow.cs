using DisplayedLatencyMetrics.App;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayedLatencyMetrics.Overlay;

internal sealed unsafe class NativeOverlayWindow : IDisposable
{
    private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.Overlay";
    private const uint ApplyUpdateMessage = Win32.WM_APP + 41;

    private readonly record struct PendingUpdate(
        nint TargetWindow,
        OverlayDocument Document,
        OverlayPresentationSettings Presentation);

    private readonly record struct OverlayPalette(
        uint Background,
        uint Border,
        uint Text,
        uint Accent,
        uint Muted);

    private static NativeOverlayWindow? s_current;

    private readonly Lock _sync = new();
    private readonly UiTheme _theme;
    private readonly nint _instance;
    private nint _classNamePointer;
    private nint _window;
    private nint _memoryDc;
    private nint _bitmap;
    private nint _previousBitmap;
    private nint _bits;
    private nint _font;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private int _fontSize;
    private uint _fontDpi;
    private PendingUpdate _pending;
    private bool _hasPending;
    private bool _messagePosted;
    private bool _disposed;
    private bool _reportedRenderFailure;

    internal NativeOverlayWindow(UiTheme theme, nint instance)
    {
        _theme = theme;
        _instance = instance;
        RegisterAndCreateWindow();
    }

    internal void Publish(
        nint targetWindow,
        OverlayDocument document,
        OverlayPresentationSettings presentation)
    {
        QueueUpdate(new PendingUpdate(targetWindow, document, presentation));
    }

    internal void Clear() => QueueUpdate(new PendingUpdate(0, OverlayDocument.Empty, default));

    private void QueueUpdate(PendingUpdate update)
    {
        nint window;
        lock (_sync)
        {
            if (_disposed || _window == 0)
            {
                return;
            }

            _pending = update;
            _hasPending = true;
            if (_messagePosted)
            {
                return;
            }
            _messagePosted = true;
            window = _window;
        }

        if (!Win32.PostMessage(window, ApplyUpdateMessage, 0, 0))
        {
            lock (_sync)
            {
                _messagePosted = false;
            }
        }
    }

    private void ApplyPendingUpdate()
    {
        PendingUpdate update;
        lock (_sync)
        {
            _messagePosted = false;
            if (!_hasPending)
            {
                return;
            }
            update = _pending;
            _hasPending = false;
        }

        if (update.TargetWindow == 0 || update.Document.Lines.Length == 0 ||
            !Win32.IsWindow(update.TargetWindow) || !Win32.IsWindowVisible(update.TargetWindow) ||
            Win32.IsIconic(update.TargetWindow) || Win32.GetForegroundWindow() != update.TargetWindow)
        {
            Hide();
            return;
        }

        if (!TryGetTargetClientRect(update.TargetWindow, out Win32.Rect targetRect))
        {
            Hide();
            return;
        }

        uint dpi = Math.Max(96U, Win32.GetDpiForWindow(update.TargetWindow));
        if (!Render(update.Document, update.Presentation, dpi, targetRect, out int x, out int y))
        {
            Hide();
            if (!_reportedRenderFailure)
            {
                _reportedRenderFailure = true;
                Logger.Error($"Native overlay render failed. Win32={Marshal.GetLastWin32Error()}");
            }
            return;
        }

        _reportedRenderFailure = false;
        Win32.SetWindowPos(
            _window,
            Win32.HWND_TOPMOST,
            x,
            y,
            _bitmapWidth,
            _bitmapHeight,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        Win32.ShowWindow(_window, Win32.SW_SHOWNOACTIVATE);
    }

    private bool Render(
        OverlayDocument document,
        OverlayPresentationSettings presentation,
        uint dpi,
        Win32.Rect targetRect,
        out int destinationX,
        out int destinationY)
    {
        destinationX = 0;
        destinationY = 0;
        if (!EnsureMemoryDc() || !EnsureFont(presentation.FontSize, dpi))
        {
            return false;
        }

        nint previousFont = Win32.SelectObject(_memoryDc, _font);
        if (Win32.SetBkMode(_memoryDc, Win32.TRANSPARENT) == 0)
        {
            Win32.SelectObject(_memoryDc, previousFont);
            return false;
        }
        string measurement = "Ag";
        if (!Win32.GetTextExtentPoint32(_memoryDc, measurement, measurement.Length, out Win32.Size textSize))
        {
            Win32.SelectObject(_memoryDc, previousFont);
            return false;
        }

        int maxTextWidth = 0;
        foreach (OverlayLine line in document.Lines)
        {
            if (Win32.GetTextExtentPoint32(_memoryDc, line.Text, line.Text.Length, out Win32.Size size))
            {
                maxTextWidth = Math.Max(maxTextWidth, size.Width);
            }
        }

        int padding = Scale(presentation.Padding, dpi);
        int rowGap = Math.Max(1, Scale(3, dpi));
        int lineHeight = Math.Max(1, textSize.Height + rowGap);
        int maximumWidth = Math.Max(120, targetRect.Width - Scale(12, dpi));
        int width = Math.Clamp(maxTextWidth + padding * 2, 1, Math.Min(8192, maximumWidth));
        int height = Math.Clamp(document.Lines.Length * lineHeight + padding * 2, 1, 4096);

        if (!EnsureBitmap(width, height))
        {
            Win32.SelectObject(_memoryDc, previousFont);
            return false;
        }

        NativeMemory.Clear((void*)_bits, checked((nuint)(width * height * sizeof(uint))));
        OverlayPalette palette = GetPalette(presentation.ColorTheme);

        nint dcBrush = Win32.GetStockObject(Win32.DC_BRUSH);
        nint dcPen = Win32.GetStockObject(Win32.DC_PEN);
        nint oldBrush = Win32.SelectObject(_memoryDc, dcBrush);
        nint oldPen = Win32.SelectObject(_memoryDc, dcPen);
        uint previousBrushColor = Win32.SetDcBrushColor(_memoryDc, palette.Background);
        uint previousPenColor = Win32.SetDcPenColor(_memoryDc, palette.Border);
        if (previousBrushColor == uint.MaxValue || previousPenColor == uint.MaxValue)
        {
            Win32.SelectObject(_memoryDc, oldPen);
            Win32.SelectObject(_memoryDc, oldBrush);
            Win32.SelectObject(_memoryDc, previousFont);
            return false;
        }
        Win32.Rectangle(_memoryDc, 0, 0, width, height);
        Win32.SelectObject(_memoryDc, oldPen);
        Win32.SelectObject(_memoryDc, oldBrush);

        int y = padding;
        foreach (OverlayLine line in document.Lines)
        {
            uint color = line.Role switch
            {
                OverlayLineRole.Title => palette.Accent,
                OverlayLineRole.Separator => palette.Border,
                OverlayLineRole.Status => palette.Muted,
                _ => palette.Text,
            };
            if (Win32.SetTextColor(_memoryDc, color) == uint.MaxValue)
            {
                Win32.SelectObject(_memoryDc, previousFont);
                return false;
            }
            Win32.Rect lineRect = new()
            {
                Left = padding,
                Top = y,
                Right = width - padding,
                Bottom = y + textSize.Height,
            };
            Win32.DrawText(
                _memoryDc,
                line.Text,
                line.Text.Length,
                ref lineRect,
                Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
            y += lineHeight;
        }
        Win32.SelectObject(_memoryDc, previousFont);

        ApplyPremultipliedAlpha(width, height, presentation.OpacityPercent);
        CalculatePosition(targetRect, width, height, presentation, dpi, out destinationX, out destinationY);

        nint screenDc = Win32.GetDc(0);
        if (screenDc == 0)
        {
            return false;
        }

        try
        {
            Win32.Point destination = new(destinationX, destinationY);
            Win32.Size size = new(width, height);
            Win32.Point source = new(0, 0);
            Win32.BlendFunction blend = new()
            {
                BlendOp = Win32.AC_SRC_OVER,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AC_SRC_ALPHA,
            };
            return Win32.UpdateLayeredWindow(
                _window,
                screenDc,
                &destination,
                &size,
                _memoryDc,
                &source,
                0,
                &blend,
                Win32.ULW_ALPHA);
        }
        finally
        {
            if (Win32.ReleaseDc(0, screenDc) == 0)
            {
                Logger.Warning("Unable to release the screen DC after drawing the native overlay.");
            }
        }
    }

    private void ApplyPremultipliedAlpha(int width, int height, int opacityPercent)
    {
        uint alpha = (uint)Math.Clamp((opacityPercent * 255 + 50) / 100, 1, 255);
        uint* pixels = (uint*)_bits;
        int count = checked(width * height);
        for (int index = 0; index < count; index++)
        {
            uint pixel = pixels[index];
            uint color = pixel & 0x00FFFFFFU;
            if (color == 0)
            {
                pixels[index] = 0;
                continue;
            }

            uint blue = pixel & 0xFFU;
            uint green = (pixel >> 8) & 0xFFU;
            uint red = (pixel >> 16) & 0xFFU;
            blue = (blue * alpha + 127U) / 255U;
            green = (green * alpha + 127U) / 255U;
            red = (red * alpha + 127U) / 255U;
            pixels[index] = (alpha << 24) | (red << 16) | (green << 8) | blue;
        }
    }

    private static void CalculatePosition(
        Win32.Rect target,
        int width,
        int height,
        OverlayPresentationSettings presentation,
        uint dpi,
        out int x,
        out int y)
    {
        int margin = Scale(12, dpi);
        int left = target.Left + margin;
        int centerX = target.Left + (target.Width - width) / 2;
        int right = target.Right - width - margin;
        int top = target.Top + margin;
        int centerY = target.Top + (target.Height - height) / 2;
        int bottom = target.Bottom - height - margin;

        x = presentation.Anchor switch
        {
            OverlayAnchor.TopCenter or OverlayAnchor.Center or OverlayAnchor.BottomCenter => centerX,
            OverlayAnchor.TopRight or OverlayAnchor.CenterRight or OverlayAnchor.BottomRight => right,
            _ => left,
        };
        y = presentation.Anchor switch
        {
            OverlayAnchor.CenterLeft or OverlayAnchor.Center or OverlayAnchor.CenterRight => centerY,
            OverlayAnchor.BottomLeft or OverlayAnchor.BottomCenter or OverlayAnchor.BottomRight => bottom,
            _ => top,
        };

        x += Scale(presentation.OffsetX, dpi);
        y += Scale(presentation.OffsetY, dpi);
        if (width <= target.Width)
        {
            x = Math.Clamp(x, target.Left, target.Right - width);
        }
        if (height <= target.Height)
        {
            y = Math.Clamp(y, target.Top, target.Bottom - height);
        }
    }

    private static bool TryGetTargetClientRect(nint window, out Win32.Rect screenRect)
    {
        screenRect = default;
        if (Win32.GetClientRect(window, out Win32.Rect client) && client.Width > 0 && client.Height > 0)
        {
            Win32.Point origin = new(0, 0);
            if (Win32.ClientToScreen(window, ref origin))
            {
                screenRect = new Win32.Rect
                {
                    Left = origin.X,
                    Top = origin.Y,
                    Right = origin.X + client.Width,
                    Bottom = origin.Y + client.Height,
                };
                return true;
            }
        }
        return Win32.GetWindowRect(window, out screenRect) && screenRect.Width > 0 && screenRect.Height > 0;
    }

    private bool EnsureMemoryDc()
    {
        if (_memoryDc != 0)
        {
            return true;
        }
        nint screenDc = Win32.GetDc(0);
        if (screenDc == 0)
        {
            return false;
        }
        try
        {
            _memoryDc = Win32.CreateCompatibleDc(screenDc);
            return _memoryDc != 0;
        }
        finally
        {
            if (Win32.ReleaseDc(0, screenDc) == 0)
            {
                Logger.Warning("Unable to release the screen DC used to initialize the native overlay.");
            }
        }
    }

    private bool EnsureFont(int size, uint dpi)
    {
        if (_font != 0 && _fontSize == size && _fontDpi == dpi)
        {
            return true;
        }
        if (_font != 0)
        {
            UiTheme.DeleteOverlayFont(_font);
        }
        _font = UiTheme.CreateOverlayFont(size, dpi);
        _fontSize = size;
        _fontDpi = dpi;
        return _font != 0;
    }

    private bool EnsureBitmap(int width, int height)
    {
        if (_bitmap != 0 && _bitmapWidth == width && _bitmapHeight == height)
        {
            return true;
        }

        ReleaseBitmap();
        Win32.BitmapInfo info = new()
        {
            Header = new Win32.BitmapInfoHeader
            {
                Size = (uint)sizeof(Win32.BitmapInfoHeader),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = Win32.BI_RGB,
            },
        };
        _bitmap = Win32.CreateDibSection(_memoryDc, &info, Win32.DIB_RGB_COLORS, out _bits, 0, 0);
        if (_bitmap == 0 || _bits == 0)
        {
            ReleaseBitmap();
            return false;
        }

        nint previousBitmap = Win32.SelectObject(_memoryDc, _bitmap);
        if (previousBitmap == 0 || previousBitmap == unchecked((nint)(-1)))
        {
            Win32.DeleteObject(_bitmap);
            _bitmap = 0;
            _bits = 0;
            return false;
        }

        _previousBitmap = previousBitmap;
        _bitmapWidth = width;
        _bitmapHeight = height;
        return true;
    }

    private void ReleaseBitmap()
    {
        if (_bitmap != 0)
        {
            if (_memoryDc != 0 && _previousBitmap != 0)
            {
                Win32.SelectObject(_memoryDc, _previousBitmap);
            }
            Win32.DeleteObject(_bitmap);
        }
        _bitmap = 0;
        _previousBitmap = 0;
        _bits = 0;
        _bitmapWidth = 0;
        _bitmapHeight = 0;
    }

    private static OverlayPalette GetPalette(OverlayColorTheme theme) => theme switch
    {
        OverlayColorTheme.Amber => new OverlayPalette(
            Win32.Color(34, 26, 13), Win32.Color(101, 75, 27), Win32.Color(244, 235, 213),
            Win32.Color(255, 190, 65), Win32.Color(176, 153, 111)),
        _ => new OverlayPalette(
            Win32.Color(14, 19, 26), Win32.Color(53, 65, 77), Win32.Color(229, 237, 243),
            Win32.Color(94, 210, 224), Win32.Color(132, 147, 160)),
    };

    private void Hide()
    {
        if (_window != 0)
        {
            Win32.ShowWindow(_window, Win32.SW_HIDE);
        }
    }

    private void RegisterAndCreateWindow()
    {
        s_current = this;
        _classNamePointer = Marshal.StringToHGlobalUni(WindowClassName);
        Win32.WindowClassEx windowClass = new()
        {
            Size = (uint)sizeof(Win32.WindowClassEx),
            WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
            Instance = _instance,
            Cursor = Win32.LoadCursor(0, Win32.IDC_ARROW),
            ClassName = _classNamePointer,
        };
        if (Win32.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"Unable to register native overlay class. Win32={Marshal.GetLastWin32Error()}");
        }

        _window = Win32.CreateWindowEx(
            Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE |
            Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST,
            WindowClassName,
            string.Empty,
            Win32.WS_POPUP,
            0,
            0,
            1,
            1,
            0,
            0,
            _instance,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Unable to create native overlay window. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        NativeOverlayWindow? overlay = s_current;
        if (overlay is null)
        {
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        if (message == ApplyUpdateMessage)
        {
            overlay.ApplyPendingUpdate();
            return 0;
        }
        if (message == Win32.WM_NCHITTEST)
        {
            return Win32.HTTRANSPARENT;
        }
        if (message == Win32.WM_ERASEBKGND)
        {
            return 1;
        }
        if (message == Win32.WM_DESTROY && overlay._window == window)
        {
            overlay._window = 0;
            return 0;
        }
        return Win32.DefWindowProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _hasPending = false;
        }

        Hide();
        if (_window != 0)
        {
            Win32.DestroyWindow(_window);
            _window = 0;
        }
        ReleaseBitmap();
        if (_font != 0)
        {
            UiTheme.DeleteOverlayFont(_font);
            _font = 0;
        }
        if (_memoryDc != 0)
        {
            Win32.DeleteDc(_memoryDc);
            _memoryDc = 0;
        }
        if (_classNamePointer != 0)
        {
            Win32.UnregisterClass(WindowClassName, _instance);
            Marshal.FreeHGlobal(_classNamePointer);
            _classNamePointer = 0;
        }
        if (ReferenceEquals(s_current, this))
        {
            s_current = null;
        }
        GC.SuppressFinalize(this);
    }

    private static int Scale(int value, uint dpi) =>
        checked((int)Math.Round(value * Math.Max(96U, dpi) / 96.0));
}
