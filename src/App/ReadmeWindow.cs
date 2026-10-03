using DirectN;
using DirectN.Extensions.Com;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using WebView2;
using WebView2.Utilities;

namespace DisplayedLatencyMetrics.App;

internal sealed class ReadmeWindow : IDisposable
{
    private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.HtmlReadme";
    private const string HtmlResourceName = "DisplayedLatencyMetrics.Assets.readme.html";
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmSetFocus = 0x0007;
    private const uint WmInitializeWebView = Win32.WM_APP + 70;
    private const uint WmAttachWebView = Win32.WM_APP + 72;
    private const uint WmRepairWebView = Win32.WM_APP + 73;

    private static ReadmeWindow? s_current;

    private readonly nint _instance;
    private readonly UiTheme _theme;
    private readonly nint _icon;
    private readonly string _html;

    private nint _classNamePointer;
    private nint _window;
    private bool _automaticRepairAttempted;
    private ComObject<ICoreWebView2Controller>? _controller;
    private ComObject<ICoreWebView2>? _webView;
    private CoreWebView2CreateCoreWebView2EnvironmentCompletedHandler? _environmentHandler;
    private CoreWebView2CreateCoreWebView2ControllerCompletedHandler? _controllerHandler;
    private CoreWebView2WebMessageReceivedEventHandler? _webMessageHandler;
    private EventRegistrationToken _webMessageToken;
    private bool _webMessageRegistered;
    private bool _pageReady;
    private bool _showWhenReady;
    private bool _initializationStarted;
    private bool _classRegistered;
    private bool _disposed;

    internal ReadmeWindow(nint instance, UiTheme theme, nint icon)
    {
        _instance = instance;
        _theme = theme;
        _icon = icon;
        _html = ReadEmbeddedHtml();
        RegisterWindowClass();
    }

    internal void Show()
    {
        if (_disposed)
        {
            return;
        }

        _showWhenReady = true;
        if (_window == 0 && !CreateWindow())
        {
            Win32.MessageBox(
                0,
                "Не удалось открыть HTML README.",
                "DISPLAYED-LATENCY-METRICS",
                Win32.MB_OK | Win32.MB_ICONERROR);
            return;
        }

        if (_controller is not null)
        {
            RevealWindow();

            if (!Win32.PostMessage(_window, WmAttachWebView, 0, 0))
            {
                Logger.Warning($"Unable to attach preloaded README WebView2. Win32={Marshal.GetLastWin32Error()}");
                AttachWebView();
            }
            if (_pageReady)
            {
                ReplayEntranceAnimation();
            }
        }
        else
        {
            StartInitialization();
        }
    }

    internal void Preload()
    {
        if (_disposed || _controller is not null || _initializationStarted)
        {
            return;
        }

        _showWhenReady = false;
        if (_window == 0 && !CreateWindow())
        {
            Logger.Warning("Unable to create the hidden README preload window.");
            return;
        }
        StartInitialization();
    }

    private void RevealWindow()
    {
        if (_window == 0)
        {
            return;
        }
        Win32.ShowWindow(_window, Win32.SW_RESTORE);
        Win32.ShowWindow(_window, Win32.SW_SHOW);
        Win32.SetForegroundWindow(_window);
        Win32.UpdateWindow(_window);
    }

