using DirectN;
using DirectN.Extensions.Com;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;
using DisplayedLatencyMetrics.Overlay;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WebView2;
using WebView2.Utilities;

namespace DisplayedLatencyMetrics.App;

internal sealed class OverlaySettingsWindow : IDisposable
{
    private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.HtmlMenu";
    private const string HtmlResourceName = "DisplayedLatencyMetrics.Assets.menu.html";
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmSetFocus = 0x0007;
    private const uint WmInitializeWebView = Win32.WM_APP + 71;
    private const uint WmAttachWebView = Win32.WM_APP + 72;
    private const uint WmRepairWebView = Win32.WM_APP + 73;

    private static OverlaySettingsWindow? s_current;

    private readonly AppSettings _settings;
    private readonly UiTheme _theme;
    private readonly Action _settingsChanged;
    private readonly Func<OverlayHotkey, bool> _trySetHotkey;
    private readonly Action<string, bool> _notification;
    private readonly nint _instance;
    private readonly nint _icon;
    private readonly nint _ownerWindow;
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
    private bool _initializationStarted;
    private bool _classRegistered;
    private bool _disposed;

    internal OverlaySettingsWindow(
        AppSettings settings,
        UiTheme theme,
        Action settingsChanged,
        Func<OverlayHotkey, bool> trySetHotkey,
        Action<string, bool> notification,
        nint instance,
        nint icon,
        nint ownerWindow)
    {
        _settings = settings;
        _theme = theme;
        _settingsChanged = settingsChanged;
        _trySetHotkey = trySetHotkey;
        _notification = notification;
        _instance = instance;
        _icon = icon;
        _ownerWindow = ownerWindow;
        _html = ReadEmbeddedHtml();
        RegisterWindowClass();
    }

