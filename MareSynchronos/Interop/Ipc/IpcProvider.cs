﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using MareSynchronos.PlayerData.Export;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.Interop.Ipc;

public class IpcProvider : IHostedService, IMediatorSubscriber
{
    private readonly ILogger<IpcProvider> _Logger;
    private readonly IDalamudPluginInterface _pi;
    private readonly MareCharaFileManager _mareCharaFileManager;
    private readonly DalamudUtilService _dalamudUtil;
    private ICallGateProvider<string, IGameObject, bool>? _loadFileProvider;
    private ICallGateProvider<string, IGameObject, Task<bool>>? _loadFileAsyncProvider;
    private ICallGateProvider<List<nint>>? _handledGameAddresses;
    private readonly List<GameObjectHandler> _activeGameObjectHandlers = [];

    public MareMediator Mediator { get; init; }

    public IpcProvider(ILogger<IpcProvider> Logger, IDalamudPluginInterface pi,
        MareCharaFileManager mareCharaFileManager, DalamudUtilService dalamudUtil,
        MareMediator mareMediator)
    {
        //_//Logger = //Logger;
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
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //_//Logger.LogInformation("Starting IpcProviderService");
        _loadFileProvider = _pi.GetIpcProvider<string, IGameObject, bool>("MareSynchronos.LoadMcdf");
        _loadFileProvider.RegisterFunc(LoadMcdf);
        _loadFileAsyncProvider = _pi.GetIpcProvider<string, IGameObject, Task<bool>>("MareSynchronos.LoadMcdfAsync");
        _loadFileAsyncProvider.RegisterFunc(LoadMcdfAsync);
        _handledGameAddresses = _pi.GetIpcProvider<List<nint>>("MareSynchronos.GetHandledAddresses");
        _handledGameAddresses.RegisterFunc(GetHandledAddresses);
        //_//Logger.LogInformation("Started IpcProviderService");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //_//Logger.LogDebug("Stopping IpcProvider Service");
        _loadFileProvider?.UnregisterFunc();
        _loadFileAsyncProvider?.UnregisterFunc();
        _handledGameAddresses?.UnregisterFunc();
        Mediator.UnsubscribeAll(this);
        return Task.CompletedTask;
    }

    private async Task<bool> LoadMcdfAsync(string path, IGameObject target)
    {
        if (_mareCharaFileManager.CurrentlyWorking || !_dalamudUtil.IsInGpose)
            return false;

        await ApplyFileAsync(path, target).ConfigureAwait(false);

        return true;
    }

    private bool LoadMcdf(string path, IGameObject target)
    {
        if (_mareCharaFileManager.CurrentlyWorking || !_dalamudUtil.IsInGpose)
            return false;

        _ = Task.Run(async () => await ApplyFileAsync(path, target).ConfigureAwait(false)).ConfigureAwait(false);

        return true;
    }

    private async Task ApplyFileAsync(string path, IGameObject target)
    {
        try
        {
            var expectedLength = _mareCharaFileManager.LoadMareCharaFile(path);
            await _mareCharaFileManager.ApplyMareCharaFile(target, expectedLength).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //_//Logger.LogError(e, "Failure of IPC call");
        }
        finally
        {
            _mareCharaFileManager.ClearMareCharaFile();
        }
    }

    private List<nint> GetHandledAddresses()
    {
        return _activeGameObjectHandlers.Where(g => g.Address != nint.Zero).Select(g => g.Address).Distinct().ToList();
    }
}
