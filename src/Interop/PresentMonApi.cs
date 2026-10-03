using System.Runtime.InteropServices;

namespace DisplayedLatencyMetrics.Interop;

internal enum PmStatus : int
{
    Success = 0,
    Failure,
    BadArgument,
    BadHandle,
    ServiceError,
    InvalidEtlFile,
    InvalidPid,
    AlreadyTrackingProcess,
    UnableToCreateNsm,
    InvalidAdapterId,
    OutOfRange,
    InsufficientBuffer,
    PipeError,
    SessionNotOpen,
    MiddlewareMissingPath,
    NonexistentFilePath,
    MiddlewareInvalidSignature,
    MiddlewareMissingEndpoint,
    MiddlewareVersionLow,
    MiddlewareVersionHigh,
    MiddlewareServiceMismatch,
    QueryMalformed,
    ModeMismatch,
    FeatureDisabled,
}

internal enum PmMetric : int
{
    SwapChainAddress = 1,
    CpuStartQpc = 7,
    BetweenAppStart = 86,
    CpuBusy = 9,
    CpuWait = 10,
    GpuTime = 13,
    GpuBusy = 14,
    GpuWait = 15,
    SyncInterval = 18,
    PresentMode = 20,
    PresentRuntime = 21,
    AllowsTearing = 22,
    GpuLatency = 23,
    DisplayLatency = 24,
    FrameType = 63,
    BetweenPresents = 78,
    BetweenDisplayChange = 80,
    UntilDisplayed = 81,
}

internal enum PmPresentMode : int
{
    Unknown = 0,
    HardwareLegacyFlip = 1,
    HardwareLegacyCopyToFrontBuffer = 2,
    HardwareIndependentFlip = 3,
    ComposedFlip = 4,
    ComposedCopyWithGpuGdi = 5,
    ComposedCopyWithCpuGdi = 6,
    HardwareComposedIndependentFlip = 8,
}

internal enum PmGraphicsRuntime : int
{
    Unknown = 0,
    Dxgi = 1,
    D3d9 = 2,
}

internal enum PmFrameType : int
{
    NotSet = 0,
    Unspecified = 1,
    Application = 2,
    Repeated = 3,
    IntelXeFg = 50,
    AmdAfmf = 100,
}

internal enum PmStat : int
{
    None = 0,
}

internal enum PmUnit : int
{
    Dimensionless,
    Ratio,
    Boolean,
    Percent,
    Fps,
    Microseconds,
    Milliseconds,
    Seconds,
    Minutes,
    Hours,
    Milliwatts,
    Watts,
    Kilowatts,
    VerticalBlanks,
    Millivolts,
    Volts,
    Hertz,
    Kilohertz,
    Megahertz,
    Gigahertz,
    Celsius,
    Rpm,
    BitsPerSecond,
    KilobitsPerSecond,
    MegabitsPerSecond,
    GigabitsPerSecond,
    Bytes,
    Kilobytes,
    Megabytes,
    Gigabytes,
    Qpc,
}

