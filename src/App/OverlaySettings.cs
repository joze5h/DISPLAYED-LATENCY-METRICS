using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.App;

internal enum OverlayLayoutMode
{
    Standard = 0,
    Compact = 1,
    TwoColumns = 2,
    SingleLine = 3,
}

internal enum OverlayAnchor
{
    TopLeft = 0,
    TopCenter = 1,
    TopRight = 2,
    CenterLeft = 3,
    Center = 4,
    CenterRight = 5,
    BottomLeft = 6,
    BottomCenter = 7,
    BottomRight = 8,
}

internal enum OverlayColorTheme
{
    Dark = 0,
    Amber = 1,
}

internal enum OverlayOutputMode
{
    NativeWindow = 0,
    Rtss = 1,
}

internal readonly record struct OverlayHotkey(bool IsEnabled, uint Modifiers, uint VirtualKey)
{
    internal static OverlayHotkey Disabled => new(false, 0, 0);
}

internal static class OverlayHotkeyFormatter
{
    private const uint VirtualKeyF1 = 0x70;
    private const uint VirtualKeyF24 = 0x87;

    internal static bool IsValid(OverlayHotkey hotkey) =>
        !hotkey.IsEnabled ||
        (hotkey.VirtualKey is > 0 and <= 0xFE &&
         !IsModifierKey(hotkey.VirtualKey) &&
         (hotkey.Modifiers != 0 || hotkey.VirtualKey is >= VirtualKeyF1 and <= VirtualKeyF24));

    internal static bool IsModifierKey(uint virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    internal static string Display(OverlayHotkey hotkey)
    {
        if (!hotkey.IsEnabled)
        {
            return "НЕ НАЗНАЧЕНА";
        }

        var parts = new List<string>(5);
        if ((hotkey.Modifiers & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((hotkey.Modifiers & Win32.MOD_ALT) != 0) parts.Add("Alt");
        if ((hotkey.Modifiers & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((hotkey.Modifiers & Win32.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName(hotkey.VirtualKey));
        return string.Join('+', parts);
    }

    private static string KeyName(uint virtualKey) => virtualKey switch
    {
        >= VirtualKeyF1 and <= VirtualKeyF24 => $"F{virtualKey - VirtualKeyF1 + 1}",
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "PrintScreen",
        0x2D => "Insert",
        0x2E => "Delete",
        _ => $"VK-{virtualKey:X2}",
    };
}

internal readonly record struct OverlayPresentationSettings(
    OverlayLayoutMode LayoutMode,
    OverlayAnchor Anchor,
    OverlayColorTheme ColorTheme,
    int OffsetX,
    int OffsetY,
    int DecimalPlaces,
    int FontSize,
    int OpacityPercent,
    int Padding,
    int RefreshIntervalMilliseconds,
    bool ShowTitle,
    bool ShowSeparators,
    bool ShowUnits)
{
    internal static OverlayPresentationSettings Default => new(
        OverlayLayoutMode.Standard,
        OverlayAnchor.TopLeft,
        OverlayColorTheme.Dark,
        OffsetX: 0,
        OffsetY: 0,
        DecimalPlaces: 3,
        FontSize: 14,
        OpacityPercent: 100,
        Padding: 12,
        RefreshIntervalMilliseconds: 1000,
        ShowTitle: true,
        ShowSeparators: true,
        ShowUnits: true);
}

internal readonly record struct SettingsSnapshot(
    bool OverlayEnabled,
    OverlayOutputMode OutputMode,
    OverlayHotkey Hotkey,
    MetricFlags EnabledMetrics,
    UInt128 MetricOrder,
    OverlayPresentationSettings Presentation);