    private unsafe void RegisterWindowClass()
    {
        s_current = this;
        _classNamePointer = Marshal.StringToHGlobalUni(WindowClassName);
        Win32.WindowClassEx windowClass = new()
        {
            Size = (uint)sizeof(Win32.WindowClassEx),
            Style = Win32.CS_HREDRAW | Win32.CS_VREDRAW,
            WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
            Instance = _instance,
            Icon = _icon,
            SmallIcon = _icon,
            Cursor = Win32.LoadCursor(0, Win32.IDC_ARROW),
            BackgroundBrush = _theme.BackgroundBrush,
            ClassName = _classNamePointer,
        };

        _classRegistered = Win32.RegisterClassEx(ref windowClass) != 0;
        if (!_classRegistered)
        {
            Logger.Warning($"Unable to register HTML README window class. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    private bool CreateWindow()
    {
        nint anchor = Win32.GetForegroundWindow();
        (int x, int y, int width, int height) = _theme.CenteredWindow(anchor, 1180, 820);
        _window = Win32.CreateWindowEx(
            Win32.WS_EX_APPWINDOW | Win32.WS_EX_CONTROLPARENT,
            WindowClassName,
            "README — DISPLAYED-LATENCY-METRICS",
            Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_MINIMIZEBOX |
            Win32.WS_MAXIMIZEBOX | Win32.WS_THICKFRAME | Win32.WS_CLIPCHILDREN,
            x,
            y,
            width,
            height,
            0,
            0,
            _instance,
            0);

        if (_window != 0)
        {
            return true;
        }

        Logger.Error($"CreateWindowEx for HTML README failed. Win32={Marshal.GetLastWin32Error()}");
        return false;
    }

    private void CreateControls(nint window)
    {
        _window = window;
        UiTheme.ApplyTopLevelStyle(window);

        if (!Win32.PostMessage(window, WmInitializeWebView, 0, 0))
        {
            Logger.Warning($"Unable to queue WebView2 initialization. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    private void StartInitialization()
    {
        if (_initializationStarted || _window == 0 || _disposed)
        {
            return;
        }

        _initializationStarted = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_html))
            {
                throw new InvalidOperationException("Embedded HTML resource is empty.");
            }

            WebView2Utilities.Initialize(Assembly.GetEntryAssembly());

            string? browserVersion = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
            if (string.IsNullOrWhiteSpace(browserVersion))
            {
                throw new InvalidOperationException(
                    "Microsoft Edge WebView2 Runtime не найден. Установите Evergreen Runtime и перезапустите программу.");
            }

            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DISPLAYED-LATENCY-METRICS",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);

            _environmentHandler = new CoreWebView2CreateCoreWebView2EnvironmentCompletedHandler(
                (result, environment) =>
                {
                    try
                    {
                        result.ThrowOnError();
                        if (_disposed || _window == 0)
                        {
                            return;
                        }

                        _controllerHandler = new CoreWebView2CreateCoreWebView2ControllerCompletedHandler(
                            (controllerResult, controller) =>
                            {
                                try
                                {
                                    controllerResult.ThrowOnError();
                                    if (_disposed || _window == 0)
                                    {
                                        return;
                                    }

                                    _controller?.Dispose();
                                    _controller = new ComObject<ICoreWebView2Controller>(controller);

                                    ResizeWebView();

                                    controller.get_CoreWebView2(out var webView).ThrowOnError();
                                    _webView?.Dispose();
                                    _webView = new ComObject<ICoreWebView2>(webView);
                                    try
                                    {
                                        webView.get_Settings(out var settings).ThrowOnError();
                                        settings.put_IsStatusBarEnabled(false).ThrowOnError();
                                        settings.put_AreDefaultContextMenusEnabled(false).ThrowOnError();
                                        settings.put_AreDevToolsEnabled(false).ThrowOnError();
                                    }
                                    catch (Exception settingsException)
                                    {
                                        Logger.Warning("Unable to apply README WebView2 settings.", settingsException);
                                    }

                                    _webMessageHandler = new CoreWebView2WebMessageReceivedEventHandler(HandleWebMessage);
                                    _webMessageToken = default;
                                    webView.add_WebMessageReceived(_webMessageHandler, ref _webMessageToken).ThrowOnError();
                                    _webMessageRegistered = true;
                                    _pageReady = false;
                                    webView.NavigateToString(PWSTR.From(_html)).ThrowOnError();
                                }
                                catch (Exception exception)
                                {
                                    Logger.Error("Unable to create Native AOT WebView2 controller", exception);
                                    ShowInitializationError(exception);
                                }
                            });

                        environment.CreateCoreWebView2Controller(_window, _controllerHandler).ThrowOnError();
                    }
                    catch (Exception exception)
                    {
                        Logger.Error("Unable to create Native AOT WebView2 environment", exception);
                        ShowInitializationError(exception);
                    }
                });

            WebView2.Functions.CreateCoreWebView2EnvironmentWithOptions(
                PWSTR.Null,
                PWSTR.From(userDataFolder),
                null!,
                _environmentHandler).ThrowOnError();
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to initialize Native AOT WebView2 README", exception);
            ShowInitializationError(exception);
        }
    }

    private void HandleWebMessage(ICoreWebView2 sender, ICoreWebView2WebMessageReceivedEventArgs args)
    {
        _ = sender;
        PWSTR raw = default;
        try
        {
            args.get_WebMessageAsJson(out raw).ThrowOnError();
            string json = raw.Value == 0 ? string.Empty : Marshal.PtrToStringUni(raw.Value) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("action", out JsonElement action) ||
                !string.Equals(action.GetString(), "ready", StringComparison.Ordinal))
            {
                return;
            }

            _pageReady = true;
            if (_showWhenReady && _window != 0)
            {
                RevealWindow();
                Win32.PostMessage(_window, WmAttachWebView, 0, 0);
            }
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to process README WebView2 ready message.", exception);
            ShowInitializationError(exception);
        }
        finally
        {
            if (raw.Value != 0)
            {
                Marshal.FreeCoTaskMem(raw.Value);
            }
        }
    }

    private void AttachWebView()
    {
        if (_controller is null || _window == 0)
        {
            return;
        }

        try
        {
            _controller.Object.put_IsVisible(true).ThrowOnError();
            ResizeWebView();
            MoveWebViewFocus();
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to attach README WebView2 to its visible window.", exception);
            ShowInitializationError(exception);
        }
    }

    private void MoveWebViewFocus()
    {
        if (_controller is null)
        {
            return;
        }

        try
        {
            _controller.Object.MoveFocus(
                COREWEBVIEW2_MOVE_FOCUS_REASON.COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC).ThrowOnError();
        }
        catch (Exception exception)
        {
            Logger.Warning("Unable to move focus to README WebView2.", exception);
        }
    }

    private void ShowInitializationError(Exception exception)
    {
        if (_disposed || _window == 0)
        {
            return;
        }

        if (!_showWhenReady)
        {
            Logger.Warning("Hidden README preload failed; initialization will be retried when README is opened.", exception);
            Win32.DestroyWindow(_window);
            return;
        }

        if (!_automaticRepairAttempted)
        {
            _automaticRepairAttempted = true;
            Logger.Warning("HTML README WebView2 initialization failed; starting automatic WebView2 repair.", exception);
            Win32.ShowWindow(_window, Win32.SW_HIDE);
            if (!Win32.PostMessage(_window, WmRepairWebView, 0, 0))
            {
                Logger.Error($"Unable to queue README WebView2 repair. Win32={Marshal.GetLastWin32Error()}");
                Win32.DestroyWindow(_window);
            }
            return;
        }

        Logger.Error("HTML README initialization failed again after automatic WebView2 repair.", exception);
        Win32.DestroyWindow(_window);
    }

    private void RunAutomaticRepair()
    {
        CloseWebView();
        if (DependencyBootstrapper.RepairWebView2())
        {
            _initializationStarted = false;
            Show();
            return;
        }

        if (_window != 0)
        {
            Win32.DestroyWindow(_window);
        }
    }

    private void ResizeWebView()
    {
        if (_controller is null || _window == 0 || !Win32.GetClientRect(_window, out Win32.Rect client))
        {
            return;
        }

        try
        {
            var bounds = new RECT
            {
                left = 0,
                top = 0,
                right = Math.Max(1, client.Width),
                bottom = Math.Max(1, client.Height),
            };
            _controller.Object.put_Bounds(bounds).ThrowOnError();
        }
        catch (Exception exception)
        {
            Logger.Warning("Unable to resize README WebView2.", exception);
        }
    }

    private void PaintWindow(nint window)
    {
        nint dc = Win32.BeginPaint(window, out Win32.PaintStruct paint);
        if (dc != 0 && Win32.GetClientRect(window, out Win32.Rect client))
        {
            Win32.FillRect(dc, ref client, _theme.BackgroundBrush);
        }
        Win32.EndPaint(window, ref paint);
    }

    private static string ReadEmbeddedHtml() =>
        EmbeddedWebAssets.ReadHtml(HtmlResourceName, "README");

    private void CloseWebView()
    {
        if (_webMessageRegistered && _webView is not null)
        {
            try
            {
                _webView.Object.remove_WebMessageReceived(_webMessageToken).ThrowOnError();
            }
            catch (Exception exception)
            {
                Logger.Warning("Unable to remove README WebView2 message handler.", exception);
            }
        }
        _webMessageRegistered = false;
        _pageReady = false;
        _webMessageHandler = null;
        _webView?.Dispose();
        _webView = null;
        _controller?.Dispose();
        _controller = null;
        _controllerHandler = null;
        _environmentHandler = null;
    }

    private void ReplayEntranceAnimation()
    {
        if (_webView is null)
        {
            return;
        }

        try
        {
            _webView.Object.PostWebMessageAsJson(PWSTR.From("{\"type\":\"windowShown\"}")).ThrowOnError();
        }
        catch (Exception exception)
        {
            Logger.Warning("Unable to replay README entrance animation.", exception);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        ReadmeWindow? app = s_current;
        if (app is null)
        {
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        try
        {
            switch (message)
            {
                case Win32.WM_CREATE:
                    app.CreateControls(window);
                    return 0;

                case Win32.WM_SIZE:
                    app.ResizeWebView();
                    return 0;

                case WmInitializeWebView:
                    app.StartInitialization();
                    return 0;

                case WmAttachWebView:
                    app.AttachWebView();
                    return 0;

                case WmRepairWebView:
                    app.RunAutomaticRepair();
                    return 0;

                case WmSetFocus:
                    app.MoveWebViewFocus();
                    return 0;

                case WmGetMinMaxInfo:
                    if (lParam != 0)
                    {
                        MinMaxInfo* limits = (MinMaxInfo*)lParam;
                        limits->MinimumTrackSize.X = app._theme.Scale(820);
                        limits->MinimumTrackSize.Y = app._theme.Scale(580);
                    }
                    return 0;

                case Win32.WM_DPICHANGED:
                    if (lParam != 0)
                    {
                        Win32.Rect* suggested = (Win32.Rect*)lParam;
                        Win32.MoveWindow(
                            window,
                            suggested->Left,
                            suggested->Top,
                            suggested->Width,
                            suggested->Height,
                            true);
                    }
                    app.ResizeWebView();
                    return 0;

                case Win32.WM_PAINT:
                    app.PaintWindow(window);
                    return 0;

                case Win32.WM_ERASEBKGND:
                    return 1;

                case Win32.WM_CLOSE:
                    Win32.ShowWindow(window, Win32.SW_HIDE);
                    return 0;

                case Win32.WM_DESTROY:
                    app.CloseWebView();
                    if (app._window == window)
                    {
                        app._window = 0;
                        app._initializationStarted = false;
                        app._automaticRepairAttempted = false;
                        app._showWhenReady = false;
                    }
                    return 0;
            }
        }
        catch (Exception exception)
        {
            Logger.Error("HTML README window procedure failed", exception);
        }

        return Win32.DefWindowProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        CloseWebView();

        if (_window != 0)
        {
            Win32.DestroyWindow(_window);
            _window = 0;
        }

        if (_classRegistered)
        {
            Win32.UnregisterClass(WindowClassName, _instance);
            _classRegistered = false;
        }
        if (_classNamePointer != 0)
        {
            Marshal.FreeHGlobal(_classNamePointer);
            _classNamePointer = 0;
        }
        if (ReferenceEquals(s_current, this))
        {
            s_current = null;
        }

        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        internal Win32.Point Reserved;
        internal Win32.Point MaximumSize;
        internal Win32.Point MaximumPosition;
        internal Win32.Point MinimumTrackSize;
        internal Win32.Point MaximumTrackSize;
    }
}
