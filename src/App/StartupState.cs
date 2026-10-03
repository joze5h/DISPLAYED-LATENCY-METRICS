using DisplayedLatencyMetrics.Infrastructure;
using Microsoft.Win32;

namespace DisplayedLatencyMetrics.App;

internal static class StartupState
{
    private const string RegistryPath = @"Software\DISPLAYED-LATENCY-METRICS";
    private const string FirstRunCompletedValue = "FirstRunCompleted";

    internal static bool IsFirstRun()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            return key?.GetValue(FirstRunCompletedValue) is not int value || value != 1;
        }
        catch (Exception exception)
        {
            Logger.Warning("Unable to read first-run state from the registry.", exception);
            return true;
        }
    }

    internal static void MarkFirstRunCompleted()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
            key.SetValue(FirstRunCompletedValue, 1, RegistryValueKind.DWord);
        }
        catch (Exception exception)
        {

            Logger.Warning("Unable to save first-run state to the registry.", exception);
        }
    }
}
