using McdfLoader.McdfConfiguration.Configurations;

namespace McdfLoader.McdfConfiguration;

public interface IConfigService<out T> : IDisposable where T : IMcdfConfiguration
{
    T Current { get; }
    string ConfigurationName { get; }
    string ConfigurationPath { get; }
    public event EventHandler? ConfigSave;
    void UpdateLastWriteTime();
}
