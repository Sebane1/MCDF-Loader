using Dalamud.Plugin.Services;
using MareSynchronos.FileCache;
using MareSynchronos.MareConfiguration;
using MareSynchronos.PlayerData.Services;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MareSynchronos;

public class McdfLoader : MediatorSubscriberBase, IHostedService
{
    private readonly DalamudUtilService _dalamudUtil;
    private readonly MareConfigService _mareConfigService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private IServiceScope? _runtimeServiceScope;
    private Task? _launchTask = null;

    public McdfLoader( MareConfigService mareConfigService,
        DalamudUtilService dalamudUtil,
        IServiceScopeFactory serviceScopeFactory, McdfMediator mediator) : base(mediator)
    {
        _mareConfigService = mareConfigService;
        _dalamudUtil = dalamudUtil;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version!;
        Logger.Information("Launching {name} {major}.{minor}.{build}", "Mare Synchronos", version.Major, version.Minor, version.Build);

        Mediator.Subscribe<SwitchToMainUiMessage>(this, (msg) => { if (_launchTask == null || _launchTask.IsCompleted) _launchTask = Task.Run(WaitForPlayerAndLaunchCharacterManager); });
        Mediator.Subscribe<DalamudLoginMessage>(this, (_) => DalamudUtilOnLogIn());
        Mediator.Subscribe<DalamudLogoutMessage>(this, (_) => DalamudUtilOnLogOut());

        Mediator.StartQueueProcessing();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        UnsubscribeAll();

        DalamudUtilOnLogOut();

        Logger.Debug("Halting MarePlugin");

        return Task.CompletedTask;
    }

    private void DalamudUtilOnLogIn()
    {
        Logger?.Debug("Client login");
        if (_launchTask == null || _launchTask.IsCompleted) _launchTask = Task.Run(WaitForPlayerAndLaunchCharacterManager);
    }

    private void DalamudUtilOnLogOut()
    {
        Logger?.Debug("Client logout");

        _runtimeServiceScope?.Dispose();
    }

    private async Task WaitForPlayerAndLaunchCharacterManager()
    {
        while (!await _dalamudUtil.GetIsPlayerPresentAsync().ConfigureAwait(false))
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        try
        {
            Logger?.Debug("Launching Managers");

            _runtimeServiceScope?.Dispose();
            _runtimeServiceScope = _serviceScopeFactory.CreateScope();
            _runtimeServiceScope.ServiceProvider.GetRequiredService<CacheCreationService>();
            _runtimeServiceScope.ServiceProvider.GetRequiredService<TransientResourceManager>();
            _runtimeServiceScope.ServiceProvider.GetRequiredService<NotificationService>();

#if !DEBUG
            if (_mareConfigService.Current.LogLevel != LogLevel.Information)
            {
                Mediator.Publish(new NotificationMessage("Abnormal Log Level",
                    $"Your log level is set to '{_mareConfigService.Current.LogLevel}' which is not recommended for normal usage. Set it to '{LogLevel.Information}' in \"Mare Settings -> Debug\" unless instructed otherwise.",
                    MareConfiguration.Models.NotificationType.Error, TimeSpan.FromSeconds(15000)));
            }
#endif
        }
        catch (Exception ex)
        {
            Logger?.Error(ex, "Error during launch of managers");
        }
    }
}