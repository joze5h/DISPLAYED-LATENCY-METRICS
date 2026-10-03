using DisplayedLatencyMetrics.Interop;
using System.Runtime.CompilerServices;
using System.Text;

namespace DisplayedLatencyMetrics.Overlay;

internal static unsafe class RtssOverlayPublisher
{
    private const string MappingName = "RTSSSharedMemoryV2";
    private const string Owner = "DISPLAYED-LATENCY-METRICS";
    private const uint RtssSignature = 0x52545353;
    private const uint MinimumVersion = 0x00020000;
    private const uint ExtendedTextVersion = 0x00020007;
    private const uint BusyLockVersion = 0x0002000E;

    private const int HeaderSignatureOffset = 0;
    private const int HeaderVersionOffset = 4;
    private const int HeaderOsdEntrySizeOffset = 20;
    private const int HeaderOsdArrayOffsetOffset = 24;
    private const int HeaderOsdArraySizeOffset = 28;
    private const int HeaderOsdFrameOffset = 32;
    private const int HeaderBusyOffset = 36;

    private const int SlotTextOffset = 0;
    private const int SlotOwnerOffset = 256;
    private const int SlotExtendedTextOffset = 512;
    private const int SlotTextCapacity = 256;
    private const int SlotOwnerCapacity = 256;
    private const int SlotExtendedTextCapacity = 4096;

    internal static bool IsAvailable()
    {
        if (!TryOpen(out nint mapping, out nint view, out _, out _))
        {
            return false;
        }

        Close(mapping, view);
        return true;
    }

    internal static string AvailabilityMessage()
    {
        return TryOpen(out nint mapping, out nint view, out _, out _)
            ? CloseAndDescribe(mapping, view, "RTSS работает, вывод метрик доступен.")
            : "Процесс RTSS не обнаружен, установите и запустите RivaTuner Statistics Server.";
    }

    internal static bool TryPublish(OverlayDocument document, out string problem)
    {
        string text = ToRtssText(document);
        if (!TryOpen(out nint mapping, out nint view, out uint version, out problem))
        {
            return false;
        }

        try
        {
            if (!TryFindOwnedOrFreeSlot(view, out nint slot))
            {
                problem = "Все сторонние OSD-слоты RTSS заняты другими приложениями.";
                return false;
            }

            if (!TryLock(view, version, out bool locked))
            {
                problem = "RTSS временно занят отрисовкой OSD; повторите обновление.";
                return false;
            }

            try
            {
                WriteAscii(slot + SlotOwnerOffset, SlotOwnerCapacity, Owner);
                if (version >= ExtendedTextVersion)
                {
                    WriteAscii(slot + SlotExtendedTextOffset, SlotExtendedTextCapacity, text);
                    WriteAscii(slot + SlotTextOffset, SlotTextCapacity, string.Empty);
                }
                else
                {
                    WriteAscii(slot + SlotTextOffset, SlotTextCapacity, text);
                }
                unchecked
                {
                    Unsafe.AsRef<uint>((void*)(view + HeaderOsdFrameOffset))++;
                }
                problem = string.Empty;
                return true;
            }
            finally
            {
                if (locked)
                {
                    Volatile.Write(ref Unsafe.AsRef<int>((void*)(view + HeaderBusyOffset)), 0);
                }
            }
        }
        finally
        {
            Close(mapping, view);
        }
    }

    internal static void Clear()
    {
        if (!TryOpen(out nint mapping, out nint view, out uint version, out _))
        {
            return;
        }

        try
        {
            if (!TryLock(view, version, out bool locked))
            {
                return;
            }
            try
            {
                uint entrySize = ReadUInt32(view + HeaderOsdEntrySizeOffset);
                uint arrayOffset = ReadUInt32(view + HeaderOsdArrayOffsetOffset);
                uint arraySize = ReadUInt32(view + HeaderOsdArraySizeOffset);
                if (entrySize < SlotExtendedTextOffset || arrayOffset == 0)
                {
                    return;
                }

                for (uint index = 1; index < arraySize; index++)
                {
                    nint slot = view + checked((int)(arrayOffset + index * entrySize));
                    if (!OwnerMatches(slot + SlotOwnerOffset))
                    {
                        continue;
                    }

                    WriteAscii(slot + SlotTextOffset, SlotTextCapacity, string.Empty);
                    WriteAscii(slot + SlotOwnerOffset, SlotOwnerCapacity, string.Empty);
                    if (version >= ExtendedTextVersion)
                    {
                        WriteAscii(slot + SlotExtendedTextOffset, SlotExtendedTextCapacity, string.Empty);
                    }
                    unchecked
                    {
                        Unsafe.AsRef<uint>((void*)(view + HeaderOsdFrameOffset))++;
                    }
                }
            }
            finally
            {
                if (locked)
                {
                    Volatile.Write(ref Unsafe.AsRef<int>((void*)(view + HeaderBusyOffset)), 0);
                }
            }
        }
        finally
        {
            Close(mapping, view);
        }
    }

