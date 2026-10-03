using DisplayedLatencyMetrics.Interop;
using System.Runtime.CompilerServices;

namespace DisplayedLatencyMetrics.Capture;

internal sealed unsafe class PresentMonFrameDecoder
{
    internal static readonly PmMetric[] RequiredMetrics =
    {
        PmMetric.SwapChainAddress,
        PmMetric.CpuStartQpc,
        PmMetric.FrameType,
        PmMetric.SyncInterval,
        PmMetric.PresentMode,
        PmMetric.PresentRuntime,
        PmMetric.AllowsTearing,
        PmMetric.BetweenAppStart,
        PmMetric.CpuBusy,
        PmMetric.CpuWait,
        PmMetric.GpuBusy,
        PmMetric.GpuTime,
        PmMetric.GpuWait,
        PmMetric.GpuLatency,
        PmMetric.DisplayLatency,
        PmMetric.UntilDisplayed,
        PmMetric.BetweenDisplayChange,
        PmMetric.BetweenPresents,
    };

    private readonly record struct Reader(
        ulong Offset,
        ulong Size,
        PmDataType Type,
        double Scale);

    private readonly PmQueryElement[] _elements;
    private readonly Dictionary<PmMetric, int> _indexes;
    private readonly Dictionary<PmMetric, PmMetricInfo> _metricInfo;

    private Reader _swapChain;
    private Reader _cpuStartQpc;
    private Reader _frameType;
    private Reader _syncInterval;
    private Reader _presentMode;
    private Reader _presentRuntime;
    private Reader _allowsTearing;
    private Reader _betweenAppStart;
    private Reader _cpuBusy;
    private Reader _cpuWait;
    private Reader _gpuBusy;
    private Reader _gpuTime;
    private Reader _gpuWait;
    private Reader _gpuLatency;
    private Reader _displayLatency;
    private Reader _untilDisplayed;
    private Reader _betweenDisplay;
    private Reader _betweenPresents;
    private uint _blobSize;
    private bool _initialized;

    internal PresentMonFrameDecoder(Dictionary<PmMetric, PmMetricInfo> metricInfo)
    {
        _metricInfo = metricInfo;
        _elements = new PmQueryElement[RequiredMetrics.Length];
        _indexes = new Dictionary<PmMetric, int>(RequiredMetrics.Length);
        for (int index = 0; index < RequiredMetrics.Length; index++)
        {
            PmMetric metric = RequiredMetrics[index];
            _elements[index] = new PmQueryElement
            {
                Metric = metric,
                Stat = PmStat.None,
                DeviceId = 0,
                ArrayIndex = 0,
            };
            _indexes.Add(metric, index);
        }
    }

    internal PmQueryElement[] Elements => _elements;

    internal void InitializeLayout(uint blobSize)
    {
        if (blobSize == 0)
        {
            throw new InvalidOperationException("PresentMon returned an empty frame blob.");
        }

        _blobSize = blobSize;
        _swapChain = CreateReader(PmMetric.SwapChainAddress);
        _cpuStartQpc = CreateReader(PmMetric.CpuStartQpc);
        _frameType = CreateReader(PmMetric.FrameType);
        _syncInterval = CreateReader(PmMetric.SyncInterval);
        _presentMode = CreateReader(PmMetric.PresentMode);
        _presentRuntime = CreateReader(PmMetric.PresentRuntime);
        _allowsTearing = CreateReader(PmMetric.AllowsTearing);
        _betweenAppStart = CreateReader(PmMetric.BetweenAppStart);
        _cpuBusy = CreateReader(PmMetric.CpuBusy);
        _cpuWait = CreateReader(PmMetric.CpuWait);
        _gpuBusy = CreateReader(PmMetric.GpuBusy);
        _gpuTime = CreateReader(PmMetric.GpuTime);
        _gpuWait = CreateReader(PmMetric.GpuWait);
        _gpuLatency = CreateReader(PmMetric.GpuLatency);
        _displayLatency = CreateReader(PmMetric.DisplayLatency);
        _untilDisplayed = CreateReader(PmMetric.UntilDisplayed);
        _betweenDisplay = CreateReader(PmMetric.BetweenDisplayChange);
        _betweenPresents = CreateReader(PmMetric.BetweenPresents);
        _initialized = true;
    }

