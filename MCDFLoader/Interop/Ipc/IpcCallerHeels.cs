using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using McdfLoader.Services;
using McdfLoader.Services.Mediator;
using Microsoft.Extensions.Logging;

namespace McdfLoader.Interop.Ipc;

public sealed class IpcCallerHeels : IIpcCaller
{
    private readonly IPluginLog  _Logger;
    private readonly McdfMediator _McdfMediator;
    private readonly DalamudUtilService _dalamudUtil;
    private readonly ICallGateSubscriber<(int, int)> _heelsGetApiVersion;
    private readonly ICallGateSubscriber<string> _heelsGetOffset;
    private readonly ICallGateSubscriber<string, object?> _heelsOffsetUpdate;
    private readonly ICallGateSubscriber<int, string, object?> _heelsRegisterPlayer;
    private readonly ICallGateSubscriber<int, object?> _heelsUnregisterPlayer;

    public IpcCallerHeels( IDalamudPluginInterface pi, DalamudUtilService dalamudUtil, McdfMediator McdfMediator)
    {
        _Logger = EntryPoint.PluginLog;
        _McdfMediator = McdfMediator;
        _dalamudUtil = dalamudUtil;
        _heelsGetApiVersion = pi.GetIpcSubscriber<(int, int)>("SimpleHeels.ApiVersion");
        _heelsGetOffset = pi.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
        _heelsRegisterPlayer = pi.GetIpcSubscriber<int, string, object?>("SimpleHeels.RegisterPlayer");
        _heelsUnregisterPlayer = pi.GetIpcSubscriber<int, object?>("SimpleHeels.UnregisterPlayer");
        _heelsOffsetUpdate = pi.GetIpcSubscriber<string, object?>("SimpleHeels.LocalChanged");

        _heelsOffsetUpdate.Subscribe(HeelsOffsetChange);

        CheckAPI();
    }

    public bool APIAvailable { get; private set; } = false;

    private void HeelsOffsetChange(string offset)
    {
        _McdfMediator.Publish(new HeelsOffsetMessage());
    }

    public async Task<string> GetOffsetAsync()
    {
        if (!APIAvailable) return string.Empty;
        return await _dalamudUtil.RunOnFrameworkThread(_heelsGetOffset.InvokeFunc).ConfigureAwait(false);
    }

    public async Task RestoreOffsetForPlayerAsync(IntPtr character)
    {
        if (!APIAvailable) return;
        await _dalamudUtil.RunOnFrameworkThread(() =>
        {
            var gameObj = _dalamudUtil.CreateGameObject(character);
            if (gameObj != null)
            {
                _Logger.Debug("Restoring Heels data to {chara}", character.ToString("X"));
                _heelsUnregisterPlayer.InvokeAction(gameObj.ObjectIndex);
            }
        }).ConfigureAwait(false);
    }

    public async Task SetOffsetForPlayerAsync(IntPtr character, string data)
    {
        if (!APIAvailable) return;
        await _dalamudUtil.RunOnFrameworkThread(() =>
        {
            var gameObj = _dalamudUtil.CreateGameObject(character);
            if (gameObj != null)
            {
                _Logger.Debug("Applying Heels data to {chara}", character.ToString("X"));
                _heelsRegisterPlayer.InvokeAction(gameObj.ObjectIndex, data);
            }
        }).ConfigureAwait(false);
    }

    public void CheckAPI()
    {
        try
        {
            APIAvailable = _heelsGetApiVersion.InvokeFunc() is { Item1: 2, Item2: >= 1 };
        }
        catch
        {
            APIAvailable = false;
        }
    }

    public void Dispose()
    {
        _heelsOffsetUpdate.Unsubscribe(HeelsOffsetChange);
    }
}
