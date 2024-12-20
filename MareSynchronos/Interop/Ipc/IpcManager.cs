﻿using Dalamud.Plugin.Services;
using MareSynchronos.Services.Mediator;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.Interop.Ipc;

public sealed partial class IpcManager : DisposableMediatorSubscriberBase
{
    public IpcManager( McdfMediator mediator,
        IpcCallerPenumbra penumbraIpc, IpcCallerGlamourer glamourerIpc, IpcCallerCustomize customizeIpc, IpcCallerHeels heelsIpc,
        IpcCallerHonorific honorificIpc, IpcCallerMoodles moodlesIpc, IpcCallerPetNames ipcCallerPetNames) : base( mediator)
    {
        CustomizePlus = customizeIpc;
        Heels = heelsIpc;
        Glamourer = glamourerIpc;
        Penumbra = penumbraIpc;
        Honorific = honorificIpc;
        Moodles = moodlesIpc;
        PetNames = ipcCallerPetNames;

        if (Initialized)
        {
            Mediator.Publish(new PenumbraInitializedMessage());
        }

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => PeriodicApiStateCheck());

        try
        {
            PeriodicApiStateCheck();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to check for some IPC, plugin not installed?");
        }
    }

    public bool Initialized => Penumbra.APIAvailable && Glamourer.APIAvailable;

    public IpcCallerCustomize CustomizePlus { get; init; }
    public IpcCallerHonorific Honorific { get; init; }
    public IpcCallerHeels Heels { get; init; }
    public IpcCallerGlamourer Glamourer { get; }
    public IpcCallerPenumbra Penumbra { get; }
    public IpcCallerMoodles Moodles { get; }
    public IpcCallerPetNames PetNames { get; }

    private void PeriodicApiStateCheck()
    {
        Penumbra.CheckAPI();
        Penumbra.CheckModDirectory();
        Glamourer.CheckAPI();
        Heels.CheckAPI();
        CustomizePlus.CheckAPI();
        Honorific.CheckAPI();
        Moodles.CheckAPI();
        PetNames.CheckAPI();
    }
}