    internal bool TryDecode(byte* blob, uint blobSize, out ulong swapChain, out FrameSample sample)
    {
        swapChain = 0;
        sample = default;
        if (!_initialized || blobSize < _blobSize ||
            !TryReadUInt64(blob, _swapChain, out swapChain) ||
            !TryReadUInt64(blob, _cpuStartQpc, out ulong cpuStartQpc) ||
            !TryReadNumeric(blob, _frameType, out double rawFrameType) ||
            !TryReadNumeric(blob, _syncInterval, out double rawSyncInterval) ||
            !TryReadNumeric(blob, _presentMode, out double rawPresentMode) ||
            !TryReadNumeric(blob, _presentRuntime, out double rawPresentRuntime) ||
            !TryReadNumeric(blob, _allowsTearing, out double rawAllowsTearing) ||
            !TryReadMilliseconds(blob, _betweenAppStart, out double betweenAppStart) ||
            !TryReadMilliseconds(blob, _cpuBusy, out double cpuBusy) ||
            !TryReadMilliseconds(blob, _cpuWait, out double cpuWait) ||
            !TryReadMilliseconds(blob, _gpuBusy, out double gpuBusy) ||
            !TryReadMilliseconds(blob, _gpuTime, out double gpuTime) ||
            !TryReadMilliseconds(blob, _gpuWait, out double gpuWait) ||
            !TryReadMilliseconds(blob, _gpuLatency, out double gpuLatency) ||
            !TryReadMilliseconds(blob, _displayLatency, out double displayLatency) ||
            !TryReadMilliseconds(blob, _untilDisplayed, out double untilDisplayed) ||
            !TryReadMilliseconds(blob, _betweenDisplay, out double betweenDisplay) ||
            !TryReadMilliseconds(blob, _betweenPresents, out double betweenPresents))
        {
            return false;
        }

        sample = new FrameSample(
            cpuStartQpc,
            (PmFrameType)ToInt32(rawFrameType),
            (PmPresentMode)ToInt32(rawPresentMode),
            (PmGraphicsRuntime)ToInt32(rawPresentRuntime),
            ToInt32(rawSyncInterval),
            rawAllowsTearing != 0.0,
            betweenAppStart,
            cpuBusy,
            cpuWait,
            gpuBusy,
            gpuTime,
            gpuWait,
            gpuLatency,
            displayLatency,
            untilDisplayed,
            betweenDisplay,
            betweenPresents);
        return true;
    }

    private Reader CreateReader(PmMetric metric)
    {
        PmQueryElement element = _elements[_indexes[metric]];
        PmMetricInfo info = _metricInfo[metric];
        int required = DataTypeSize(info.FrameType);
        if (required <= 0 || element.DataSize < (ulong)required ||
            element.DataOffset > _blobSize || (ulong)required > _blobSize - element.DataOffset)
        {
            throw new InvalidOperationException($"PresentMon returned an invalid layout for {metric}.");
        }
        return new Reader(element.DataOffset, element.DataSize, info.FrameType, info.ToMilliseconds);
    }

    private static bool TryReadMilliseconds(byte* blob, Reader reader, out double value)
    {
        value = double.NaN;
        if (!TryReadNumeric(blob, reader, out double raw))
        {
            return false;
        }
        value = raw * reader.Scale;
        return true;
    }

    private static bool TryReadUInt64(byte* blob, Reader reader, out ulong value)
    {
        value = 0;
        if (reader.Type != PmDataType.UInt64 || reader.Size < sizeof(ulong))
        {
            return false;
        }
        ref byte source = ref Unsafe.AsRef<byte>(blob + checked((int)reader.Offset));
        value = Unsafe.ReadUnaligned<ulong>(ref source);
        return true;
    }

    private static bool TryReadNumeric(byte* blob, Reader reader, out double value)
    {
        value = double.NaN;
        ref byte source = ref Unsafe.AsRef<byte>(blob + checked((int)reader.Offset));
        switch (reader.Type)
        {
            case PmDataType.Double:
                value = Unsafe.ReadUnaligned<double>(ref source);
                return true;

            case PmDataType.Int32:
            case PmDataType.Enum:
                value = Unsafe.ReadUnaligned<int>(ref source);
                return true;

            case PmDataType.UInt32:
                value = Unsafe.ReadUnaligned<uint>(ref source);
                return true;

            case PmDataType.UInt64:
                value = Unsafe.ReadUnaligned<ulong>(ref source);
                return true;

            case PmDataType.Bool:
                value = source == 0 ? 0.0 : 1.0;
                return true;

            default:
                return false;
        }
    }

    private static int ToInt32(double value) =>
        double.IsFinite(value) && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : 0;

    private static int DataTypeSize(PmDataType type) => type switch
    {
        PmDataType.Double => sizeof(double),
        PmDataType.Int32 => sizeof(int),
        PmDataType.UInt32 => sizeof(uint),
        PmDataType.Enum => sizeof(int),
        PmDataType.UInt64 => sizeof(ulong),
        PmDataType.Bool => sizeof(byte),
        _ => 0,
    };
}
