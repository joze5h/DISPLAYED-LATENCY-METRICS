using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;
using System.Globalization;

namespace DisplayedLatencyMetrics.App;

internal sealed class AppSettings
{

    private const int CurrentSettingsVersion = 14;

    private readonly Lock _sync = new();
    private readonly string _path;
    private bool _overlayEnabled = true;
    private OverlayOutputMode _outputMode = OverlayOutputMode.NativeWindow;
    private OverlayHotkey _hotkey = OverlayHotkey.Disabled;
    private MetricFlags _enabledMetrics = MetricFlags.All;
    private UInt128 _metricOrder = MetricRegistry.DefaultOrderPacked;
    private OverlayPresentationSettings _presentation = OverlayPresentationSettings.Default;

    public AppSettings()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DISPLAYED-LATENCY-METRICS");
        _path = Path.Combine(directory, "settings.ini");
        Load();
    }

    public SettingsSnapshot Snapshot()
    {
        lock (_sync)
        {
            return SnapshotLocked();
        }
    }

    public bool ToggleOverlay()
    {
        lock (_sync)
        {
            _overlayEnabled = !_overlayEnabled;
            SaveLocked();
            return _overlayEnabled;
        }
    }

    public void Apply(
        bool overlayEnabled,
        OverlayOutputMode outputMode,
        OverlayHotkey hotkey,
        MetricFlags enabledMetrics,
        UInt128 metricOrder,
        OverlayPresentationSettings presentation)
    {
        lock (_sync)
        {
            _overlayEnabled = overlayEnabled;
            _outputMode = Enum.IsDefined(outputMode) ? outputMode : OverlayOutputMode.NativeWindow;
            _hotkey = NormalizeHotkey(hotkey);
            _enabledMetrics = enabledMetrics & MetricFlags.All;
            _metricOrder = MetricRegistry.NormalizeOrder(metricOrder);
            _presentation = Normalize(presentation);
            SaveLocked();
        }
    }

    private SettingsSnapshot SnapshotLocked() =>
        new(_overlayEnabled, _outputMode, _hotkey, _enabledMetrics, _metricOrder, _presentation);

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            int settingsVersion = 1;
            MetricFlags? loadedMetrics = null;
            string? loadedMetricOrder = null;
            OverlayPresentationSettings presentation = _presentation;

            foreach (string rawLine in File.ReadLines(_path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();
                if (key.Equals("SettingsVersion", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                {
                    settingsVersion = Math.Max(1, version);
                }
                else if (key.Equals("OverlayEnabled", StringComparison.OrdinalIgnoreCase) &&
                         bool.TryParse(value, out bool overlayEnabled))
                {
                    _overlayEnabled = overlayEnabled;
                }
                else if (key.Equals("OutputMode", StringComparison.OrdinalIgnoreCase) &&
                         TryParseEnum(value, out OverlayOutputMode outputMode))
                {
                    _outputMode = outputMode;
                }
                else if (TryReadBool(key, value, "HotkeyEnabled", out bool hotkeyEnabled))
                {
                    _hotkey = _hotkey with { IsEnabled = hotkeyEnabled };
                }
                else if (TryReadUInt(key, value, "HotkeyModifiers", out uint hotkeyModifiers))
                {
                    _hotkey = _hotkey with { Modifiers = hotkeyModifiers };
                }
                else if (TryReadUInt(key, value, "HotkeyVirtualKey", out uint hotkeyVirtualKey))
                {
                    _hotkey = _hotkey with { VirtualKey = hotkeyVirtualKey };
                }
                else if (key.Equals("EnabledMetrics", StringComparison.OrdinalIgnoreCase) &&
                         ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong flags))
                {
                    loadedMetrics = (MetricFlags)flags & MetricFlags.All;
                }
                else if (key.Equals("MetricOrder", StringComparison.OrdinalIgnoreCase))
                {
                    loadedMetricOrder = value;
                }
                else if (key.Equals("LayoutMode", StringComparison.OrdinalIgnoreCase) &&
                         TryParseEnum(value, out OverlayLayoutMode layout))
                {
                    presentation = presentation with { LayoutMode = layout };
                }
                else if (key.Equals("Anchor", StringComparison.OrdinalIgnoreCase) &&
                         TryParseEnum(value, out OverlayAnchor anchor))
                {
                    presentation = presentation with { Anchor = anchor };
                }
                else if (key.Equals("ColorTheme", StringComparison.OrdinalIgnoreCase) &&
                         TryParseColorTheme(value, out OverlayColorTheme theme))
                {
                    presentation = presentation with { ColorTheme = theme };
                }
                else if (TryReadInt(key, value, "OffsetX", out int offsetX))
                {
                    presentation = presentation with { OffsetX = offsetX };
                }
                else if (TryReadInt(key, value, "OffsetY", out int offsetY))
                {
                    presentation = presentation with { OffsetY = offsetY };
                }
                else if (TryReadInt(key, value, "DecimalPlaces", out int decimals))
                {
                    presentation = presentation with { DecimalPlaces = decimals };
                }
                else if (TryReadInt(key, value, "FontSize", out int fontSize))
                {
                    presentation = presentation with { FontSize = fontSize };
                }
                else if (TryReadInt(key, value, "OpacityPercent", out int opacity))
                {
                    presentation = presentation with { OpacityPercent = opacity };
                }
                else if (TryReadInt(key, value, "Padding", out int padding))
                {
                    presentation = presentation with { Padding = padding };
                }
                else if (TryReadInt(key, value, "RefreshIntervalMilliseconds", out int refresh))
                {
                    presentation = presentation with { RefreshIntervalMilliseconds = refresh };
                }
                else if (TryReadBool(key, value, "ShowTitle", out bool showTitle))
                {
                    presentation = presentation with { ShowTitle = showTitle };
                }
                else if (TryReadBool(key, value, "ShowSeparators", out bool showSeparators))
                {
                    presentation = presentation with { ShowSeparators = showSeparators };
                }
                else if (TryReadBool(key, value, "ShowUnits", out bool showUnits))
                {
                    presentation = presentation with { ShowUnits = showUnits };
                }
            }

            if (settingsVersion < CurrentSettingsVersion)
            {

                _enabledMetrics = MetricFlags.All;
                _metricOrder = MetricRegistry.DefaultOrderPacked;
            }
            else
            {
                if (loadedMetrics.HasValue)
                {
                    _enabledMetrics = loadedMetrics.Value & MetricFlags.All;
                }

                if (!string.IsNullOrWhiteSpace(loadedMetricOrder) &&
                    UInt128.TryParse(loadedMetricOrder, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out UInt128 order))
                {
                    _metricOrder = MetricRegistry.NormalizeOrder(order);
                }
            }

            _metricOrder = MetricRegistry.NormalizeOrder(_metricOrder);
            _hotkey = NormalizeHotkey(_hotkey);
            _presentation = Normalize(presentation);
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to load settings", exception);
        }
    }

    private void SaveLocked()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            OverlayPresentationSettings value = _presentation;
            string content = string.Join('\n',
                $"SettingsVersion={CurrentSettingsVersion.ToString(CultureInfo.InvariantCulture)}",
                $"OverlayEnabled={_overlayEnabled}",
                $"OutputMode={_outputMode}",
                $"HotkeyEnabled={_hotkey.IsEnabled}",
                $"HotkeyModifiers={_hotkey.Modifiers.ToString(CultureInfo.InvariantCulture)}",
                $"HotkeyVirtualKey={_hotkey.VirtualKey.ToString(CultureInfo.InvariantCulture)}",
                $"EnabledMetrics={((ulong)_enabledMetrics).ToString("X", CultureInfo.InvariantCulture)}",
                $"MetricOrder={_metricOrder.ToString("X", CultureInfo.InvariantCulture)}",
                $"LayoutMode={value.LayoutMode}",
                $"Anchor={value.Anchor}",
                $"ColorTheme={value.ColorTheme}",
                $"OffsetX={value.OffsetX.ToString(CultureInfo.InvariantCulture)}",
                $"OffsetY={value.OffsetY.ToString(CultureInfo.InvariantCulture)}",
                $"DecimalPlaces={value.DecimalPlaces.ToString(CultureInfo.InvariantCulture)}",
                $"FontSize={value.FontSize.ToString(CultureInfo.InvariantCulture)}",
                $"OpacityPercent={value.OpacityPercent.ToString(CultureInfo.InvariantCulture)}",
                $"Padding={value.Padding.ToString(CultureInfo.InvariantCulture)}",
                $"RefreshIntervalMilliseconds={value.RefreshIntervalMilliseconds.ToString(CultureInfo.InvariantCulture)}",
                $"ShowTitle={value.ShowTitle}",
                $"ShowSeparators={value.ShowSeparators}",
                $"ShowUnits={value.ShowUnits}",
                string.Empty);

            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, content);
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception)
        {
            Logger.Error("Unable to save settings", exception);
        }
    }

    private static bool TryReadInt(string key, string value, string expectedKey, out int result)
    {
        result = 0;
        return key.Equals(expectedKey, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryReadBool(string key, string value, string expectedKey, out bool result)
    {
        result = false;
        return key.Equals(expectedKey, StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out result);
    }

    private static bool TryReadUInt(string key, string value, string expectedKey, out uint result)
    {
        result = 0;
        return key.Equals(expectedKey, StringComparison.OrdinalIgnoreCase) &&
               uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result))
        {
            return true;
        }
        result = default;
        return false;
    }

    private static bool TryParseColorTheme(string value, out OverlayColorTheme theme)
    {

        if (value.Equals("Amber", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ЯНТАРНАЯ", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ЯНТАРЬ", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("2", StringComparison.Ordinal))
        {
            theme = OverlayColorTheme.Amber;
            return true;
        }

        if (value.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Graphite", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Cyan", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ТЕМНАЯ", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0", StringComparison.Ordinal) ||
            value.Equals("1", StringComparison.Ordinal) ||
            value.Equals("3", StringComparison.Ordinal))
        {
            theme = OverlayColorTheme.Dark;
            return true;
        }

        theme = OverlayColorTheme.Dark;
        return false;
    }

    private static OverlayPresentationSettings Normalize(OverlayPresentationSettings value)
    {
        OverlayLayoutMode layout = Enum.IsDefined(value.LayoutMode)
            ? value.LayoutMode
            : OverlayLayoutMode.Standard;
        OverlayAnchor anchor = Enum.IsDefined(value.Anchor)
            ? value.Anchor
            : OverlayAnchor.TopLeft;
        OverlayColorTheme theme = Enum.IsDefined(value.ColorTheme)
            ? value.ColorTheme
            : OverlayColorTheme.Dark;

        int refresh = value.RefreshIntervalMilliseconds switch
        {
            <= 375 => 250,
            <= 750 => 500,
            <= 1500 => 1000,
            _ => 2000,
        };

        return value with
        {
            LayoutMode = layout,
            Anchor = anchor,
            ColorTheme = theme,
            OffsetX = Math.Clamp(value.OffsetX, -2000, 2000),
            OffsetY = Math.Clamp(value.OffsetY, -2000, 2000),
            DecimalPlaces = Math.Clamp(value.DecimalPlaces, 1, 3),
            FontSize = Math.Clamp(value.FontSize, 10, 32),
            OpacityPercent = Math.Clamp(value.OpacityPercent, 35, 100),
            Padding = Math.Clamp(value.Padding, 4, 32),
            RefreshIntervalMilliseconds = refresh,
        };
    }

    private static OverlayHotkey NormalizeHotkey(OverlayHotkey hotkey)
    {
        uint modifiers = hotkey.Modifiers & (Win32.MOD_ALT | Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_WIN);
        OverlayHotkey normalized = hotkey with { Modifiers = modifiers };
        return OverlayHotkeyFormatter.IsValid(normalized) ? normalized : OverlayHotkey.Disabled;
    }
}
