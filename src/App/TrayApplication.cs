using DisplayedLatencyMetrics.Capture;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Overlay;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayedLatencyMetrics.App;

internal sealed unsafe class TrayApplication : IDisposable
{
    private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.Tray";
    private const uint TrayMessage = Win32.WM_APP + 17;
    private const uint ShowTrayMenuMessage = Win32.WM_APP + 18;
    private const uint TrayIconId = 1;
    private const int ToggleHotkeyId = 1;

    private const uint CommandReadme = 100;
    private const uint CommandMenu = 101;
    private const uint CommandExit = 102;

    private static TrayApplication? s_current;

    private readonly bool _openMenuOnStartup;
    private readonly Lock _traySync = new();
    private readonly AppSettings _settings = new();
    private CaptureCoordinator? _capture;
    private UiTheme? _theme;
    private NativeOverlayWindow? _overlay;
    private OverlaySettingsWindow? _settingsWindow;
    private ReadmeWindow? _readmeWindow;
    private nint _instance;
    private nint _window;
    private nint _icon;
    private bool _ownsIcon;
    private nint _classNamePointer;
    private uint _taskbarCreatedMessage;
    private bool _hotkeyRegistered;
    private OverlayHotkey _registeredHotkey = OverlayHotkey.Disabled;
    private bool _notifyVersion4;
    private bool _iconAdded;
    private bool _menuRequestPending;
    private bool _menuVisible;
    private Win32.Point _requestedMenuPoint;
    private bool _disposed;
    private string _status = "Starting";

    internal TrayApplication(bool openMenuOnStartup)
    {
        _openMenuOnStartup = openMenuOnStartup;
    }

    internal int Run()
    {
        if (!InitializeWindow())
        {
            return 1;
        }

        _theme = new UiTheme();
        _overlay = new NativeOverlayWindow(_theme, _instance);
        _settingsWindow = new OverlaySettingsWindow(
            _settings,
            _theme,
            OnSettingsChanged,
            TrySetToggleHotkey,
            ShowNotification,
            _instance,
            _icon,
            _window);
        _readmeWindow = new ReadmeWindow(_instance, _theme, _icon);

        AddTrayIcon();
        TrySetToggleHotkey(_settings.Snapshot().Hotkey);

        _capture = new CaptureCoordinator(_settings, _overlay, UpdateStatus, ShowNotification);

        if (_openMenuOnStartup)
        {
            _settingsWindow.Show();
        }
        _readmeWindow.Preload();

        _capture.Start();

        while (true)
        {
            int result = Win32.GetMessage(out Win32.Message message, 0, 0, 0);
            if (result == 0)
            {
                return 0;
            }
            if (result < 0)
            {
                Logger.Error($"GetMessage failed. Win32={Marshal.GetLastWin32Error()}");
                return 2;
            }

            Win32.TranslateMessage(ref message);
            Win32.DispatchMessage(ref message);
        }
    }

    private bool InitializeWindow()
    {
        s_current = this;
        _instance = Win32.GetModuleHandle(null);
        LoadApplicationIcon();
        _classNamePointer = Marshal.StringToHGlobalUni(WindowClassName);

        Win32.WindowClassEx windowClass = new()
        {
            Size = (uint)sizeof(Win32.WindowClassEx),
            WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
            Instance = _instance,
            Icon = _icon,
            SmallIcon = _icon,
            ClassName = _classNamePointer,
        };

        if (Win32.RegisterClassEx(ref windowClass) == 0)
        {
            Logger.Error($"RegisterClassEx failed. Win32={Marshal.GetLastWin32Error()}");
            return false;
        }

        _window = Win32.CreateWindowEx(
            0,
            WindowClassName,
            "DISPLAYED-LATENCY-METRICS",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            _instance,
            0);
        if (_window == 0)
        {
            Logger.Error($"CreateWindowEx failed. Win32={Marshal.GetLastWin32Error()}");
            return false;
        }

        _taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
        AllowLowerIntegrityMessage(TrayMessage, "tray callback");
        if (_taskbarCreatedMessage != 0)
        {
            AllowLowerIntegrityMessage(_taskbarCreatedMessage, "TaskbarCreated");
        }
        return true;
    }