    internal void Show()
    {
        if (_disposed)
        {
            return;
        }

        if (_window == 0 && !CreateWindow())
        {
            _notification("Не удалось открыть MENU.", true);
            return;
        }

        if (_controller is not null)
        {
            RevealWindow();
            ResizeWebView();
            MoveWebViewFocus();
            if (_pageReady)
            {
                SendState(_settings.Snapshot(), "state");
                ReplayEntranceAnimation();
            }
        }
        else
        {
            StartInitialization();
        }
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
            Logger.Warning($"Unable to register HTML MENU window class. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    private bool CreateWindow()
    {
        nint anchor = _ownerWindow != 0 ? _ownerWindow : Win32.GetForegroundWindow();
        (int x, int y, int width, int height) = _theme.CenteredWindow(anchor, 1240, 900);
        _window = Win32.CreateWindowEx(
            Win32.WS_EX_APPWINDOW | Win32.WS_EX_CONTROLPARENT,
            WindowClassName,
            "MENU — DISPLAYED-LATENCY-METRICS",
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

        Logger.Error($"CreateWindowEx for HTML MENU failed. Win32={Marshal.GetLastWin32Error()}");
        return false;
    }

    private void CreateControls(nint window)
    {
        _window = window;
        UiTheme.ApplyTopLevelStyle(window);

        if (!Win32.PostMessage(window, WmInitializeWebView, 0, 0))
        {
            Logger.Warning($"Unable to queue HTML MENU WebView2 initialization. Win32={Marshal.GetLastWin32Error()}");
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
                throw new InvalidOperationException("Embedded HTML MENU resource is empty.");
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

                                    ReleaseWebViewController();
                                    _controller = new ComObject<ICoreWebView2Controller>(controller);
                                    ResizeWebView();

                                    controller.get_CoreWebView2(out var webView).ThrowOnError();
                                    _webView = new ComObject<ICoreWebView2>(webView);

                                    try
                                    {
                                        webView.get_Settings(out var webSettings).ThrowOnError();
                                        webSettings.put_IsStatusBarEnabled(false).ThrowOnError();
                                        webSettings.put_AreDefaultContextMenusEnabled(false).ThrowOnError();
                                        webSettings.put_AreDevToolsEnabled(false).ThrowOnError();
                                    }
                                    catch (Exception settingsException)
                                    {
                                        Logger.Warning("Unable to apply HTML MENU WebView2 settings.", settingsException);
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
                                    Logger.Error("Unable to create Native AOT WebView2 MENU controller", exception);
                                    ShowInitializationError(exception);
                                }
                            });

                        environment.CreateCoreWebView2Controller(_window, _controllerHandler).ThrowOnError();
                    }
                    catch (Exception exception)
                    {
                        Logger.Error("Unable to create Native AOT WebView2 MENU environment", exception);
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
            Logger.Error("Unable to initialize Native AOT WebView2 MENU", exception);
            ShowInitializationError(exception);
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
            Logger.Error("Unable to attach HTML MENU WebView2 to its visible window.", exception);
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
            JsonElement root = document.RootElement;
            if (!TryGetString(root, "action", out string action))
            {
                return;
            }

            switch (action)
            {
                case "ready":
                    _pageReady = true;
                    SendState(_settings.Snapshot(), "state");
                    RevealWindow();
                    Win32.PostMessage(_window, WmAttachWebView, 0, 0);
                    break;

                case "preview":
                    if (!TryGetObject(root, "draft", out JsonElement previewDraft))
                    {
                        SendToast("Некорректные параметры предпросмотра.", true);
                        return;
                    }

                    if (!TryReadDraft(previewDraft, out SettingsSnapshot previewSettings, out string previewError))
                    {
                        SendToast(string.IsNullOrEmpty(previewError)
                            ? "Некорректные параметры предпросмотра."
                            : previewError, true);
                        return;
                    }

                    SendPreview(previewSettings);
                    break;

                case "defaults":
                    SendState(DefaultSnapshot(), "draft");
                    break;

                case "apply":
                    if (!TryGetObject(root, "draft", out JsonElement applyDraft))
                    {
                        SendApplyResult(false, default, "Некорректные настройки.");
                        return;
                    }

                    if (!TryReadDraft(applyDraft, out SettingsSnapshot requested, out string applyError))
                    {
                        SendApplyResult(false, default, string.IsNullOrEmpty(applyError)
                            ? "Некорректные настройки."
                            : applyError);
                        return;
                    }

                    ApplySettings(requested);
                    break;
            }
        }
        catch (JsonException exception)
        {
            Logger.Warning("Unable to parse HTML MENU message.", exception);
            SendToast("MENU передал некорректный JSON.", true);
        }
        catch (Exception exception)
        {
            Logger.Error("HTML MENU bridge failed", exception);
            SendToast("Ошибка связи MENU с программой.", true);
        }
        finally
        {
            if (raw.Value != 0)
            {
                Marshal.FreeCoTaskMem(raw.Value);
            }
        }
    }

    private void ApplySettings(SettingsSnapshot requested)
    {
        if (!_trySetHotkey(requested.Hotkey))
        {
            SendApplyResult(false, default, "Не удалось зарегистрировать выбранную горячую клавишу.");
            return;
        }

        _settings.Apply(
            requested.OverlayEnabled,
            requested.OutputMode,
            requested.Hotkey,
            requested.EnabledMetrics,
            requested.MetricOrder,
            requested.Presentation);

        SettingsSnapshot applied = _settings.Snapshot();
        _settingsChanged();
        _notification("Настройки применены.", false);
        SendApplyResult(true, applied, "Настройки применены.");
    }

    private static SettingsSnapshot DefaultSnapshot() => new(
        OverlayEnabled: true,
        OutputMode: OverlayOutputMode.NativeWindow,
        Hotkey: OverlayHotkey.Disabled,
        EnabledMetrics: MetricFlags.All,
        MetricOrder: MetricRegistry.DefaultOrderPacked,
        Presentation: OverlayPresentationSettings.Default);

    private static bool TryReadDraft(
        JsonElement root,
        out SettingsSnapshot snapshot,
        out string error)
    {
        snapshot = default;
        error = string.Empty;
        SettingsSnapshot defaults = DefaultSnapshot();

        bool overlayEnabled = GetBoolean(root, "overlayEnabled", defaults.OverlayEnabled);
        int outputModeValue = GetInt32(root, "outputMode", (int)defaults.OutputMode);
        OverlayOutputMode outputMode = outputModeValue == (int)OverlayOutputMode.Rtss
            ? OverlayOutputMode.Rtss
            : OverlayOutputMode.NativeWindow;

        OverlayHotkey hotkey = OverlayHotkey.Disabled;
        if (TryGetObject(root, "hotkey", out JsonElement hotkeyElement))
        {
            bool enabled = GetBoolean(hotkeyElement, "enabled", false);
            uint modifiers = (uint)Math.Clamp(GetInt32(hotkeyElement, "modifiers", 0), 0, 15);
            modifiers &= Win32.MOD_ALT | Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_WIN;
            uint virtualKey = (uint)Math.Clamp(GetInt32(hotkeyElement, "virtualKey", 0), 0, 0xFE);
            hotkey = enabled ? new OverlayHotkey(true, modifiers, virtualKey) : OverlayHotkey.Disabled;
        }

        if (!OverlayHotkeyFormatter.IsValid(hotkey))
        {
            error = "Горячая клавиша должна быть F1–F24 либо сочетанием с Ctrl / Alt / Shift / Win.";
            return false;
        }

        MetricFlags enabledMetrics = MetricFlags.None;
        if (root.TryGetProperty("enabledMetricIds", out JsonElement enabledArray) &&
            enabledArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in enabledArray.EnumerateArray())
            {
                if (item.TryGetInt32(out int id) && TryMetricFromId(id, out MetricFlags flag))
                {
                    enabledMetrics |= flag;
                }
            }
        }
        else
        {
            enabledMetrics = defaults.EnabledMetrics;
        }
        enabledMetrics &= MetricFlags.All;

        Span<bool> seen = stackalloc bool[MetricRegistry.Count];
        Span<MetricFlags> order = stackalloc MetricFlags[MetricRegistry.Count];
        int orderCount = 0;
        if (root.TryGetProperty("metricOrderIds", out JsonElement orderArray) &&
            orderArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in orderArray.EnumerateArray())
            {
                if (!item.TryGetInt32(out int id) ||
                    !TryMetricFromId(id, out MetricFlags flag) ||
                    seen[id])
                {
                    continue;
                }

                seen[id] = true;
                order[orderCount++] = flag;
            }
        }

