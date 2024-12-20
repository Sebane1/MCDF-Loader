using MareSynchronos.MareConfiguration.Models;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.MareConfiguration.Configurations;

[Serializable]
public class MareConfig : IMareConfiguration
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