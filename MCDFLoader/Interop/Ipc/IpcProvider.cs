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
    private readonly McdfCharaFileManager _mcdfCharaFileManager;
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
        _mcdfCharaFileManager = McdfCharaFileManager;
        _dalamudUtil = dalamudUtil;
        Mediator = McdfMediator;
        _Logger = EntryPoint.PluginLog;

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
        string capturedName = string.Empty;
        ushort capturedIndex = 0;
        nint capturedAddress = nint.Zero;

        if (target != null)
        {
            try
            {
                if (_dalamudUtil.IsOnFrameworkThread)
                {
                    capturedName = target.Name?.TextValue ?? string.Empty;
                    capturedIndex = target.ObjectIndex;
                    capturedAddress = target.Address;
                }
                else
                {
                    (capturedName, capturedIndex, capturedAddress) = await _dalamudUtil.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            return (target.Name?.TextValue ?? string.Empty, target.ObjectIndex, target.Address);
                        }
                        catch
                        {
                            return (string.Empty, (ushort)0, nint.Zero);
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch
            {
                try { capturedAddress = target.Address; } catch { }
            }
        }

        await ApplyAppearanceAsync(path, target, 0, capturedName, capturedIndex, capturedAddress).ConfigureAwait(false);
        return true;
    }

    public bool LoadAppearance(string path, IGameObject target, int appearanceSwap)
    {
        // Notice: LoadAppearance may be called from a Task.Run (background thread), e.g. from AQuestReborn.
        // Therefore, accessing target.Name directly can throw "Not on main thread!".
        // Safely extract name, index, and address via DalamudUtilService / framework thread if needed.
        string capturedName = string.Empty;
        ushort capturedIndex = 0;
        nint capturedAddress = nint.Zero;

        if (target != null)
        {
            try
            {
                if (_dalamudUtil.IsOnFrameworkThread)
                {
                    capturedName = target.Name?.TextValue ?? string.Empty;
                    capturedIndex = target.ObjectIndex;
                    capturedAddress = target.Address;
                }
                else
                {
                    (capturedName, capturedIndex, capturedAddress) = _dalamudUtil.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            return (target.Name?.TextValue ?? string.Empty, target.ObjectIndex, target.Address);
                        }
                        catch
                        {
                            return (string.Empty, (ushort)0, nint.Zero);
                        }
                    }).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Fallback in case of framework run failure
                try { capturedAddress = target.Address; } catch { }
            }
        }

        _ = Task.Run(async () => await ApplyAppearanceAsync(path, target, appearanceSwap, capturedName, capturedIndex, capturedAddress).ConfigureAwait(false)).ConfigureAwait(false);

        return true;
    }
    public bool LoadAppearance(string path, IGameObject target)
    {
        return LoadAppearance(path, target, 0);
    }
    public CharacterCustomization GetGlamourerCustomization()
    {
        return _mcdfCharaFileManager.GetGlamourerCustomization();
    }
    public void CreateMCDF(string path)
    {
        _mcdfCharaFileManager.SaveMcdfCharaFile("MCDF", path);
    }
    public bool IsWorking()
    {
        return _mcdfCharaFileManager.CurrentlyWorking;
    }
    public void RemoveAllTemporaryCollections()
    {
        _mcdfCharaFileManager.RemoveAllTemporaryCollections();
    }
    public void RemoveTemporaryCollection(string name)
    {
        _mcdfCharaFileManager.RemoveTemporaryCollection(name);
    }
    private async Task ApplyAppearanceAsync(string path, IGameObject? target, int appearanceApplicationType = 0,
        string capturedName = "", ushort capturedIndex = 0, nint capturedAddress = 0)
    {
        try
        {
            if (path.Contains(".mcdf") || path.Contains(".bin"))
            {
                var data = _mcdfCharaFileManager.LoadMcdfCharaFile(path);
                if (data != null)
                {
                    if (target != null)
                    {
                        await _mcdfCharaFileManager.ApplyMcdfCharaFile(target, data.Item1, data.Item2, appearanceApplicationType, capturedName, capturedIndex, capturedAddress).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await _mcdfCharaFileManager.ApplyStandaloneGlamourerString(target, path, appearanceApplicationType, capturedName, capturedIndex).ConfigureAwait(false);
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
