using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;
using MareSynchronos.MareConfiguration.Models;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.Interop.Ipc;

public sealed class IpcCallerGlamourer : DisposableMediatorSubscriberBase, IIpcCaller
{
    private readonly IPluginLog _Logger;
    private readonly IDalamudPluginInterface _pi;
    private readonly DalamudUtilService _dalamudUtil;
    private readonly McdfMediator _mareMediator;
    private readonly RedrawManager _redrawManager;

    private readonly ApiVersion _glamourerApiVersions;
    private readonly ApplyState? _glamourerApplyAll;
    private readonly GetStateBase64? _glamourerGetAllCustomization;
    private readonly RevertState _glamourerRevert;
    private readonly RevertStateName _glamourerRevertByName;
    private readonly UnlockState _glamourerUnlock;
    private readonly UnlockStateName _glamourerUnlockByName;
    private readonly EventSubscriber<nint>? _glamourerStateChanged;

    private bool _shownGlamourerUnavailable = false;
    private readonly uint LockCode = 0x6D617265;

    public IpcCallerGlamourer(IDalamudPluginInterface pi, DalamudUtilService dalamudUtil, McdfMediator mareMediator,
        RedrawManager redrawManager) : base(mareMediator)
    {
        _glamourerApiVersions = new ApiVersion(pi);
        _glamourerGetAllCustomization = new GetStateBase64(pi);
        _glamourerApplyAll = new ApplyState(pi);
        _glamourerRevert = new RevertState(pi);
        _glamourerRevertByName = new RevertStateName(pi);
        _glamourerUnlock = new UnlockState(pi);
        _glamourerUnlockByName = new UnlockStateName(pi);

        _Logger = EntryPoint.PluginLog;
        _pi = pi;
        _dalamudUtil = dalamudUtil;
        _mareMediator = mareMediator;
        _redrawManager = redrawManager;
        CheckAPI();

        _glamourerStateChanged = StateChanged.Subscriber(pi, GlamourerChanged);
        _glamourerStateChanged.Enable();

        Mediator.Subscribe<DalamudLoginMessage>(this, s => _shownGlamourerUnavailable = false);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _redrawManager.Cancel();
        _glamourerStateChanged?.Dispose();
    }

    public bool APIAvailable { get; private set; }

    public void CheckAPI()
    {
        bool apiAvailable = false;
        try
        {
            bool versionValid = (_pi.InstalledPlugins
                .FirstOrDefault(p => string.Equals(p.InternalName, "Glamourer", StringComparison.OrdinalIgnoreCase))
                ?.Version ?? new Version(0, 0, 0, 0)) >= new Version(1, 3, 0, 10);
            try
            {
                var version = _glamourerApiVersions.Invoke();
                if (version is { Major: 1, Minor: >= 1 } && versionValid)
                {
                    apiAvailable = true;
                }
            }
            catch
            {
                // ignore
            }
            _shownGlamourerUnavailable = _shownGlamourerUnavailable && !apiAvailable;

            APIAvailable = apiAvailable;
        }
        catch
        {
            APIAvailable = apiAvailable;
        }
        finally
        {
            if (!apiAvailable && !_shownGlamourerUnavailable)
            {
                _shownGlamourerUnavailable = true;
                _mareMediator.Publish(new NotificationMessage("Glamourer inactive", "Your Glamourer installation is not active or out of date. Update Glamourer to continue to use MCDF Loader. If you just updated Glamourer, ignore this message.",
                    NotificationType.Error));
            }
        }
    }

    public async Task ApplyAllAsync(IGameObject gameObject, GameObjectHandler handler, string? customization, Guid applicationId, CancellationToken token, bool onlyEquipment = false, bool locksCustomization = false)
    {
        if (!APIAvailable || string.IsNullOrEmpty(customization) || _dalamudUtil.IsZoning) return;

        await _redrawManager.RedrawSemaphore.WaitAsync(token).ConfigureAwait(false);

        try
        {

            await _redrawManager.PenumbraRedrawInternalAsync(handler, applicationId, (chara) =>
            {
                try
                {
                    Logger.Debug("[{appid}] Calling on IPC: GlamourerApplyAll", applicationId);
                    _glamourerApplyAll!.Invoke(customization, chara.ObjectIndex, locksCustomization ? LockCode : 0, onlyEquipment ? Glamourer.Api.Enums.ApplyFlag.Equipment : Glamourer.Api.Enums.ApplyFlag.Customization | Glamourer.Api.Enums.ApplyFlag.Equipment);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "[{appid}] Failed to apply Glamourer data", applicationId);
                }
            }, token).ConfigureAwait(false);
        }
        finally
        {
            _redrawManager.RedrawSemaphore.Release();
        }
    }

    public async Task<string> GetCharacterCustomizationAsync(IntPtr character)
    {
        if (!APIAvailable) return string.Empty;
        try
        {
            return await _dalamudUtil.RunOnFrameworkThread(() =>
            {
                var gameObj = _dalamudUtil.CreateGameObject(character);
                if (gameObj is ICharacter c)
                {
                    return _glamourerGetAllCustomization!.Invoke(c.ObjectIndex).Item2 ?? string.Empty;
                }
                return string.Empty;
            }).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task RevertAsync(string name, GameObjectHandler handler, Guid applicationId, CancellationToken token)
    {
        if ((!APIAvailable) || _dalamudUtil.IsZoning) return;
        try
        {
            await _redrawManager.RedrawSemaphore.WaitAsync(token).ConfigureAwait(false);
            await _redrawManager.PenumbraRedrawInternalAsync(handler, applicationId, (chara) =>
            {
                try
                {
                    Logger.Debug("[{appid}] Calling On IPC: GlamourerUnlockName", applicationId);
                    _glamourerUnlock.Invoke(chara.ObjectIndex, LockCode);
                    Logger.Debug("[{appid}] Calling On IPC: GlamourerRevert", applicationId);
                    _glamourerRevert.Invoke(chara.ObjectIndex, LockCode);
                    Logger.Debug("[{appid}] Calling On IPC: PenumbraRedraw", applicationId);

                    _mareMediator.Publish(new PenumbraRedrawCharacterMessage(chara));
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "[{appid}] Error during GlamourerRevert", applicationId);
                }
            }, token).ConfigureAwait(false);
        }
        finally
        {
            _redrawManager.RedrawSemaphore.Release();
        }
    }

    public async Task RevertByNameAsync(string name, Guid applicationId)
    {
        if ((!APIAvailable) || _dalamudUtil.IsZoning) return;

        await _dalamudUtil.RunOnFrameworkThread(() =>
        {
            RevertByName(name, applicationId);

        }).ConfigureAwait(false);
    }

    public void RevertByName(string name, Guid applicationId)
    {
        if ((!APIAvailable) || _dalamudUtil.IsZoning) return;

        try
        {
            Logger.Debug("[{appid}] Calling On IPC: GlamourerRevertByName", applicationId);
            _glamourerRevertByName.Invoke(name, LockCode);
            Logger.Debug("[{appid}] Calling On IPC: GlamourerUnlockName", applicationId);
            _glamourerUnlockByName.Invoke(name, LockCode);
        }
        catch (Exception ex)
        {
            _Logger.Warning(ex, "Error during Glamourer RevertByName");
        }
    }

    private void GlamourerChanged(nint address)
    {
        _mareMediator.Publish(new GlamourerChangedMessage(address));
    }
}