internal enum PmDataType : int
{
    Double,
    Int32,
    UInt32,
    Enum,
    String,
    UInt64,
    Bool,
    Void,
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmIntrospectionObjectArray
{
    internal nint Data;
    internal nuint Size;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmIntrospectionDataTypeInfo
{
    internal PmDataType PolledType;
    internal PmDataType FrameType;
    internal int EnumId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmIntrospectionMetric
{
    internal PmMetric Id;
    internal int Type;
    internal PmUnit Unit;
    internal PmUnit PreferredUnitHint;
    internal nint TypeInfo;
    internal nint StatInfo;
    internal nint DeviceMetricInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmIntrospectionUnit
{
    internal PmUnit Id;
    internal PmUnit BaseUnitId;
    internal double Scale;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmIntrospectionRoot
{
    internal nint Metrics;
    internal nint Enums;
    internal nint Devices;
    internal nint Units;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PmQueryElement
{
    internal PmMetric Metric;
    internal PmStat Stat;
    internal uint DeviceId;
    internal uint ArrayIndex;
    internal ulong DataOffset;
    internal ulong DataSize;
}

internal readonly record struct PmMetricInfo(PmDataType FrameType, double ToMilliseconds);

internal sealed unsafe class PresentMonApi : IDisposable
{
    private nint _module;

    private readonly delegate* unmanaged[Cdecl]<nint*, PmStatus> _openSession;
    private readonly delegate* unmanaged[Cdecl]<nint, PmStatus> _closeSession;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, PmStatus> _startTrackingProcess;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, PmStatus> _stopTrackingProcess;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, PmStatus> _setEtwFlushPeriod;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, PmStatus> _flushFrames;
    private readonly delegate* unmanaged[Cdecl]<nint, nint*, PmQueryElement*, ulong, uint*, PmStatus> _registerFrameQuery;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, byte*, uint*, PmStatus> _consumeFrames;
    private readonly delegate* unmanaged[Cdecl]<nint, PmStatus> _freeFrameQuery;
    private readonly delegate* unmanaged[Cdecl]<nint, nint*, PmStatus> _getIntrospectionRoot;
    private readonly delegate* unmanaged[Cdecl]<nint, PmStatus> _freeIntrospectionRoot;

    private PresentMonApi(nint module)
    {
        _module = module;
        _openSession = (delegate* unmanaged[Cdecl]<nint*, PmStatus>)ResolveAddress("pmOpenSession");
        _closeSession = (delegate* unmanaged[Cdecl]<nint, PmStatus>)ResolveAddress("pmCloseSession");
        _startTrackingProcess = (delegate* unmanaged[Cdecl]<nint, uint, PmStatus>)ResolveAddress("pmStartTrackingProcess");
        _stopTrackingProcess = (delegate* unmanaged[Cdecl]<nint, uint, PmStatus>)ResolveAddress("pmStopTrackingProcess");
        _setEtwFlushPeriod = (delegate* unmanaged[Cdecl]<nint, uint, PmStatus>)ResolveAddress("pmSetEtwFlushPeriod");
        _flushFrames = (delegate* unmanaged[Cdecl]<nint, uint, PmStatus>)ResolveAddress("pmFlushFrames");
        _registerFrameQuery = (delegate* unmanaged[Cdecl]<nint, nint*, PmQueryElement*, ulong, uint*, PmStatus>)ResolveAddress("pmRegisterFrameQuery");
        _consumeFrames = (delegate* unmanaged[Cdecl]<nint, uint, byte*, uint*, PmStatus>)ResolveAddress("pmConsumeFrames");
        _freeFrameQuery = (delegate* unmanaged[Cdecl]<nint, PmStatus>)ResolveAddress("pmFreeFrameQuery");
        _getIntrospectionRoot = (delegate* unmanaged[Cdecl]<nint, nint*, PmStatus>)ResolveAddress("pmGetIntrospectionRoot");
        _freeIntrospectionRoot = (delegate* unmanaged[Cdecl]<nint, PmStatus>)ResolveAddress("pmFreeIntrospectionRoot");
    }

    public static PresentMonApi Load(string path)
    {
        nint module = Win32.LoadLibraryEx(
            path,
            0,
            Win32.LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | Win32.LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (module == 0)
        {
            module = Win32.LoadLibraryEx(path, 0, Win32.LOAD_WITH_ALTERED_SEARCH_PATH);
        }
        if (module == 0)
        {
            throw new InvalidOperationException($"Unable to load PresentMonAPI2.dll: {path}. Win32={Marshal.GetLastWin32Error()}");
        }

        try
        {
            return new PresentMonApi(module);
        }
        catch
        {
            Win32.FreeLibrary(module);
            throw;
        }
    }

    public PmStatus OpenSession(out nint session)
    {
        nint local = 0;
        PmStatus status = _openSession(&local);
        session = local;
        return status;
    }

    public PmStatus CloseSession(nint session) => _closeSession(session);

    public PmStatus StartTrackingProcess(nint session, uint pid) => _startTrackingProcess(session, pid);

    public PmStatus StopTrackingProcess(nint session, uint pid) => _stopTrackingProcess(session, pid);

    public PmStatus SetEtwFlushPeriod(nint session, uint milliseconds) => _setEtwFlushPeriod(session, milliseconds);

    public PmStatus FlushFrames(nint session, uint pid) => _flushFrames(session, pid);

    public PmStatus FreeFrameQuery(nint query) => _freeFrameQuery(query);

    public PmStatus RegisterFrameQuery(
        nint session,
        out nint query,
        PmQueryElement[] elements,
        out uint blobSize)
    {
        nint localQuery = 0;
        uint localBlobSize = 0;
        PmStatus status;
        fixed (PmQueryElement* elementPointer = elements)
        {
            status = _registerFrameQuery(
                session,
                &localQuery,
                elementPointer,
                (ulong)elements.LongLength,
                &localBlobSize);
        }
        query = localQuery;
        blobSize = localBlobSize;
        return status;
    }

    public PmStatus ConsumeFrames(nint query, uint pid, byte* buffer, ref uint frameCount)
    {
        uint localCount = frameCount;
        PmStatus status = _consumeFrames(query, pid, buffer, &localCount);
        frameCount = localCount;
        return status;
    }

    public Dictionary<PmMetric, PmMetricInfo> ResolveMetricInfo(nint session, ReadOnlySpan<PmMetric> metrics)
    {
        nint rootAddress = 0;
        PmStatus status = _getIntrospectionRoot(session, &rootAddress);
        if (status != PmStatus.Success || rootAddress == 0)
        {
            throw new InvalidOperationException($"pmGetIntrospectionRoot failed: {status}");
        }

        try
        {
            var result = new Dictionary<PmMetric, PmMetricInfo>(metrics.Length);
            foreach (PmMetric metric in metrics)
            {
                result.Add(metric, ResolveMetric((PmIntrospectionRoot*)rootAddress, metric));
            }
            return result;
        }
        finally
        {
            _freeIntrospectionRoot(rootAddress);
        }
    }

    private static PmMetricInfo ResolveMetric(PmIntrospectionRoot* root, PmMetric metricId)
    {
        PmIntrospectionMetric* metric = FindMetric(root, metricId);
        if (metric == null || metric->TypeInfo == 0)
        {
            throw new InvalidOperationException($"PresentMon metric {metricId} is unavailable.");
        }

        PmIntrospectionDataTypeInfo* typeInfo = (PmIntrospectionDataTypeInfo*)metric->TypeInfo;
        if (!IsNumeric(typeInfo->FrameType))
        {
            throw new InvalidOperationException($"PresentMon metric {metricId} has unsupported frame type {typeInfo->FrameType}.");
        }

        double toMilliseconds = 1.0;
        if (IsDurationMetric(metricId))
        {
            PmIntrospectionUnit* source = FindUnit(root, metric->Unit);
            PmIntrospectionUnit* destination = FindUnit(root, PmUnit.Milliseconds);
            if (source == null || destination == null ||
                source->BaseUnitId != destination->BaseUnitId ||
                !double.IsFinite(source->Scale) ||
                !double.IsFinite(destination->Scale) ||
                destination->Scale == 0.0)
            {
                throw new InvalidOperationException($"PresentMon metric {metricId} cannot be converted to milliseconds.");
            }
            toMilliseconds = source->Scale / destination->Scale;
            if (!double.IsFinite(toMilliseconds) || toMilliseconds <= 0.0)
            {
                throw new InvalidOperationException($"PresentMon metric {metricId} returned an invalid unit scale.");
            }
        }

        return new PmMetricInfo(typeInfo->FrameType, toMilliseconds);
    }

    private static bool IsDurationMetric(PmMetric metric) => metric is
        PmMetric.BetweenAppStart or
        PmMetric.CpuBusy or
        PmMetric.CpuWait or
        PmMetric.GpuBusy or
        PmMetric.GpuTime or
        PmMetric.GpuWait or
        PmMetric.GpuLatency or
        PmMetric.DisplayLatency or
        PmMetric.BetweenPresents or
        PmMetric.BetweenDisplayChange or
        PmMetric.UntilDisplayed;

    private static PmIntrospectionMetric* FindMetric(PmIntrospectionRoot* root, PmMetric id)
    {
        if (root == null || root->Metrics == 0)
        {
            return null;
        }
        PmIntrospectionObjectArray* array = (PmIntrospectionObjectArray*)root->Metrics;
        if (array->Data == 0)
        {
            return null;
        }
        nint* items = (nint*)array->Data;
        for (nuint index = 0; index < array->Size; index++)
        {
            PmIntrospectionMetric* metric = (PmIntrospectionMetric*)items[index];
            if (metric != null && metric->Id == id)
            {
                return metric;
            }
        }
        return null;
    }

    private static PmIntrospectionUnit* FindUnit(PmIntrospectionRoot* root, PmUnit id)
    {
        if (root == null || root->Units == 0)
        {
            return null;
        }
        PmIntrospectionObjectArray* array = (PmIntrospectionObjectArray*)root->Units;
        if (array->Data == 0)
        {
            return null;
        }
        nint* items = (nint*)array->Data;
        for (nuint index = 0; index < array->Size; index++)
        {
            PmIntrospectionUnit* unit = (PmIntrospectionUnit*)items[index];
            if (unit != null && unit->Id == id)
            {
                return unit;
            }
        }
        return null;
    }

    private static bool IsNumeric(PmDataType type) => type is
        PmDataType.Double or
        PmDataType.Int32 or
        PmDataType.UInt32 or
        PmDataType.Enum or
        PmDataType.UInt64 or
        PmDataType.Bool;

    private nint ResolveAddress(string name)
    {
        nint address = Win32.GetProcAddress(_module, name);
        if (address == 0)
        {
            throw new MissingMethodException($"PresentMon API export is missing: {name}");
        }
        return address;
    }

    public void Dispose()
    {
        nint module = Interlocked.Exchange(ref _module, 0);
        if (module != 0)
        {
            Win32.FreeLibrary(module);
        }
        GC.SuppressFinalize(this);
    }
}

internal static class PresentMonStatus
{
    internal static string Describe(PmStatus status) => $"{status} ({(int)status})";
}
