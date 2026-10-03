using System.Runtime.InteropServices;

namespace DisplayedLatencyMetrics.Interop;

internal static unsafe partial class Win32
{
    internal const uint WM_NULL = 0x0000;
    internal const uint WM_CREATE = 0x0001;
    internal const uint WM_DESTROY = 0x0002;
    internal const uint WM_SIZE = 0x0005;
    internal const uint WM_PAINT = 0x000F;
    internal const uint WM_TIMER = 0x0113;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_ERASEBKGND = 0x0014;
    internal const uint WM_SETFONT = 0x0030;
    internal const uint WM_COMMAND = 0x0111;
    internal const uint WM_CTLCOLORSTATIC = 0x0138;
    internal const uint WM_NCHITTEST = 0x0084;
    internal const uint WM_CONTEXTMENU = 0x007B;
    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_RBUTTONUP = 0x0205;
    internal const uint WM_HOTKEY = 0x0312;
    internal const uint WM_DPICHANGED = 0x02E0;
    internal const uint NIN_SELECT = 0x0400;
    internal const uint NIN_KEYSELECT = 0x0401;
    internal const uint WM_APP = 0x8000;

    internal const uint COINIT_APARTMENTTHREADED = 0x2;
    internal const uint COINIT_DISABLE_OLE1DDE = 0x4;
    internal const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    internal const uint CS_VREDRAW = 0x0001;
    internal const uint CS_HREDRAW = 0x0002;

    internal const uint NIM_ADD = 0x00000000;
    internal const uint NIM_MODIFY = 0x00000001;
    internal const uint NIM_DELETE = 0x00000002;
    internal const uint NIM_SETFOCUS = 0x00000003;
    internal const uint NIM_SETVERSION = 0x00000004;
    internal const uint NOTIFYICON_VERSION_4 = 4;

    internal const uint NIF_MESSAGE = 0x00000001;
    internal const uint NIF_ICON = 0x00000002;
    internal const uint NIF_TIP = 0x00000004;
    internal const uint NIF_INFO = 0x00000010;
    internal const uint NIF_SHOWTIP = 0x00000080;
    internal const uint NIIF_INFO = 0x00000001;
    internal const uint NIIF_ERROR = 0x00000003;

    internal const uint MF_STRING = 0x00000000;

    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_CAPTION = 0x00C00000;
    internal const uint WS_SYSMENU = 0x00080000;
    internal const uint WS_MINIMIZEBOX = 0x00020000;
    internal const uint WS_MAXIMIZEBOX = 0x00010000;
    internal const uint WS_THICKFRAME = 0x00040000;
    internal const uint WS_VISIBLE = 0x10000000;
    internal const uint WS_CHILD = 0x40000000;
    internal const uint WS_CLIPCHILDREN = 0x02000000;

    internal const uint WS_EX_TRANSPARENT = 0x00000020;
    internal const uint WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint WS_EX_TOPMOST = 0x00000008;
    internal const uint WS_EX_CONTROLPARENT = 0x00010000;
    internal const uint WS_EX_APPWINDOW = 0x00040000;
    internal const uint WS_EX_LAYERED = 0x00080000;
    internal const uint WS_EX_NOACTIVATE = 0x08000000;

    internal const uint TPM_RIGHTALIGN = 0x0008;
    internal const uint TPM_BOTTOMALIGN = 0x0020;
    internal const uint TPM_RIGHTBUTTON = 0x0002;
    internal const uint TPM_RETURNCMD = 0x0100;
    internal const uint TPM_NONOTIFY = 0x0080;

    internal const int SW_HIDE = 0;
    internal const int SW_SHOW = 5;
    internal const int SW_RESTORE = 9;
    internal const int SW_SHOWNOACTIVATE = 4;

    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_WIN = 0x0008;
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const uint MSGFLT_ALLOW = 1;

    internal const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
    internal const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
    internal const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;
    internal const uint FILE_MAP_ALL_ACCESS = 0x000F001F;

