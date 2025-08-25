using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using McdfLoader;
using McdfLoader.FileCache;
using McdfLoader.Interop;
using McdfLoader.Interop.Ipc;
using McdfLoader.McdfConfiguration;
using McdfLoader.McdfConfiguration.Configurations;
using McdfLoader.PlayerData.Export;
using McdfLoader.PlayerData.Factories;
using McdfLoader.PlayerData.Pairs;
using McdfLoader.PlayerData.Services;
using McdfLoader.Services;
using McdfLoader.Services.Events;
using McdfLoader.Services.Mediator;
using McdfDataImporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;

namespace McdfLoader;

public sealed class EntryPoint
{
    private readonly IHost _host;
    private static IServiceCollection _collection;

    public static IPluginLog PluginLog { get; set; }

    public EntryPoint(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IDataManager gameData,
        IFramework framework, IObjectTable objectTable, IClientState clientState, ICondition condition, IChatGui chatGui,
        IGameGui gameGui, IDtrBar dtrBar, IPluginLog pluginLog, ITargetManager targetManager, INotificationManager notificationManager,
        ITextureProvider textureProvider, IContextMenu contextMenu, IGameInteropProvider gameInteropProvider, string path)
    {
        AppearanceAccessUtils.CacheLocation = path;
        PluginLog = pluginLog;
        if (!Directory.Exists(pluginInterface.ConfigDirectory.FullName))
            Directory.CreateDirectory(pluginInterface.ConfigDirectory.FullName);
        var traceDir = Path.Join(pluginInterface.ConfigDirectory.FullName, "tracelog");
        if (!Directory.Exists(traceDir))
            Directory.CreateDirectory(traceDir);

        foreach (var file in Directory.EnumerateFiles(traceDir)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc).Skip(9))
        {
            int attempts = 0;
            bool deleted = false;
            while (!deleted && attempts < 5)
            {
                try
                {
                    file.Delete();
                    deleted = true;
                }
                catch
                {
                    attempts++;
                    Thread.Sleep(500);
                }
            }
        }

        _host = new HostBuilder()
        .UseContentRoot(pluginInterface.ConfigDirectory.FullName)
        .ConfigureLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddFile(Path.Combine(traceDir, $"Mcdf-trace-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log"), (opt) =>
            {
                opt.Append = true;
                opt.RollingFilesConvention = FileLoggerOptions.FileRollingConvention.Ascending;
                opt.MinLevel = LogLevel.Trace;
                opt.FileSizeLimitBytes = 50 * 1024 * 1024;
            });
            lb.SetMinimumLevel(LogLevel.Trace);
        })
        .ConfigureServices(collection =>
        {
            collection.AddSingleton<FileDialogManager>();
            // add Mcdf related singletons
            collection.AddSingleton<McdfMediator>();
            collection.AddSingleton<FileCacheManager>();
            collection.AddSingleton<McdfCharaFileManager>();
            collection.AddSingleton<PerformanceCollectorService>();
            collection.AddSingleton<McdfDataLoader>();
            collection.AddSingleton<GameObjectHandlerFactory>();
            collection.AddSingleton<XivDataAnalyzer>();
            collection.AddSingleton<FileCompactor>();
            collection.AddSingleton((s) => new IpcProvider(
                pluginInterface,
                s.GetRequiredService<McdfCharaFileManager>(), s.GetRequiredService<DalamudUtilService>(),
                s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new DalamudUtilService(
                clientState, objectTable, framework, gameGui, condition, gameData, targetManager, s.GetRequiredService<McdfMediator>(), s.GetRequiredService<PerformanceCollectorService>()));
            collection.AddSingleton((s) => new IpcCallerPenumbra( pluginInterface,
    s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>(), s.GetRequiredService<RedrawManager>()));
            collection.AddSingleton((s) => new IpcCallerGlamourer( pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>(), s.GetRequiredService<RedrawManager>()));
            collection.AddSingleton((s) => new IpcCallerCustomize( pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton<RedrawManager>();
            collection.AddSingleton((s) => new IpcCallerPenumbra( pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>(), s.GetRequiredService<RedrawManager>()));
            collection.AddSingleton((s) => new IpcCallerGlamourer( pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>(), s.GetRequiredService<RedrawManager>()));
            collection.AddSingleton((s) => new IpcCallerCustomize( pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new IpcCallerHeels(pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new IpcCallerHonorific(pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new IpcCallerMoodles(pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new IpcCallerPetNames(pluginInterface,
                s.GetRequiredService<DalamudUtilService>(), s.GetRequiredService<McdfMediator>()));
            collection.AddSingleton((s) => new IpcManager(
                s.GetRequiredService<McdfMediator>(), s.GetRequiredService<IpcCallerPenumbra>(), s.GetRequiredService<IpcCallerGlamourer>(),
                s.GetRequiredService<IpcCallerCustomize>(), s.GetRequiredService<IpcCallerHeels>(), s.GetRequiredService<IpcCallerHonorific>(),
                s.GetRequiredService<IpcCallerMoodles>(), s.GetRequiredService<IpcCallerPetNames>()));
            collection.AddSingleton((s) => new NotificationService(
                s.GetRequiredService<McdfMediator>(), s.GetRequiredService<DalamudUtilService>(),
                notificationManager, chatGui, s.GetRequiredService<McdfConfigService>()));
            collection.AddSingleton((s) => new McdfConfigService(path));
            collection.AddSingleton((s) => new TransientConfigService(pluginInterface.ConfigDirectory.FullName));
            collection.AddSingleton((s) => new XivDataStorageService(pluginInterface.ConfigDirectory.FullName));
            collection.AddSingleton<IConfigService<IMcdfConfiguration>>(s => s.GetRequiredService<McdfConfigService>());
            collection.AddSingleton<IConfigService<IMcdfConfiguration>>(s => s.GetRequiredService<TransientConfigService>());
            collection.AddSingleton<IConfigService<IMcdfConfiguration>>(s => s.GetRequiredService<XivDataStorageService>());
            collection.AddSingleton<ConfigurationSaveService>();

            // add scoped services
            collection.AddScoped<CacheMonitor>();

            collection.AddScoped<CacheCreationService>();
            collection.AddScoped<TransientResourceManager>();
            collection.AddScoped<PlayerDataFactory>();

            collection.AddHostedService(p => p.GetRequiredService<ConfigurationSaveService>());
            collection.AddHostedService(p => p.GetRequiredService<McdfMediator>());
            collection.AddHostedService(p => p.GetRequiredService<NotificationService>());
            collection.AddHostedService(p => p.GetRequiredService<FileCacheManager>());
            collection.AddHostedService(p => p.GetRequiredService<DalamudUtilService>());
            collection.AddHostedService(p => p.GetRequiredService<PerformanceCollectorService>());
            collection.AddHostedService(p => p.GetRequiredService<IpcProvider>());
            collection.AddHostedService(p => p.GetRequiredService<McdfDataLoader>());
            _collection = collection;
        })
        .Build();

        _ = _host.StartAsync();
    }

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }
}