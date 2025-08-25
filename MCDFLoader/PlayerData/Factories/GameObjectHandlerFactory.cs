using Dalamud.Plugin.Services;
using McdfLoader.API.Data.Enum;
using McdfLoader.PlayerData.Handlers;
using McdfLoader.Services;
using McdfLoader.Services.Mediator;
using Microsoft.Extensions.Logging;

namespace McdfLoader.PlayerData.Factories;

public class GameObjectHandlerFactory
{
    private readonly DalamudUtilService _dalamudUtilService;
    private readonly IPluginLog _logger;
    private readonly McdfMediator _McdfMediator;
    private readonly PerformanceCollectorService _performanceCollectorService;

    public GameObjectHandlerFactory( PerformanceCollectorService performanceCollectorService, McdfMediator McdfMediator,
        DalamudUtilService dalamudUtilService)
    {
        _logger = EntryPoint.PluginLog;
        _performanceCollectorService = performanceCollectorService;
        _McdfMediator = McdfMediator;
        _dalamudUtilService = dalamudUtilService;
    }

    public async Task<GameObjectHandler> Create(ObjectKind objectKind, Func<nint> getAddressFunc, bool isWatched = false)
    {
        return await _dalamudUtilService.RunOnFrameworkThread(() => new GameObjectHandler(
            _performanceCollectorService, _McdfMediator, _dalamudUtilService, objectKind, getAddressFunc, isWatched)).ConfigureAwait(false);
    }
}