    internal const uint MB_OK = 0x00000000;
    internal const uint MB_ICONERROR = 0x00000010;
    internal const int DEFAULT_GUI_FONT = 17;
    internal const int DC_BRUSH = 18;
    internal const int DC_PEN = 19;
    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;
    internal const int TRANSPARENT = 1;
    internal const uint FW_NORMAL = 400;
    internal const uint FW_SEMIBOLD = 600;
    internal const uint DEFAULT_CHARSET = 1;
    internal const uint OUT_DEFAULT_PRECIS = 0;
    internal const uint CLIP_DEFAULT_PRECIS = 0;
    internal const uint CLEARTYPE_QUALITY = 5;
    internal const uint DEFAULT_PITCH = 0;
    internal const uint FF_DONTCARE = 0;

    internal const uint DT_LEFT = 0x00000000;
    internal const uint DT_VCENTER = 0x00000004;
    internal const uint DT_SINGLELINE = 0x00000020;
    internal const uint DT_NOPREFIX = 0x00000800;
    internal const uint DT_END_ELLIPSIS = 0x00008000;

    internal const uint BI_RGB = 0;
    internal const uint DIB_RGB_COLORS = 0;
    internal const byte AC_SRC_OVER = 0;
    internal const byte AC_SRC_ALPHA = 1;
    internal const uint ULW_ALPHA = 0x00000002;

    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    internal const int HTTRANSPARENT = -1;

    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_BORDER_COLOR = 34;
    internal const int DWMWA_CAPTION_COLOR = 35;
    internal const int DWMWA_TEXT_COLOR = 36;
    internal const int DWMWCP_DONOTROUND = 1;

    internal static readonly nint HWND_TOPMOST = (nint)(-1);
    internal static readonly nint IDI_APPLICATION = (nint)32512;
    internal static readonly nint IDC_ARROW = (nint)32512;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;

        internal Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        internal int Width;
        internal int Height;

