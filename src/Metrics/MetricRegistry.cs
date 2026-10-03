using DisplayedLatencyMetrics.Metrics.Definitions;
using System.Numerics;

namespace DisplayedLatencyMetrics.Metrics;

internal readonly record struct MetricDescriptor(
    MetricFlags Flag,
    string Name,
    MetricGroup Group);

internal static class MetricRegistry
{
    internal const int Count = 23;
    private const int PackedBitWidth = 5;
    private const uint PackedBitMask = (1U << PackedBitWidth) - 1U;

    private static readonly IMetric[] Metrics =
    {
        new PresentModeMetric(),

        new AverageMsBetweenAppStartMetric(),
        new CpuBusyMetric(),
        new CpuWaitMetric(),

        new AverageMsGPUBusyMetric(),
        new AverageMsGPUTimeMetric(),
        new AverageMsGPUWaitMetric(),
        new AverageMsGPULatencyMetric(),

        new AverageMsBetweenPresentsMetric(),
        new MsBetweenPresentsSdMetric(),
        new RenderedRmssdMetric(),

        new StutterTimePercentMetric(),
        new StutterFramesMetric(),

        new AverageDisplayLatencyMetric(),
        new DisplayLatencySdMetric(),
        new DisplayLatencyRmssdMetric(),

        new MsUntilDisplayedMetric(),
        new MsUntilDisplayedSdMetric(),
        new MsUntilDisplayedRmssdMetric(),
        new AverageMsBetweenDisplayChangeMetric(),
        new MsBetweenDisplayChangeSdMetric(),
        new DisplayedRmssdMetric(),
        new DisplayedStepwiseRelativeMetric(),
    };

    private static readonly IMetric?[] ByBit = BuildByBit();
    private static readonly MetricDescriptor[] DefaultOrderItems = BuildDefaultOrder();

    internal static ReadOnlySpan<IMetric> All => Metrics;

    internal static ReadOnlySpan<MetricDescriptor> DefaultOrder => DefaultOrderItems;

    internal static readonly UInt128 DefaultOrderPacked = Pack(Metrics);

    internal static IMetric Get(MetricFlags flag)
    {
        int index = BitIndex(flag);
        if (index < 0 || ByBit[index] is not IMetric metric)
        {
            throw new ArgumentOutOfRangeException(nameof(flag), flag, "Unknown metric flag.");
        }

        return metric;
    }

    internal static MetricFlags At(UInt128 packedOrder, int index)
    {
        if ((uint)index >= Count)
        {
            return MetricFlags.None;
        }

        int bit = (int)((packedOrder >> (index * PackedBitWidth)) & (UInt128)PackedBitMask);
        return bit < Count && ByBit[bit] is not null
            ? (MetricFlags)(1UL << bit)
            : MetricFlags.None;
    }

    internal static UInt128 Pack(ReadOnlySpan<MetricFlags> order)
    {
        UInt128 packed = 0;
        int count = Math.Min(order.Length, Count);
        for (int index = 0; index < count; index++)
        {
            int bit = BitIndex(order[index]);
            if (bit >= 0)
            {
                packed |= (UInt128)(uint)bit << (index * PackedBitWidth);
            }
        }

        return packed;
    }

    internal static UInt128 NormalizeOrder(UInt128 packedOrder)
    {
        Span<bool> seen = stackalloc bool[Count];
        Span<MetricFlags> normalized = stackalloc MetricFlags[Count];
        int output = 0;

        for (int index = 0; index < Count; index++)
        {
            MetricFlags flag = At(packedOrder, index);
            int bit = BitIndex(flag);
            if (bit >= 0 && !seen[bit])
            {
                seen[bit] = true;
                normalized[output++] = flag;
            }
        }

        foreach (IMetric metric in Metrics)
        {
            int bit = BitIndex(metric.Flag);
            if (!seen[bit])
            {
                seen[bit] = true;
                normalized[output++] = metric.Flag;
            }
        }

        return Pack(normalized);
    }

    internal static int BitIndex(MetricFlags flag)
    {
        ulong value = (ulong)flag;
        if (value == 0 || (value & (value - 1)) != 0)
        {
            return -1;
        }

        int index = BitOperations.TrailingZeroCount(value);
        return index < Count ? index : -1;
    }

    private static UInt128 Pack(ReadOnlySpan<IMetric> metrics)
    {
        Span<MetricFlags> flags = stackalloc MetricFlags[Count];
        for (int index = 0; index < metrics.Length; index++)
        {
            flags[index] = metrics[index].Flag;
        }

        return Pack(flags);
    }

    private static MetricDescriptor[] BuildDefaultOrder()
    {
        var descriptors = new MetricDescriptor[Metrics.Length];
        for (int index = 0; index < Metrics.Length; index++)
        {
            IMetric metric = Metrics[index];
            descriptors[index] = new MetricDescriptor(metric.Flag, metric.Name, metric.Group);
        }

        return descriptors;
    }

    private static IMetric?[] BuildByBit()
    {
        var byBit = new IMetric?[Count];
        foreach (IMetric metric in Metrics)
        {
            int bit = BitIndex(metric.Flag);
            if (bit < 0 || byBit[bit] is not null)
            {
                throw new InvalidOperationException($"Duplicate or invalid metric flag: {metric.Flag}.");
            }

            byBit[bit] = metric;
        }

        for (int index = 0; index < byBit.Length; index++)
        {
            if (byBit[index] is null)
            {
                throw new InvalidOperationException($"Metric bit {index} is not registered.");
            }
        }

        return byBit;
    }
}
