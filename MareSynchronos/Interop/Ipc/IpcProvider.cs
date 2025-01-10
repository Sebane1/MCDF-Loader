using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using MareSynchronos.PlayerData.Export;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using McdfDataImporter;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoleplayingVoiceDalamud.Glamourer;

namespace MareSynchronos.Interop.Ipc;

public class IpcProvider : IHostedService, IMediatorSubscriber
{
    private readonly IPluginLog _Logger;
    private readonly IDalamudPluginInterface _pi;
    private readonly MareCharaFileManager _mareCharaFileManager;
    private readonly DalamudUtilService _dalamudUtil;
    private ICallGateProvider<string, IGameObject, int, bool>? _loadFileProvider;
    private ICallGateProvider<string, IGameObject, Task<bool>>? _loadFileAsyncProvider;
    private ICallGateProvider<List<nint>>? _handledGameAddresses;
    private readonly List<GameObjectHandler> _activeGameObjectHandlers = [];

    public McdfMediator Mediator { get; init; }

    public IpcProvider(IDalamudPluginInterface pi,
        MareCharaFileManager mareCharaFileManager, DalamudUtilService dalamudUtil,
        McdfMediator mareMediator)
    {
        _pi = pi;
        _mareCharaFileManager = mareCharaFileManager;
        _dalamudUtil = dalamudUtil;
        Mediator = mareMediator;

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
        _loadFileProvider = _pi.GetIpcProvider<string, IGameObject, int, bool>("McdfStandalone.LoadMcdf");
        _loadFileProvider.RegisterFunc(LoadAppearance);
        _loadFileAsyncProvider = _pi.GetIpcProvider<string, IGameObject, Task<bool>>("McdfStandalone.LoadMcdfAsync");
        _loadFileAsyncProvider.RegisterFunc(LoadMcdfAsync);
        _handledGameAddresses = _pi.GetIpcProvider<List<nint>>("McdfStandalone.GetHandledAddresses");
        _handledGameAddresses.RegisterFunc(GetHandledAddresses);
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
    public CharacterCustomization GetGlamourerCustomization()
    {
        return _mareCharaFileManager.GetGlamourerCustomization();
    }
    public void CreateMCDF(string path)
    {
        _mareCharaFileManager.SaveMareCharaFile("Quest Reborn MCDF", path);
    }
    public bool IsWorking()
    {
        return _mareCharaFileManager.CurrentlyWorking;
    }
    public void RemoveAllTemporaryCollections()
    {
        _mareCharaFileManager.RemoveAllTemporaryCollections();
    }
    public void RemoveTemporaryCollection(string name)
    {
        _mareCharaFileManager.RemoveTemporaryCollection(name);
    }
    private async Task ApplyAppearanceAsync(string path, IGameObject target, int appearanceApplicationType = 0)
    {
        try
        {
            if (path.Length < 256)
            {
                var data = _mareCharaFileManager.LoadMareCharaFile(path);
                if (data != null)
                {
                    if (target != null)
                    {
                        await _mareCharaFileManager.ApplyMareCharaFile(target, data.Item1, data.Item2, appearanceApplicationType).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                _mareCharaFileManager.ApplyStandaloneGlamourerString(target, path, appearanceApplicationType);
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
