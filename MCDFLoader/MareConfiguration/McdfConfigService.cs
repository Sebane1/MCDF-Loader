using McdfLoader.McdfConfiguration.Configurations;

namespace McdfLoader.McdfConfiguration;

public class McdfConfigService : ConfigurationServiceBase<McdfConfig>
{
    public const string ConfigName = "config.json";

    public McdfConfigService(string configDir) : base(configDir)
    {
    }

    public override string ConfigurationName => ConfigName;
}