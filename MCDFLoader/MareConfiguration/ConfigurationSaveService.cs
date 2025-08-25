using Dalamud.Plugin.Services;
using McdfLoader.McdfConfiguration.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace McdfLoader.McdfConfiguration;

public class ConfigurationSaveService : IHostedService
{
    private readonly HashSet<object> _configsToSave = [];
    private readonly SemaphoreSlim _configSaveSemaphore = new(1, 1);
    private readonly CancellationTokenSource _configSaveCheckCts = new();
    public const string BackupFolder = "config_backup";
    private readonly MethodInfo _saveMethod;

    public ConfigurationSaveService(IEnumerable<IConfigService<IMcdfConfiguration>> configs)
    {
        foreach (var config in configs)
        {
            config.ConfigSave += OnConfigurationSave;
        }
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        _saveMethod = GetType().GetMethod(nameof(SaveConfig), BindingFlags.Instance | BindingFlags.NonPublic)!;
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
    }

    private void OnConfigurationSave(object? sender, EventArgs e)
    {
        _configSaveSemaphore.Wait();
        _configsToSave.Add(sender!);
        _configSaveSemaphore.Release();
    }

    private async Task PeriodicSaveCheck(CancellationToken ct)
    {
    }

    private async Task SaveConfigs()
    {
        if (_configsToSave.Count == 0) return;

        await _configSaveSemaphore.WaitAsync().ConfigureAwait(false);
        var configList = _configsToSave.ToList();
        _configsToSave.Clear();
        _configSaveSemaphore.Release();

        foreach (var config in configList)
        {
            var expectedType = config.GetType().BaseType!.GetGenericArguments()[0];
            var save = _saveMethod.MakeGenericMethod(expectedType);
            await ((Task)save.Invoke(this, [config])!).ConfigureAwait(false);
        }
    }

    private async Task SaveConfig<T>(IConfigService<T> config) where T : IMcdfConfiguration
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
