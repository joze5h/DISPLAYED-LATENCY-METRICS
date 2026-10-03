using DisplayedLatencyMetrics.App;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Logger.Initialize();

        int comResult = Win32.CoInitializeEx(
            0,
            Win32.COINIT_APARTMENTTHREADED | Win32.COINIT_DISABLE_OLE1DDE);
        bool uninitializeCom = comResult >= 0;
        if (comResult < 0 && comResult != Win32.RPC_E_CHANGED_MODE)
        {
            Logger.Error($"CoInitializeEx(STA) failed. HRESULT=0x{comResult:X8}");
            return 1;
        }

        try
        {
            using var singleInstance = new Mutex(
                initiallyOwned: true,
                name: @"Local\DISPLAYED-LATENCY-METRICS.NativeAot",
                createdNew: out bool createdNew);

            if (!createdNew)
            {
                return 0;
            }

            StartupSplashWindow.Show();

            Thread.Sleep(500);

            bool firstRun = StartupState.IsFirstRun();

            if (!DependencyBootstrapper.EnsureDependencies())
            {
                return 1;
            }

            if (firstRun)
            {
                StartupState.MarkFirstRunCompleted();
            }

            try
            {
                using var app = new TrayApplication(openMenuOnStartup: firstRun);
                return app.Run();
            }
            catch (Exception exception)
            {
                Logger.Error("Fatal startup failure", exception);
                return 1;
            }
        }
        finally
        {
            if (uninitializeCom)
            {
                Win32.CoUninitialize();
            }
        }
    }
}