    private static bool TryOpen(out nint mapping, out nint view, out uint version, out string problem)
    {
        mapping = Win32.OpenFileMapping(Win32.FILE_MAP_ALL_ACCESS, false, MappingName);
        view = 0;
        version = 0;
        problem = "RTSS не запущен или его shared-memory API недоступен.";
        if (mapping == 0)
        {
            return false;
        }

        view = Win32.MapViewOfFile(mapping, Win32.FILE_MAP_ALL_ACCESS, 0, 0, 0);
        if (view == 0)
        {
            Win32.CloseHandle(mapping);
            mapping = 0;
            problem = "Не удалось открыть shared-memory API RTSS.";
            return false;
        }

        uint signature = ReadUInt32(view + HeaderSignatureOffset);
        version = ReadUInt32(view + HeaderVersionOffset);
        if (signature == RtssSignature && version >= MinimumVersion)
        {
            return true;
        }

        Close(mapping, view);
        mapping = 0;
        view = 0;
        problem = "Версия shared-memory API RTSS не поддерживается.";
        return false;
    }

    private static string CloseAndDescribe(nint mapping, nint view, string text)
    {
        Close(mapping, view);
        return text;
    }

    private static bool TryFindOwnedOrFreeSlot(nint view, out nint slot)
    {
        slot = 0;
        uint entrySize = ReadUInt32(view + HeaderOsdEntrySizeOffset);
        uint arrayOffset = ReadUInt32(view + HeaderOsdArrayOffsetOffset);
        uint arraySize = ReadUInt32(view + HeaderOsdArraySizeOffset);
        if (entrySize < SlotExtendedTextOffset || arrayOffset == 0 || arraySize < 2)
        {
            return false;
        }

        for (uint index = 1; index < arraySize; index++)
        {
            nint candidate = view + checked((int)(arrayOffset + index * entrySize));
            if (OwnerMatches(candidate + SlotOwnerOffset))
            {
                slot = candidate;
                return true;
            }
        }

        for (uint index = 1; index < arraySize; index++)
        {
            nint candidate = view + checked((int)(arrayOffset + index * entrySize));
            if (ReadAscii(candidate + SlotOwnerOffset, SlotOwnerCapacity).Length == 0)
            {
                slot = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool TryLock(nint view, uint version, out bool locked)
    {
        locked = false;
        if (version < BusyLockVersion)
        {
            return true;
        }

        ref int busy = ref Unsafe.AsRef<int>((void*)(view + HeaderBusyOffset));
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
        {
            return false;
        }
        locked = true;
        return true;
    }

    private static bool OwnerMatches(nint address) =>
        string.Equals(ReadAscii(address, SlotOwnerCapacity), Owner, StringComparison.Ordinal);

    private static uint ReadUInt32(nint address) => Unsafe.ReadUnaligned<uint>((void*)address);

    private static string ReadAscii(nint address, int capacity)
    {
        ReadOnlySpan<byte> bytes = new((void*)address, capacity);
        int length = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(length < 0 ? bytes : bytes[..length]);
    }

    private static void WriteAscii(nint address, int capacity, string text)
    {
        Span<byte> destination = new((void*)address, capacity);
        destination.Clear();
        int length = Encoding.ASCII.GetBytes(text.AsSpan(), destination[..Math.Max(0, capacity - 1)]);
        if (length < capacity)
        {
            destination[length] = 0;
        }
    }

    private static void Close(nint mapping, nint view)
    {
        if (view != 0)
        {
            Win32.UnmapViewOfFile(view);
        }
        if (mapping != 0)
        {
            Win32.CloseHandle(mapping);
        }
    }

    private static string ToRtssText(OverlayDocument document)
    {
        var builder = new StringBuilder(512);
        foreach (OverlayLine line in document.Lines)
        {
            string text = line.Text
                .Replace('─', '-')
                .Replace('·', '/')
                .Replace('→', '>')
                .Replace('…', '.');
            if (builder.Length != 0)
            {
                builder.Append('\n');
            }
            builder.Append(text);
        }
        return builder.ToString();
    }
}
