using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using McdfLoader.PlayerData.Export;
using McdfLoader.PlayerData.Handlers;
using McdfLoader.Services;
using McdfLoader.Services.Mediator;
using McdfDataImporter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoleplayingVoiceDalamud.Glamourer;

namespace McdfLoader.Interop.Ipc;

public class IpcProvider : IHostedService, IMediatorSubscriber
{
    private readonly IPluginLog _Logger;
    private readonly IDalamudPluginInterface _pi;
    private readonly McdfCharaFileManager _McdfCharaFileManager;
    private readonly DalamudUtilService _dalamudUtil;
    private ICallGateProvider<string, IGameObject, int, bool>? _loadFileProvider;
    private ICallGateProvider<string, IGameObject, Task<bool>>? _loadFileAsyncProvider;
    private ICallGateProvider<List<nint>>? _handledGameAddresses;
    private ICallGateProvider<string, IGameObject, bool> _loadFileProviderMcdfCompat;
    private ICallGateProvider<string, IGameObject, Task<bool>> _loadFileAsyncProviderMcdfCompat;
    private ICallGateProvider<List<nint>> _handledGameAddressesMcdfCompat;
    private readonly List<GameObjectHandler> _activeGameObjectHandlers = [];

    public McdfMediator Mediator { get; init; }

    public IpcProvider(IDalamudPluginInterface pi,
        McdfCharaFileManager McdfCharaFileManager, DalamudUtilService dalamudUtil,
        McdfMediator McdfMediator)
    {
        _pi = pi;
        _McdfCharaFileManager = McdfCharaFileManager;
        _dalamudUtil = dalamudUtil;
        Mediator = McdfMediator;

        Mediator.Subscribe<GameObjectHandlerCreatedMessage>(this, (msg) =>
        {
            if (msg.OwnedObject) return;
            _activeGameObjectHandlers.Add(msg.GameObjectHandler);
        });
        Mediator.Subscribe<GameObjectHandlerDestroyedMessage>(this, (msg) =>
        {
            if (msg.OwnedObject) return;
            _activeGameObjectHandlers.Remove(msg.GameObjectHandler);
        });
        Task.Run(() => StartAsync(CancellationToken.None));
        AppearanceAccessUtils.AppearanceManager = this;
    }
    public async void Dispose()
    {
        await StopAsync(CancellationToken.None);
        RemoveAllTemporaryCollections();
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dalamudUtil.RunOnFrameworkThread(() =>
        {
            _loadFileProvider = _pi.GetIpcProvider<string, IGameObject, int, bool>("McdfStandalone.LoadMcdf");
            _loadFileProvider.RegisterFunc(LoadAppearance);
            _loadFileAsyncProvider = _pi.GetIpcProvider<string, IGameObject, Task<bool>>("McdfStandalone.LoadMcdfAsync");
            _loadFileAsyncProvider.RegisterFunc(LoadMcdfAsync);
            _handledGameAddresses = _pi.GetIpcProvider<List<nint>>("McdfStandalone.GetHandledAddresses");
            _handledGameAddresses.RegisterFunc(GetHandledAddresses);

            _loadFileProviderMcdfCompat = _pi.GetIpcProvider<string, IGameObject, bool>("McdfSynchronos.LoadMcdf");
            _loadFileProviderMcdfCompat.RegisterFunc(LoadAppearance);
            _loadFileAsyncProviderMcdfCompat = _pi.GetIpcProvider<string, IGameObject, Task<bool>>("McdfSynchronos.LoadMcdfAsync");
            _loadFileAsyncProviderMcdfCompat.RegisterFunc(LoadMcdfAsync);
            _handledGameAddressesMcdfCompat = _pi.GetIpcProvider<List<nint>>("McdfSynchronos.GetHandledAddresses");
            _handledGameAddressesMcdfCompat.RegisterFunc(GetHandledAddresses);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _loadFileProvider?.UnregisterFunc();
        _loadFileAsyncProvider?.UnregisterFunc();
        _handledGameAddresses?.UnregisterFunc();
        Mediator.UnsubscribeAll(this);
        return Task.CompletedTask;
    }

    public async Task<bool> LoadMcdfAsync(string path, IGameObject target)
    {
        await ApplyAppearanceAsync(path, target).ConfigureAwait(false);
        return true;
    }

    public bool LoadAppearance(string path, IGameObject target, int appearanceSwap)
    {
        _ = Task.Run(async () => await ApplyAppearanceAsync(path, target, appearanceSwap).ConfigureAwait(false)).ConfigureAwait(false);

        return true;
    }
    public bool LoadAppearance(string path, IGameObject target)
    {
        return LoadAppearance(path, target, 0);
    }
    public CharacterCustomization GetGlamourerCustomization()
    {
        return _McdfCharaFileManager.GetGlamourerCustomization();
    }
    public void CreateMCDF(string path)
    {
        _McdfCharaFileManager.SaveMcdfCharaFile("Quest Reborn MCDF", path);
    }
    public bool IsWorking()
    {
        return _McdfCharaFileManager.CurrentlyWorking;
    }
    public void RemoveAllTemporaryCollections()
    {
        _McdfCharaFileManager.RemoveAllTemporaryCollections();
    }
    public void RemoveTemporaryCollection(string name)
    {
        _McdfCharaFileManager.RemoveTemporaryCollection(name);
    }
    private async Task ApplyAppearanceAsync(string path, IGameObject target, int appearanceApplicationType = 0)
    {
        try
        {
            if (path.Contains(".mcdf"))
            {
                var data = _McdfCharaFileManager.LoadMcdfCharaFile(path);
                if (data != null)
                {
                    if (target != null)
                    {
                        await _McdfCharaFileManager.ApplyMcdfCharaFile(target, data.Item1, data.Item2, appearanceApplicationType).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                _McdfCharaFileManager.ApplyStandaloneGlamourerString(target, path, appearanceApplicationType);
            }
        }
        catch (Exception e)
        {
            _Logger.Error(e, "Failure of IPC call");
        }
    }

    private List<nint> GetHandledAddresses()
    {
        return _activeGameObjectHandlers.Where(g => g.Address != nint.Zero).Select(g => g.Address).Distinct().ToList();
    }
}