        for (int id = 0; id < MetricRegistry.Count; id++)
        {
            if (!seen[id] && TryMetricFromId(id, out MetricFlags flag))
            {
                seen[id] = true;
                order[orderCount++] = flag;
            }
        }

        UInt128 metricOrder = MetricRegistry.NormalizeOrder(MetricRegistry.Pack(order));
        OverlayPresentationSettings defaultPresentation = defaults.Presentation;
        OverlayPresentationSettings presentation = defaultPresentation;
        if (TryGetObject(root, "presentation", out JsonElement p))
        {
            presentation = new OverlayPresentationSettings(
                LayoutMode: (OverlayLayoutMode)Math.Clamp(GetInt32(p, "layoutMode", (int)defaultPresentation.LayoutMode), 0, 3),
                Anchor: (OverlayAnchor)Math.Clamp(GetInt32(p, "anchor", (int)defaultPresentation.Anchor), 0, 8),
                ColorTheme: (OverlayColorTheme)Math.Clamp(GetInt32(p, "colorTheme", (int)defaultPresentation.ColorTheme), 0, 1),
                OffsetX: Math.Clamp(GetInt32(p, "offsetX", defaultPresentation.OffsetX), -2000, 2000),
                OffsetY: Math.Clamp(GetInt32(p, "offsetY", defaultPresentation.OffsetY), -2000, 2000),
                DecimalPlaces: Math.Clamp(GetInt32(p, "decimalPlaces", defaultPresentation.DecimalPlaces), 1, 3),
                FontSize: Math.Clamp(GetInt32(p, "fontSize", defaultPresentation.FontSize), 10, 32),
                OpacityPercent: Math.Clamp(GetInt32(p, "opacityPercent", defaultPresentation.OpacityPercent), 35, 100),
                Padding: Math.Clamp(GetInt32(p, "padding", defaultPresentation.Padding), 4, 32),
                RefreshIntervalMilliseconds: NormalizeRefresh(
                    GetInt32(p, "refreshIntervalMilliseconds", defaultPresentation.RefreshIntervalMilliseconds)),
                ShowTitle: GetBoolean(p, "showTitle", defaultPresentation.ShowTitle),
                ShowSeparators: GetBoolean(p, "showSeparators", defaultPresentation.ShowSeparators),
                ShowUnits: GetBoolean(p, "showUnits", defaultPresentation.ShowUnits));
        }

