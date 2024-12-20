using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.Services.Mediator;

public abstract class DisposableMediatorSubscriberBase : MediatorSubscriberBase, IDisposable
{
    protected DisposableMediatorSubscriberBase(IPluginLog Logger, McdfMediator mediator) : base(Logger, mediator)
    {
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Logger.Debug("Disposing {type} ({this})", GetType().Name, this);
        UnsubscribeAll();
    }
}