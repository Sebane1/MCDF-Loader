using Dalamud.Plugin.Services;
using MareSynchronos.API.Data.Enum;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using Microsoft.Extensions.Logging;

namespace MareSynchronos.PlayerData.Factories;

public class GameObjectHandlerFactory
{
    private readonly DalamudUtilService _dalamudUtilService;
    private readonly IPluginLog _logger;
    private readonly McdfMediator _mareMediator;
    private readonly PerformanceCollectorService _performanceCollectorService;

    public GameObjectHandlerFactory( PerformanceCollectorService performanceCollectorService, McdfMediator mareMediator,
        DalamudUtilService dalamudUtilService)
    {
        _logger = EntryPoint.PluginLog;
        _performanceCollectorService = performanceCollectorService;
        _mareMediator = mareMediator;
        _dalamudUtilService = dalamudUtilService;
    }

    public async Task<GameObjectHandler> Create(ObjectKind objectKind, Func<nint> getAddressFunc, bool isWatched = false)
    {
        return await _dalamudUtilService.RunOnFrameworkThread(() => new GameObjectHandler(
            _performanceCollectorService, _mareMediator, _dalamudUtilService, objectKind, getAddressFunc, isWatched)).ConfigureAwait(false);
    }
}