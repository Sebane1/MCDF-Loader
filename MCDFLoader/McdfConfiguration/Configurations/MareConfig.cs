using McdfLoader.McdfConfiguration.Models;
using Microsoft.Extensions.Logging;

namespace McdfLoader.McdfConfiguration.Configurations;

[Serializable]
public class McdfConfig : IMcdfConfiguration
{
    public string CacheFolder { get; set; } = string.Empty;
    public bool InitialScanComplete { get; internal set; }
    public double MaxLocalCacheInGiB { get; internal set; }
    public bool LogPerformance { get; internal set; }
    public NotificationLocation InfoNotification { get; internal set; }
    public NotificationLocation WarningNotification { get; internal set; }
    public NotificationLocation ErrorNotification { get; internal set; }

    internal bool HasValidSetup()
    {
        throw new NotImplementedException();
    }
}