        snapshot = new SettingsSnapshot(
            overlayEnabled,
            outputMode,
            hotkey,
            enabledMetrics,
            metricOrder,
            presentation);
        return true;
    }

    private static int NormalizeRefresh(int value) => value switch
    {
        <= 375 => 250,
        <= 750 => 500,
        <= 1500 => 1000,
        _ => 2000,
    };

    private static bool TryMetricFromId(int id, out MetricFlags flag)
    {
        if ((uint)id < MetricRegistry.Count)
        {
            flag = MetricRegistry.DefaultOrder[id].Flag;
            return true;
        }

        flag = MetricFlags.None;
        return false;
    }

    private static int MetricId(MetricFlags flag)
    {
        for (int id = 0; id < MetricRegistry.Count; id++)
        {
            if (MetricRegistry.DefaultOrder[id].Flag == flag)
            {
                return id;
            }
        }
        return -1;
    }

    private static bool TryGetObject(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return value.Length != 0;
    }

    private static int GetInt32(JsonElement root, string propertyName, int fallback)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out int value))
        {
            return value;
        }
        return fallback;
    }

    private static bool GetBoolean(JsonElement root, string propertyName, bool fallback)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    private void SendState(SettingsSnapshot snapshot, string messageType)
    {
        PostJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", messageType);
            WriteSettings(writer, snapshot);
            writer.WritePropertyName("metrics");
            writer.WriteStartArray();
            for (int id = 0; id < MetricRegistry.Count; id++)
            {
                MetricDescriptor descriptor = MetricRegistry.DefaultOrder[id];
                writer.WriteStartObject();
                writer.WriteNumber("id", id);
                writer.WriteString("name", descriptor.Name);
                writer.WriteString("group", LocalizedMetricGroup(descriptor.Group));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("outputInfo", OutputInformation(snapshot.OutputMode));
            WritePreview(writer, snapshot);
            writer.WriteEndObject();
        });
    }

    private void SendPreview(SettingsSnapshot snapshot)
    {
        PostJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "preview");
            writer.WriteString("outputInfo", OutputInformation(snapshot.OutputMode));
            WritePreview(writer, snapshot);
            writer.WriteEndObject();
        });
    }

    private void SendToast(string message, bool error)
    {
        PostJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "toast");
            writer.WriteString("message", message);
            writer.WriteBoolean("error", error);
            writer.WriteEndObject();
        });
    }

    private void SendApplyResult(bool success, SettingsSnapshot snapshot, string message)
    {
        PostJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "applyResult");
            writer.WriteBoolean("success", success);
            writer.WriteString("message", message);
            if (success)
            {
                WriteSettings(writer, snapshot);
                writer.WriteString("outputInfo", OutputInformation(snapshot.OutputMode));
                WritePreview(writer, snapshot);
            }
            writer.WriteEndObject();
        });
    }

    private static void WriteSettings(Utf8JsonWriter writer, SettingsSnapshot snapshot)
    {
        writer.WritePropertyName("settings");
        writer.WriteStartObject();
        writer.WriteBoolean("overlayEnabled", snapshot.OverlayEnabled);
        writer.WriteNumber("outputMode", (int)snapshot.OutputMode);

        writer.WritePropertyName("hotkey");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", snapshot.Hotkey.IsEnabled);
        writer.WriteNumber("modifiers", snapshot.Hotkey.Modifiers);
        writer.WriteNumber("virtualKey", snapshot.Hotkey.VirtualKey);
        writer.WriteString("display", OverlayHotkeyFormatter.Display(snapshot.Hotkey));
        writer.WriteEndObject();

        writer.WritePropertyName("enabledMetricIds");
        writer.WriteStartArray();
        for (int id = 0; id < MetricRegistry.Count; id++)
        {
            MetricFlags flag = MetricRegistry.DefaultOrder[id].Flag;
            if ((snapshot.EnabledMetrics & flag) != 0)
            {
                writer.WriteNumberValue(id);
            }
        }
        writer.WriteEndArray();

        writer.WritePropertyName("metricOrderIds");
        writer.WriteStartArray();
        UInt128 normalizedOrder = MetricRegistry.NormalizeOrder(snapshot.MetricOrder);
        for (int index = 0; index < MetricRegistry.Count; index++)
        {
            int id = MetricId(MetricRegistry.At(normalizedOrder, index));
            if (id >= 0)
            {
                writer.WriteNumberValue(id);
            }
        }
        writer.WriteEndArray();

        OverlayPresentationSettings p = snapshot.Presentation;
        writer.WritePropertyName("presentation");
        writer.WriteStartObject();
        writer.WriteNumber("layoutMode", (int)p.LayoutMode);
        writer.WriteNumber("anchor", (int)p.Anchor);
        writer.WriteNumber("colorTheme", (int)p.ColorTheme);
        writer.WriteNumber("offsetX", p.OffsetX);
        writer.WriteNumber("offsetY", p.OffsetY);
        writer.WriteNumber("decimalPlaces", p.DecimalPlaces);
        writer.WriteNumber("fontSize", p.FontSize);
        writer.WriteNumber("opacityPercent", p.OpacityPercent);
        writer.WriteNumber("padding", p.Padding);
        writer.WriteNumber("refreshIntervalMilliseconds", p.RefreshIntervalMilliseconds);
        writer.WriteBoolean("showTitle", p.ShowTitle);
        writer.WriteBoolean("showSeparators", p.ShowSeparators);
        writer.WriteBoolean("showUnits", p.ShowUnits);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WritePreview(Utf8JsonWriter writer, SettingsSnapshot snapshot)
    {
        OverlayDocument preview = OverlayFormatter.BuildPreview(
            snapshot.Presentation,
            snapshot.EnabledMetrics,
            snapshot.MetricOrder,
            snapshot.OutputMode);

        writer.WritePropertyName("previewLines");
        writer.WriteStartArray();
        foreach (OverlayLine line in preview.Lines)
        {
            writer.WriteStartObject();
            writer.WriteString("text", line.Text);
            writer.WriteString("role", OverlayRoleName(line.Role));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static string OutputInformation(OverlayOutputMode mode) => mode == OverlayOutputMode.Rtss
        ? "НА ВЕСЬ ЭКРАН: программа передаёт рассчитанные метрики в RTSS, который рисует OSD внутри графического потока игры. " +
          "Внешний вид настраивается в RTSS. " + RtssOverlayPublisher.AvailabilityMessage()
        : "ПОЛНОЭКРАННЫЙ В ОКНЕ: программа отображает собственный оверлей поверх игры, " +
          "но игра должна быть запущена в режиме 'Полноэкранный в окне'.";

    private static string LocalizedMetricGroup(MetricGroup group) => group switch
    {
        MetricGroup.Presentation => "PRESENT",
        MetricGroup.Cpu => "CPU",
        MetricGroup.Render => "RENDER",
        MetricGroup.Display => "DISPLAY",
        MetricGroup.Analysis => "STUTTER",
        _ => "DISPLAY",
    };

    private void PostJson(Action<Utf8JsonWriter> write)
    {
        if (_disposed || !_pageReady || _webView is null)
        {
            return;
        }

        try
        {
            var buffer = new ArrayBufferWriter<byte>(4096);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                write(writer);
                writer.Flush();
            }

            string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
            _webView.Object.PostWebMessageAsJson(PWSTR.From(json)).ThrowOnError();
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to post message to HTML MENU", exception);
        }
    }

    private void ReplayEntranceAnimation() => PostJson(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("type", "windowShown");
        writer.WriteEndObject();
    });

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
            Logger.Warning("Unable to move focus to HTML MENU WebView2.", exception);
        }
    }

    private void ShowInitializationError(Exception exception)
    {
        if (_disposed || _window == 0)
        {
            return;
        }

        if (!_automaticRepairAttempted)
        {
            _automaticRepairAttempted = true;
            Logger.Warning("HTML MENU WebView2 initialization failed; starting automatic WebView2 repair.", exception);
            Win32.ShowWindow(_window, Win32.SW_HIDE);
            if (!Win32.PostMessage(_window, WmRepairWebView, 0, 0))
            {
                Logger.Error($"Unable to queue MENU WebView2 repair. Win32={Marshal.GetLastWin32Error()}");
                Win32.DestroyWindow(_window);
            }
            return;
        }

        Logger.Error("HTML MENU initialization failed again after automatic WebView2 repair.", exception);
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
            Logger.Warning("Unable to resize HTML MENU WebView2.", exception);
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
        EmbeddedWebAssets.ReadHtml(HtmlResourceName, "MENU");

    private static string OverlayRoleName(OverlayLineRole role) => role switch
    {
        OverlayLineRole.Title => "title",
        OverlayLineRole.Separator => "separator",
        OverlayLineRole.Status => "status",
        _ => "metric",
    };

    private void ReleaseWebViewController()
    {
        if (_webMessageRegistered && _webView is not null)
        {
            try
            {
                _webView.Object.remove_WebMessageReceived(_webMessageToken).ThrowOnError();
            }
            catch (Exception exception)
            {
                Logger.Warning("Unable to remove HTML MENU WebView2 message handler.", exception);
            }
        }

        _webMessageRegistered = false;
        _pageReady = false;
        _webMessageHandler = null;
        _webView?.Dispose();
        _webView = null;
        _controller?.Dispose();
        _controller = null;
    }

    private void CloseWebView()
    {
        ReleaseWebViewController();
        _controllerHandler = null;
        _environmentHandler = null;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        OverlaySettingsWindow? app = s_current;
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
                        limits->MinimumTrackSize.Y = app._theme.Scale(620);
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
                    }
                    return 0;
            }
        }
        catch (Exception exception)
        {
            Logger.Error("HTML MENU window procedure failed", exception);
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