    private void LoadApplicationIcon()
    {
        string? executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            nint largeIcon = 0;
            nint smallIcon = 0;
            if (Win32.ExtractIconEx(executablePath, 0, &largeIcon, &smallIcon, 1) != 0)
            {
                _icon = smallIcon != 0 ? smallIcon : largeIcon;
                _ownsIcon = _icon != 0;

                if (largeIcon != 0 && largeIcon != _icon)
                {
                    Win32.DestroyIcon(largeIcon);
                }
                if (smallIcon != 0 && smallIcon != _icon)
                {
                    Win32.DestroyIcon(smallIcon);
                }
                if (_icon != 0)
                {
                    return;
                }
            }
            else
            {
                if (largeIcon != 0)
                {
                    Win32.DestroyIcon(largeIcon);
                }
                if (smallIcon != 0)
                {
                    Win32.DestroyIcon(smallIcon);
                }
            }
        }

        _icon = Win32.LoadIcon(_instance, Win32.IDI_APPLICATION);
        if (_icon == 0)
        {
            Logger.Warning("Unable to load the embedded application icon; using the Windows default icon.");
            _icon = Win32.LoadIcon(0, Win32.IDI_APPLICATION);
        }
    }

    private void AllowLowerIntegrityMessage(uint message, string name)
    {
        if (!Win32.ChangeWindowMessageFilterEx(_window, message, Win32.MSGFLT_ALLOW, 0))
        {
            Logger.Warning(
                $"Unable to allow lower-integrity {name} message 0x{message:X}. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    private void AddTrayIcon()
    {
        lock (_traySync)
        {
            Win32.NotifyIconData data = CreateNotifyData(
                Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP);
            Win32.CopyToFixed(data.Tip, 128, BuildTooltipLocked());

            _iconAdded = Win32.ShellNotifyIcon(Win32.NIM_ADD, &data);
            if (_iconAdded)
            {
                data.TimeoutOrVersion = Win32.NOTIFYICON_VERSION_4;
                _notifyVersion4 = Win32.ShellNotifyIcon(Win32.NIM_SETVERSION, &data);
                if (!_notifyVersion4)
                {
                    Logger.Warning($"NIM_SETVERSION(4) failed. Win32={Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                _notifyVersion4 = false;
                Logger.Error($"Unable to add tray icon. Win32={Marshal.GetLastWin32Error()}");
            }
        }
    }

    private Win32.NotifyIconData CreateNotifyData(uint flags) => new()
    {
        Size = (uint)sizeof(Win32.NotifyIconData),
        Window = _window,
        Id = TrayIconId,
        Flags = flags,
        CallbackMessage = TrayMessage,
        Icon = _icon,
    };

    private void UpdateStatus(string status)
    {
        lock (_traySync)
        {
            _status = status;
            if (!_iconAdded || _window == 0)
            {
                return;
            }

            Win32.NotifyIconData data = CreateNotifyData(Win32.NIF_TIP);
            Win32.CopyToFixed(data.Tip, 128, BuildTooltipLocked());
            Win32.ShellNotifyIcon(Win32.NIM_MODIFY, &data);
        }
    }

    private string BuildTooltipLocked()
    {
        string value = $"DISPLAYED-LATENCY-METRICS\n{_status}";
        return value.Length <= 127 ? value : value[..127];
    }

    private void ShowNotification(string message, bool error)
    {
        lock (_traySync)
        {
            if (!_iconAdded || _window == 0)
            {
                return;
            }

            Win32.NotifyIconData data = CreateNotifyData(Win32.NIF_INFO);
            data.InfoFlags = error ? Win32.NIIF_ERROR : Win32.NIIF_INFO;
            Win32.CopyToFixed(data.InfoTitle, 64, "DISPLAYED-LATENCY-METRICS");
            Win32.CopyToFixed(data.Info, 256, message);
            Win32.ShellNotifyIcon(Win32.NIM_MODIFY, &data);
        }
    }

    private void RequestMenu(uint notification, nuint callbackPosition)
    {
        if (_menuVisible || _menuRequestPending || _window == 0)
        {
            return;
        }

        Win32.Point point;
        bool callbackContainsCoordinates = _notifyVersion4 &&
                                           (notification is Win32.NIN_SELECT or Win32.NIN_KEYSELECT ||
                                            notification is >= 0x0200 and <= 0x020D);
        if (callbackContainsCoordinates)
        {
            point = new Win32.Point(
                unchecked((short)Win32.LowWord(callbackPosition)),
                unchecked((short)Win32.HighWord(callbackPosition)));
            if (point.X == -1 && point.Y == -1 && !Win32.GetCursorPos(out point))
            {
                return;
            }
        }
        else if (!Win32.GetCursorPos(out point))
        {
            return;
        }

        _requestedMenuPoint = point;
        _menuRequestPending = true;
        if (!Win32.PostMessage(_window, ShowTrayMenuMessage, 0, 0))
        {
            _menuRequestPending = false;
            Logger.Error($"Unable to queue tray menu. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    private void ShowRequestedMenu()
    {
        _menuRequestPending = false;
        ShowMenu(_requestedMenuPoint);
    }

    private void ShowMenu(Win32.Point point)
    {
        if (_menuVisible || _window == 0)
        {
            return;
        }

        nint menu = Win32.CreatePopupMenu();
        if (menu == 0)
        {
            Logger.Error($"CreatePopupMenu failed. Win32={Marshal.GetLastWin32Error()}");
            return;
        }

        _menuVisible = true;
        try
        {
            if (!Win32.AppendMenu(menu, Win32.MF_STRING, CommandReadme, "README") ||
                !Win32.AppendMenu(menu, Win32.MF_STRING, CommandMenu, "MENU") ||
                !Win32.AppendMenu(menu, Win32.MF_STRING, CommandExit, "Exit"))
            {
                Logger.Error($"AppendMenu failed. Win32={Marshal.GetLastWin32Error()}");
                return;
            }

            Win32.SetForegroundWindow(_window);
            Win32.Point menuPoint = GetMenuPointInWorkArea(point);
            uint command = Win32.TrackPopupMenuEx(
                menu,
                Win32.TPM_RIGHTALIGN | Win32.TPM_BOTTOMALIGN | Win32.TPM_RIGHTBUTTON |
                Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY,
                menuPoint.X,
                menuPoint.Y,
                _window,
                0);
            Win32.PostMessage(_window, Win32.WM_NULL, 0, 0);

            Win32.NotifyIconData data = CreateNotifyData(0);
            Win32.ShellNotifyIcon(Win32.NIM_SETFOCUS, &data);
            HandleCommand(command);
        }
        finally
        {
            Win32.DestroyMenu(menu);
            _menuVisible = false;
        }
    }

    private Win32.Point GetMenuPointInWorkArea(Win32.Point point)
    {
        nint monitor = Win32.MonitorFromPoint(point, Win32.MONITOR_DEFAULTTONEAREST);
        Win32.MonitorInfo info = new()
        {
            Size = (uint)sizeof(Win32.MonitorInfo),
        };

        if (monitor == 0 || !Win32.GetMonitorInfo(monitor, ref info))
        {
            return point;
        }

        int menuWidth = _theme?.Scale(164) ?? 164;
        int menuHeight = _theme?.Scale(104) ?? 104;
        int x = Math.Clamp(point.X + ScaleTrayMenuGap(), info.Work.Left + menuWidth, info.Work.Right);
        int y = Math.Clamp(point.Y - ScaleTrayMenuGap(), info.Work.Top + menuHeight, info.Work.Bottom);

        return new Win32.Point { X = x, Y = y };
    }

    private int ScaleTrayMenuGap() => _theme?.Scale(4) ?? 4;

    private void HandleCommand(uint command)
    {
        switch (command)
        {
            case CommandReadme:
                _readmeWindow?.Show();
                break;

            case CommandMenu:
                _settingsWindow?.Show();
                break;

            case CommandExit:
                if (_window != 0)
                {
                    Win32.DestroyWindow(_window);
                }
                break;
        }
    }

    private void ToggleOverlay()
    {
        bool enabled = _settings.ToggleOverlay();
        if (!enabled)
        {
            _overlay?.Clear();
            RtssOverlayPublisher.Clear();
        }
        _capture?.RequestRefresh();
        UpdateStatus(enabled ? "Overlay enabled" : "Overlay disabled");
    }

    private bool TrySetToggleHotkey(OverlayHotkey requestedHotkey)
    {
        if (_window == 0)
        {
            return false;
        }

        if (!requestedHotkey.IsEnabled)
        {
            if (_hotkeyRegistered)
            {
                Win32.UnregisterHotKey(_window, ToggleHotkeyId);
                _hotkeyRegistered = false;
            }
            _registeredHotkey = OverlayHotkey.Disabled;
            return true;
        }

        if (!OverlayHotkeyFormatter.IsValid(requestedHotkey))
        {
            return false;
        }

        if (_hotkeyRegistered && _registeredHotkey == requestedHotkey)
        {
            return true;
        }

        bool hadPreviousHotkey = _hotkeyRegistered;
        OverlayHotkey previousHotkey = _registeredHotkey;
        if (hadPreviousHotkey)
        {
            Win32.UnregisterHotKey(_window, ToggleHotkeyId);
            _hotkeyRegistered = false;
        }

        _hotkeyRegistered = Win32.RegisterHotKey(
            _window,
            ToggleHotkeyId,
            requestedHotkey.Modifiers | Win32.MOD_NOREPEAT,
            requestedHotkey.VirtualKey);
        if (_hotkeyRegistered)
        {
            _registeredHotkey = requestedHotkey;
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        Logger.Warning($"RegisterHotKey({OverlayHotkeyFormatter.Display(requestedHotkey)}) failed. Win32={error}");
        if (hadPreviousHotkey)
        {
            _hotkeyRegistered = Win32.RegisterHotKey(
                _window,
                ToggleHotkeyId,
                previousHotkey.Modifiers | Win32.MOD_NOREPEAT,
                previousHotkey.VirtualKey);
            if (_hotkeyRegistered)
            {
                _registeredHotkey = previousHotkey;
            }
        }

        ShowNotification(
            $"{OverlayHotkeyFormatter.Display(requestedHotkey)} уже занята другим приложением.",
            true);
        return false;
    }

    private void OnSettingsChanged()
    {
        SettingsSnapshot snapshot = _settings.Snapshot();
        if (!snapshot.OverlayEnabled)
        {
            _overlay?.Clear();
            RtssOverlayPublisher.Clear();
        }
        else if (snapshot.OutputMode == OverlayOutputMode.NativeWindow)
        {
            RtssOverlayPublisher.Clear();
        }
        else if (snapshot.OutputMode == OverlayOutputMode.Rtss)
        {
            _overlay?.Clear();
        }
        _capture?.RequestRefresh();
        UpdateStatus(snapshot.OverlayEnabled ? "Settings applied" : "Overlay disabled");
    }

    private void RemoveTrayIcon()
    {
        lock (_traySync)
        {
            if (!_iconAdded)
            {
                return;
            }

            Win32.NotifyIconData data = CreateNotifyData(0);
            Win32.ShellNotifyIcon(Win32.NIM_DELETE, &data);
            _iconAdded = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
    {
        TrayApplication? app = s_current;
        if (app is null)
        {
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        if (message == TrayMessage)
        {
            nuint callbackData = unchecked((nuint)lParam);
            uint notification = Win32.LowWord(callbackData);
            uint iconId = app._notifyVersion4
                ? Win32.HighWord(callbackData)
                : unchecked((uint)wParam);
            if (iconId != TrayIconId)
            {
                return 0;
            }
            if (notification is Win32.WM_RBUTTONUP or Win32.WM_LBUTTONUP or Win32.WM_CONTEXTMENU or
                Win32.NIN_SELECT or Win32.NIN_KEYSELECT)
            {
                app.RequestMenu(notification, wParam);
                return 0;
            }
        }
        else if (message == ShowTrayMenuMessage)
        {
            app.ShowRequestedMenu();
            return 0;
        }
        else if (message == app._taskbarCreatedMessage && message != 0)
        {
            app._iconAdded = false;
            app._notifyVersion4 = false;
            app.AddTrayIcon();
            return 0;
        }
        else if (message == Win32.WM_HOTKEY && unchecked((int)wParam) == ToggleHotkeyId)
        {
            app.ToggleOverlay();
            return 0;
        }
        else if (message == Win32.WM_COMMAND)
        {
            app.HandleCommand(Win32.LowWord(wParam));
            return 0;
        }
        else if (message == Win32.WM_DESTROY)
        {
            if (app._hotkeyRegistered)
            {
                Win32.UnregisterHotKey(window, ToggleHotkeyId);
                app._hotkeyRegistered = false;
            }
            app.RemoveTrayIcon();
            app._window = 0;
            Win32.PostQuitMessage(0);
            return 0;
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

        _capture?.Dispose();
        _capture = null;
        _overlay?.Clear();
        RtssOverlayPublisher.Clear();
        _settingsWindow?.Dispose();
        _settingsWindow = null;
        _readmeWindow?.Dispose();
        _readmeWindow = null;
        _overlay?.Dispose();
        _overlay = null;
        RemoveTrayIcon();

        if (_window != 0)
        {
            if (_hotkeyRegistered)
            {
                Win32.UnregisterHotKey(_window, ToggleHotkeyId);
                _hotkeyRegistered = false;
            }
            Win32.DestroyWindow(_window);
            _window = 0;
        }
        if (_instance != 0)
        {
            Win32.UnregisterClass(WindowClassName, _instance);
        }
        if (_classNamePointer != 0)
        {
            Marshal.FreeHGlobal(_classNamePointer);
            _classNamePointer = 0;
        }
        if (_ownsIcon && _icon != 0)
        {
            Win32.DestroyIcon(_icon);
            _icon = 0;
            _ownsIcon = false;
        }

        _theme?.Dispose();
        _theme = null;
        s_current = null;
        GC.SuppressFinalize(this);
    }
}
