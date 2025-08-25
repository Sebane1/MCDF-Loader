using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using McdfLoader.PlayerData.Handlers;
using McdfLoader.Services;
using McdfLoader.Services.Mediator;
using McdfLoader.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace McdfLoader.Interop.Ipc;

public class RedrawManager
{
    private readonly McdfMediator _McdfMediator;
    private readonly DalamudUtilService _dalamudUtil;
    private readonly ConcurrentDictionary<nint, bool> _penumbraRedrawRequests = [];
    private CancellationTokenSource _disposalCts = new();

    public SemaphoreSlim RedrawSemaphore { get; init; } = new(2, 2);

    public RedrawManager(McdfMediator McdfMediator, DalamudUtilService dalamudUtil)
    {
        _McdfMediator = McdfMediator;
        _dalamudUtil = dalamudUtil;
    }

    public async Task PenumbraRedrawInternalAsync( GameObjectHandler handler, Guid applicationId, Action<ICharacter> action, CancellationToken token)
    {
        _McdfMediator.Publish(new PenumbraStartRedrawMessage(handler.Address));

        _penumbraRedrawRequests[handler.Address] = true;

        try
        {
            using CancellationTokenSource cancelToken = new CancellationTokenSource();
            using CancellationTokenSource combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken.Token, token, _disposalCts.Token);
            var combinedToken = combinedCts.Token;
            cancelToken.CancelAfter(TimeSpan.FromSeconds(15));
            await handler.ActOnFrameworkAfterEnsureNoDrawAsync(action, combinedToken).ConfigureAwait(false);

            if (!_disposalCts.Token.IsCancellationRequested)
                await _dalamudUtil.WaitWhileCharacterIsDrawing(handler, applicationId, 30000, combinedToken).ConfigureAwait(false);
        }
        finally
        {
            _penumbraRedrawRequests[handler.Address] = false;
            _McdfMediator.Publish(new PenumbraEndRedrawMessage(handler.Address));
        }
    }

    internal void Cancel()
    {
        _disposalCts = _disposalCts.CancelRecreate();
    }
}
