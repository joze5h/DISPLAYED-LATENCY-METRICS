using DisplayedLatencyMetrics.Infrastructure;
using System.Reflection;
using System.Text;

namespace DisplayedLatencyMetrics.App;

internal static class EmbeddedWebAssets
{
    internal static string ReadHtml(string resourceName, string windowName)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? htmlStream = assembly.GetManifestResourceStream(resourceName);
            if (htmlStream is null)
            {
                Logger.Error($"Embedded HTML resource '{resourceName}' was not found.");
                return string.Empty;
            }

            using var reader = new StreamReader(
                htmlStream,
                new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception exception)
        {
            Logger.Error($"Unable to read embedded HTML {windowName}", exception);
            return string.Empty;
        }
    }
}
