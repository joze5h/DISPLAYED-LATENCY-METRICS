using DisplayedLatencyMetrics.Infrastructure;
using System.Diagnostics;

namespace DisplayedLatencyMetrics.Interop;

internal static class PresentMonRuntime
{
    public static string? FindApiDll()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("PRESENTMON_API2_DLL");
        if (!string.IsNullOrWhiteSpace(explicitPath) && IsSupportedApiDll(explicitPath))
        {
            return explicitPath;
        }

        var candidates = new List<string>();
        AddCandidateRoot(candidates, Environment.GetEnvironmentVariable("ProgramW6432"));
        AddCandidateRoot(candidates, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IsSupportedApiDll(candidate))
            {
                return candidate;
            }
        }

        string programFiles = Environment.GetEnvironmentVariable("ProgramW6432") ??
                              Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string intelRoot = Path.Combine(programFiles, "Intel");
        if (!Directory.Exists(intelRoot))
        {
            return null;
        }

        try
        {
            foreach (string candidate in Directory.EnumerateFiles(
                         intelRoot,
                         "PresentMonAPI2.dll",
                         SearchOption.AllDirectories))
            {
                if (IsSupportedApiDll(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Warning("PresentMon recursive API search failed.", exception);
        }

        return null;
    }

    private static bool IsSupportedApiDll(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
            bool supported = version.FileMajorPart == 2 &&
                             version.FileMinorPart == 5 &&
                             version.FileBuildPart == 1;
            if (!supported)
            {
                Logger.Warning(
                    $"Ignoring unsupported PresentMonAPI2.dll {version.FileVersion ?? "unknown"}: {path}");
            }

            return supported;
        }
        catch (Exception exception)
        {
            Logger.Warning($"Unable to validate PresentMonAPI2.dll version at {path}.", exception);
            return false;
        }
    }

    private static void AddCandidateRoot(List<string> candidates, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        candidates.Add(Path.Combine(root, "Intel", "PresentMonSharedService", "PresentMonAPI2.dll"));
        candidates.Add(Path.Combine(root, "Intel", "PresentMon", "PresentMonAPI2.dll"));
        candidates.Add(Path.Combine(root, "Intel", "PresentMon", "SDK", "PresentMonAPI2.dll"));
    }
}
