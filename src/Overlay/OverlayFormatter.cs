using DisplayedLatencyMetrics.App;
using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.Overlay;

internal enum OverlayLineRole
{
    Title,
    Metric,
    Separator,
    Status,
}

internal readonly record struct OverlayLine(string Text, OverlayLineRole Role);

internal readonly record struct OverlayDocument(OverlayLine[] Lines)
{
    internal static OverlayDocument Empty => new(Array.Empty<OverlayLine>());
}

internal static class OverlayFormatter
{
    private const string Title = "DLM - PIPELINE";

    private readonly record struct MetricItem(
        string Label,
        string Value,
        MetricGroup Group);

    internal static OverlayDocument Build(MetricSnapshot stats, SettingsSnapshot settings)
    {
        List<MetricItem> items = BuildItems(stats, settings);
        var lines = new List<OverlayLine>(items.Count + 8);
        OverlayPresentationSettings presentation = settings.Presentation;

        if (presentation.LayoutMode == OverlayLayoutMode.SingleLine)
        {
            AppendSingleLine(lines, items, presentation.ShowTitle);
        }
        else
        {
            if (presentation.ShowTitle)
            {
                lines.Add(new OverlayLine(Title, OverlayLineRole.Title));
            }

            if (items.Count == 0)
            {
                lines.Add(new OverlayLine("No metrics selected — open MENU", OverlayLineRole.Status));
            }
            else
            {
                switch (presentation.LayoutMode)
                {
                    case OverlayLayoutMode.Compact:
                        AppendCompact(lines, items);
                        break;

                    case OverlayLayoutMode.TwoColumns:
                        AppendTwoColumns(lines, items);
                        break;

                    default:
                        AppendStandard(lines, items, presentation.ShowSeparators && settings.OutputMode == OverlayOutputMode.NativeWindow);
                        break;
                }
            }
        }

        return new OverlayDocument(lines.ToArray());
    }

    internal static OverlayDocument BuildStatus(string status, SettingsSnapshot settings)
    {
        var lines = new List<OverlayLine>(3);
        if (settings.Presentation.ShowTitle)
        {
            lines.Add(new OverlayLine(Title, OverlayLineRole.Title));
        }
        lines.Add(new OverlayLine(status, OverlayLineRole.Status));
        return new OverlayDocument(lines.ToArray());
    }

    internal static OverlayDocument BuildPreview(
        OverlayPresentationSettings presentation,
        MetricFlags enabledMetrics,
        UInt128 metricOrder,
        OverlayOutputMode outputMode)
    {
        SettingsSnapshot settings = new(
            true,
            outputMode,
            OverlayHotkey.Disabled,
            enabledMetrics,
            metricOrder,
            presentation);
        MetricSnapshot sample = OverlayPreviewData.Create();
        return Build(sample, settings);
    }

    private static List<MetricItem> BuildItems(MetricSnapshot snapshot, SettingsSnapshot settings)
    {
        var items = new List<MetricItem>(MetricRegistry.Count);
        var formatOptions = new MetricFormatOptions(
            settings.Presentation.DecimalPlaces,
            settings.Presentation.ShowUnits);

        for (int index = 0; index < MetricRegistry.Count; index++)
        {
            MetricFlags flag = MetricRegistry.At(settings.MetricOrder, index);
            if (flag == MetricFlags.None || (settings.EnabledMetrics & flag) == 0)
            {
                continue;
            }

            IMetric metric = MetricRegistry.Get(flag);
            MetricValue value = snapshot.Get(flag);
            items.Add(new MetricItem(
                metric.Name,
                metric.Format(value, formatOptions),
                metric.Group));
        }

        return items;
    }

    private static void AppendStandard(List<OverlayLine> lines, List<MetricItem> items, bool separators)
    {
        int labelWidth = 0;
        foreach (MetricItem item in items)
        {
            labelWidth = Math.Max(labelWidth, item.Label.Length);
        }
        labelWidth = Math.Min(labelWidth, 38);

        MetricGroup previousGroup = items[0].Group;
        for (int index = 0; index < items.Count; index++)
        {
            MetricItem item = items[index];
            if (separators && index > 0 && item.Group != previousGroup)
            {
                lines.Add(new OverlayLine(new string('─', Math.Min(48, labelWidth + 18)), OverlayLineRole.Separator));
            }
            lines.Add(new OverlayLine(
                string.Concat(item.Label.PadRight(labelWidth), "  ", item.Value),
                OverlayLineRole.Metric));
            previousGroup = item.Group;
        }
    }

    private static void AppendCompact(List<OverlayLine> lines, List<MetricItem> items)
    {
        int labelWidth = 0;
        foreach (MetricItem item in items)
        {
            labelWidth = Math.Max(labelWidth, item.Label.Length);
        }
        foreach (MetricItem item in items)
        {
            lines.Add(new OverlayLine(
                string.Concat(item.Label.PadRight(labelWidth), "  ", item.Value),
                OverlayLineRole.Metric));
        }
    }

    private static void AppendTwoColumns(List<OverlayLine> lines, List<MetricItem> items)
    {
        var formatted = new string[items.Count];
        int columnWidth = 0;
        for (int index = 0; index < items.Count; index++)
        {
            formatted[index] = string.Concat(items[index].Label, "  ", items[index].Value);
            columnWidth = Math.Max(columnWidth, formatted[index].Length);
        }
        columnWidth += 5;

        for (int index = 0; index < formatted.Length; index += 2)
        {
            string text = index + 1 < formatted.Length
                ? string.Concat(formatted[index].PadRight(columnWidth), formatted[index + 1])
                : formatted[index];
            lines.Add(new OverlayLine(text, OverlayLineRole.Metric));
        }
    }

    private static void AppendSingleLine(List<OverlayLine> lines, List<MetricItem> items, bool showTitle)
    {
        var parts = new List<string>(items.Count + 1);
        if (showTitle)
        {
            parts.Add(Title);
        }
        foreach (MetricItem item in items)
        {
            parts.Add(string.Concat(item.Label, " ", item.Value));
        }
        if (parts.Count == 0)
        {
            parts.Add("No metrics selected — open MENU");
        }
        lines.Add(new OverlayLine(string.Join("  |  ", parts), OverlayLineRole.Metric));
    }
}