        internal Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal readonly int Width => Right - Left;
        internal readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Hwnd;
        internal uint MessageId;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Point;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClassEx
    {
        internal uint Size;
        internal uint Style;
        internal nint WindowProcedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint BackgroundBrush;
        internal nint MenuName;
        internal nint ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal Rect Monitor;
        internal Rect Work;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PaintStruct
    {
        internal nint Dc;
        internal int Erase;
        internal Rect Paint;
        internal int Restore;
        internal int IncUpdate;
        internal fixed byte Reserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint Colors;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BlendFunction
    {
        internal byte BlendOp;
        internal byte BlendFlags;
        internal byte SourceConstantAlpha;
        internal byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GdiplusStartupInput
    {
        internal uint Version;
        internal nint DebugEventCallback;
        [MarshalAs(UnmanagedType.Bool)] internal bool SuppressBackgroundThread;
        [MarshalAs(UnmanagedType.Bool)] internal bool SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WinTrustFileInfo
    {
        internal uint Size;
        internal nint FilePath;
        internal nint FileHandle;
        internal nint KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WinTrustData
    {
        internal uint Size;
        internal nint PolicyCallbackData;
        internal nint SipClientData;
        internal uint UiChoice;
        internal uint RevocationChecks;
        internal uint UnionChoice;
        internal nint FileInfo;
        internal uint StateAction;
        internal nint StateData;
        internal nint UrlReference;
        internal uint ProviderFlags;
        internal uint UiContext;
        internal nint SignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GuidNative
    {
        internal uint Data1;
        internal ushort Data2;
        internal ushort Data3;
        internal fixed byte Data4[8];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        internal uint Size;
        internal nint Window;
        internal uint Id;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint Icon;
        internal fixed char Tip[128];
        internal uint State;
        internal uint StateMask;
        internal fixed char Info[256];
        internal uint TimeoutOrVersion;
        internal fixed char InfoTitle[64];
        internal uint InfoFlags;
        internal GuidNative GuidItem;
        internal nint BalloonIcon;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadLibraryEx(string fileName, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint GetProcAddress(nint module, string name);

    [LibraryImport("kernel32.dll", EntryPoint = "FreeLibrary", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeLibrary(nint module);

    [LibraryImport("kernel32.dll", EntryPoint = "OpenFileMappingW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint OpenFileMapping(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, string name);

    [LibraryImport("kernel32.dll", EntryPoint = "MapViewOfFile", SetLastError = true)]
    internal static partial nint MapViewOfFile(nint mapping, uint desiredAccess, uint offsetHigh, uint offsetLow, nuint numberOfBytesToMap);

    [LibraryImport("kernel32.dll", EntryPoint = "UnmapViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnmapViewOfFile(nint baseAddress);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("ole32.dll", EntryPoint = "CoInitializeEx")]
    internal static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll", EntryPoint = "CoUninitialize")]
    internal static partial void CoUninitialize();

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassEx(ref WindowClassEx windowClass);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterClass(string className, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll", EntryPoint = "UpdateWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowText(nint window, string text);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static partial nint SendMessage(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint window, out Rect rect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out Rect rect);

    [LibraryImport("user32.dll", EntryPoint = "ClientToScreen", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ClientToScreen(nint window, ref Point point);

    [LibraryImport("user32.dll", EntryPoint = "MoveWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveWindow(nint window, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "AdjustWindowRectExForDpi", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustWindowRectExForDpi(
        ref Rect rect,
        uint style,
        [MarshalAs(UnmanagedType.Bool)] bool hasMenu,
        uint extendedStyle,
        uint dpi);

    [LibraryImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")]
    internal static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    internal static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromWindow")]
    internal static partial nint MonitorFromWindow(nint window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    internal static partial nint MonitorFromPoint(Point point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll", EntryPoint = "EnableWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnableWindow(nint window, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport("user32.dll", EntryPoint = "SetFocus")]
    internal static partial nint SetFocus(nint window);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    internal static partial int GetMessage(out Message message, nint window, uint minimum, uint maximum);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(ref Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(ref Message message);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    internal static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll", EntryPoint = "ChangeWindowMessageFilterEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ChangeWindowMessageFilterEx(
        nint window,
        uint message,
        uint action,
        nint changeFilterStruct);

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    internal static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx", SetLastError = true)]
    internal static partial uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);

    [LibraryImport("user32.dll", EntryPoint = "DestroyMenu", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll", EntryPoint = "GetKeyState")]
    internal static partial short GetKeyState(int virtualKey);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    internal static partial nint LoadIcon(nint instance, nint iconName);

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint icon);

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint ExtractIconEx(
        string fileName,
        int iconIndex,
        nint* largeIcons,
        nint* smallIcons,
        uint iconCount);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    internal static partial nint LoadCursor(nint instance, nint cursorName);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBox(nint window, string text, string caption, uint type);

    [LibraryImport("user32.dll", EntryPoint = "InvalidateRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InvalidateRect(nint window, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [LibraryImport("user32.dll", EntryPoint = "RedrawWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RedrawWindow(nint window, nint updateRect, nint updateRegion, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "BeginPaint")]
    internal static partial nint BeginPaint(nint window, out PaintStruct paint);

    [LibraryImport("user32.dll", EntryPoint = "EndPaint")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EndPaint(nint window, ref PaintStruct paint);

    [LibraryImport("user32.dll", EntryPoint = "FillRect")]
    internal static partial int FillRect(nint dc, ref Rect rect, nint brush);

    [LibraryImport("user32.dll", EntryPoint = "FrameRect")]
    internal static partial int FrameRect(nint dc, ref Rect rect, nint brush);

    [LibraryImport("user32.dll", EntryPoint = "DrawTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DrawText(nint dc, string text, int count, ref Rect rect, uint format);

    [LibraryImport("user32.dll", EntryPoint = "GetDC")]
    internal static partial nint GetDc(nint window);

    [LibraryImport("user32.dll", EntryPoint = "ReleaseDC")]
    internal static partial int ReleaseDc(nint window, nint dc);

    [LibraryImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateLayeredWindow(
        nint window,
        nint destinationDc,
        Point* destination,
        Size* size,
        nint sourceDc,
        Point* source,
        uint colorKey,
        BlendFunction* blend,
        uint flags);

    [LibraryImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "SetTimer", SetLastError = true)]
    internal static partial nuint SetTimer(nint window, nuint id, uint elapsedMilliseconds, nint callback);

    [LibraryImport("user32.dll", EntryPoint = "KillTimer", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool KillTimer(nint window, nuint id);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShellNotifyIcon(uint message, NotifyIconData* data);

    [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust")]
    internal static partial int WinVerifyTrust(nint window, ref Guid action, ref WinTrustData data);

    [LibraryImport("gdi32.dll", EntryPoint = "GetStockObject")]
    internal static partial nint GetStockObject(int objectId);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateSolidBrush")]
    internal static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreatePen")]
    internal static partial nint CreatePen(int style, int width, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateFont(
        int height,
        int width,
        int escapement,
        int orientation,
        uint weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint objectHandle);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    internal static partial nint CreateCompatibleDc(nint dc);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDc(nint dc);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
    internal static partial nint CreateDibSection(
        nint dc,
        BitmapInfo* bitmapInfo,
        uint usage,
        out nint bits,
        nint section,
        uint offset);

    [LibraryImport("gdi32.dll", EntryPoint = "SelectObject")]
    internal static partial nint SelectObject(nint dc, nint objectHandle);

    [LibraryImport("gdi32.dll", EntryPoint = "SetTextColor")]
    internal static partial uint SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "SetBkColor")]
    internal static partial uint SetBkColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "SetBkMode")]
    internal static partial int SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll", EntryPoint = "SetDCBrushColor")]
    internal static partial uint SetDcBrushColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "SetDCPenColor")]
    internal static partial uint SetDcPenColor(nint dc, uint color);

    [LibraryImport("gdi32.dll", EntryPoint = "Rectangle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Rectangle(nint dc, int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTextExtentPoint32(nint dc, string text, int count, out Size size);

    [LibraryImport("gdi32.dll", EntryPoint = "AddFontMemResourceEx")]
    internal static partial nint AddFontMemResourceEx(void* font, uint size, nint reserved, out uint fontCount);

    [LibraryImport("gdi32.dll", EntryPoint = "RemoveFontMemResourceEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RemoveFontMemResourceEx(nint handle);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    internal static partial int DwmSetWindowAttribute(nint window, int attribute, void* value, uint valueSize);

    [LibraryImport("uxtheme.dll", EntryPoint = "SetWindowTheme", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetWindowTheme(nint window, string? subAppName, string? subIdList);

    [LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    internal static partial uint TimeBeginPeriod(uint periodMilliseconds);

    [LibraryImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    internal static partial uint TimeEndPeriod(uint periodMilliseconds);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdiplusStartup")]
    internal static partial int GdiplusStartup(out nint token, ref GdiplusStartupInput input, nint output);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdiplusShutdown")]
    internal static partial void GdiplusShutdown(nint token);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipCreateBitmapFromFile", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GdipCreateBitmapFromFile(string fileName, out nint bitmap);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipDisposeImage")]
    internal static partial int GdipDisposeImage(nint image);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipCreateFromHDC")]
    internal static partial int GdipCreateFromHDC(nint dc, out nint graphics);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipDeleteGraphics")]
    internal static partial int GdipDeleteGraphics(nint graphics);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipDrawImageRectI")]
    internal static partial int GdipDrawImageRectI(nint graphics, nint image, int x, int y, int width, int height);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipGetImageWidth")]
    internal static partial int GdipGetImageWidth(nint image, out uint width);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipGetImageHeight")]
    internal static partial int GdipGetImageHeight(nint image, out uint height);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipSetInterpolationMode")]
    internal static partial int GdipSetInterpolationMode(nint graphics, int interpolationMode);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipSetPixelOffsetMode")]
    internal static partial int GdipSetPixelOffsetMode(nint graphics, int pixelOffsetMode);

    [LibraryImport("gdiplus.dll", EntryPoint = "GdipDrawImageRectRect")]
    internal static partial int GdipDrawImageRectRect(
        nint graphics,
        nint image,
        float destinationX,
        float destinationY,
        float destinationWidth,
        float destinationHeight,
        float sourceX,
        float sourceY,
        float sourceWidth,
        float sourceHeight,
        int sourceUnit,
        nint imageAttributes,
        nint callback,
        nint callbackData);

    internal static void SetControlFont(nint control, nint font) =>
        SendMessage(control, WM_SETFONT, unchecked((nuint)font), 1);

    internal static uint Color(byte red, byte green, byte blue) =>
        red | ((uint)green << 8) | ((uint)blue << 16);

    internal static uint LowWord(nuint value) => (uint)(value & 0xFFFF);

    internal static uint HighWord(nuint value) => (uint)((value >> 16) & 0xFFFF);

    internal static void CopyToFixed(char* destination, int capacity, string text)
    {
        int count = Math.Min(capacity - 1, text.Length);
        for (int index = 0; index < count; index++)
        {
            destination[index] = text[index];
        }
        destination[count] = '\0';
        for (int index = count + 1; index < capacity; index++)
        {
            destination[index] = '\0';
        }
    }
}
