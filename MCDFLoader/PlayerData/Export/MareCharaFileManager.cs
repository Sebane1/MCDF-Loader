using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using LZ4;
using MareSynchronos.API.Data.Enum;
using MareSynchronos.FileCache;
using MareSynchronos.Interop.Ipc;
using MareSynchronos.MareConfiguration;
using MareSynchronos.PlayerData.Factories;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using MareSynchronos.Utils;
using McdfDataImporter;
using Microsoft.Extensions.Logging;
using RoleplayingVoiceDalamud.Glamourer;
using System.Text;
using CharacterData = MareSynchronos.API.Data.CharacterData;

namespace MareSynchronos.PlayerData.Export;

public class MareCharaFileManager : DisposableMediatorSubscriberBase
{
    private readonly MareConfigService _configService;
    private readonly DalamudUtilService _dalamudUtil;
    private readonly MareCharaFileDataFactory _factory;
    private readonly GameObjectHandlerFactory _gameObjectHandlerFactory;
    private readonly IpcManager _ipcManager;
    private readonly IPluginLog _Logger;
    private readonly FileCacheManager _manager;
    private int _globalFileCounter = 0;
    private bool _isInGpose = true;
    private CharacterData _characterData;
    public event EventHandler<Tuple<IGameObject, long, MareCharaFileHeader>> OnMcdfFailed;
    Dictionary<string, EventHandler> pastCollections = new Dictionary<string, EventHandler>();
    Dictionary<string, string> pastAppearanceType = new Dictionary<string, string>();
    private bool _resettingOldAppearance;
    private string originalPlayerAppearanceString;
    private MareCharaFileData playerCharaFileData;
    private CharacterCustomization playerCustomization;

    public MareCharaFileManager(GameObjectHandlerFactory gameObjectHandlerFactory,
        FileCacheManager manager, IpcManager ipcManager, MareConfigService configService, DalamudUtilService dalamudUtil,
        McdfMediator mediator) : base(mediator)
    {
        _factory = new(manager);
        _Logger = EntryPoint.PluginLog;
        _gameObjectHandlerFactory = gameObjectHandlerFactory;
        _manager = manager;
        _ipcManager = ipcManager;
        _configService = configService;
        _dalamudUtil = dalamudUtil;
        Mediator.Subscribe<CharacterDataCreatedMessage>(this, (x) => PlayerManagerOnPlayerHasChanged(x.CharacterData));
        Mediator.Subscribe<GposeStartMessage>(this, _ => _isInGpose = true);
        Mediator.Subscribe<GposeEndMessage>(this, async _ =>
        {
        });
    }

    private void PlayerManagerOnPlayerHasChanged(CharacterData characterData)
    {
        _characterData = characterData;
    }

    public bool CurrentlyWorking { get; private set; } = false;

    public CharacterCustomization GetGlamourerCustomization()
    {
        var playerCharaFileData = _factory.Create("description", _characterData);
        var playerCustomization = CharacterCustomization.ReadCustomization(playerCharaFileData.GlamourerData);
        return playerCustomization;
    }
    public async Task ApplyStandaloneGlamourerString(IGameObject? charaTarget, string appearance, int appearanceApplicationType)
    {
        string pastAppearanceValue = "";
        if (pastAppearanceType.ContainsKey(charaTarget.Name.TextValue))
        {
            pastAppearanceValue = pastAppearanceType[charaTarget.Name.TextValue];
        }

        if (charaTarget.ObjectIndex == 0)
        {
            if (pastCollections.ContainsKey(charaTarget.Name.TextValue) && pastAppearanceValue == "modded")
            {
                _resettingOldAppearance = true;
                pastCollections[charaTarget.Name.TextValue]?.Invoke(this, EventArgs.Empty);
                while (_resettingOldAppearance)
                {
                    Thread.Sleep(1000);
                }
                pastCollections.Remove(charaTarget.Name.TextValue);
            }
            else
            {
                playerCharaFileData = _factory.Create("description", _characterData);
                playerCustomization = CharacterCustomization.ReadCustomization(playerCharaFileData.GlamourerData);
                originalPlayerAppearanceString = playerCharaFileData.GlamourerData;
            }
        }
        pastAppearanceType[charaTarget.Name.TextValue] = "glamoured";
        var applicationType = (AppearanceSwapType)appearanceApplicationType;

        bool glamourerCanBeApplied = applicationType == AppearanceSwapType.EntireAppearance || applicationType == AppearanceSwapType.OnlyGlamourerData
            || applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity || applicationType == AppearanceSwapType.PreserveAllPhysicalTraits ||
               applicationType == AppearanceSwapType.PreserveRace;

        if (charaTarget == null) return;
        CurrentlyWorking = true;
        try
        {
            CancellationTokenSource disposeCts = new();
            var applicationId = Guid.NewGuid();
            var coll = Guid.NewGuid();
            GameObjectHandler tempHandler = await _gameObjectHandlerFactory.Create(ObjectKind.Player,
                () => _dalamudUtil.GetCharacterFromObjectTableByName(charaTarget.Name.ToString())?.Address ?? IntPtr.Zero, isWatched: false).ConfigureAwait(false);

            if (glamourerCanBeApplied)
            {
                string glamourerData = appearance;

                if ((applicationType == AppearanceSwapType.PreserveAllPhysicalTraits ||
                    applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity || applicationType == AppearanceSwapType.PreserveRace) && charaTarget.ObjectIndex == 0)
                {
                    var appearanceCustomization = CharacterCustomization.ReadCustomization(glamourerData);
                    var customizeData = appearanceCustomization.Customize;
                    if (applicationType == AppearanceSwapType.PreserveAllPhysicalTraits)
                    {
                        appearanceCustomization.Customize = playerCustomization.Customize;
                        glamourerData = appearanceCustomization.ToBase64();
                    }
                    else if (applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity)
                    {
                        appearanceCustomization.Customize.Gender = playerCustomization.Customize.Gender;
                        glamourerData = appearanceCustomization.ToBase64();
                    }
                    else if (applicationType == AppearanceSwapType.PreserveRace)
                    {
                        appearanceCustomization.Customize.Race = playerCustomization.Customize.Race;
                        appearanceCustomization.Customize.Clan = playerCustomization.Customize.Clan;
                        appearanceCustomization.Customize.SkinColor = playerCustomization.Customize.SkinColor;
                        appearanceCustomization.Customize.Face = playerCustomization.Customize.Face;
                        glamourerData = appearanceCustomization.ToBase64();
                    }
                }

                await _ipcManager.Glamourer.ApplyAllAsync(charaTarget, tempHandler, glamourerData, applicationId, disposeCts.Token, applicationType == AppearanceSwapType.PreserveAllPhysicalTraits, true).ConfigureAwait(false);
                //await _ipcManager.Penumbra.RedrawAsync(tempHandler, applicationId, disposeCts.Token).ConfigureAwait(false);
                //_dalamudUtil.WaitWhileGposeCharacterIsDrawing(charaTarget.Address, 30000);
            }

            Guid? id = Guid.NewGuid();
            string name = charaTarget.Name.TextValue;
            pastCollections[charaTarget.Name.TextValue] = async delegate
        {
            if (glamourerCanBeApplied)
            {
                await _ipcManager.Glamourer.RevertAsync(name, tempHandler, applicationId, disposeCts.Token);
                await Task.Delay(1000);
                if (charaTarget.ObjectIndex == 0)
                {
                    await _ipcManager.Glamourer.ApplyAllAsync(charaTarget, tempHandler, originalPlayerAppearanceString, applicationId, disposeCts.Token);
                }
            }
            _resettingOldAppearance = false;
            pastAppearanceType[charaTarget.Name.TextValue] = "";
            pastCollections.Remove(charaTarget.Name.TextValue);
        };
        }
        catch (Exception ex)
        {
            _Logger.Warning(ex, "Failure to read glamourer string");
        }
        finally
        {
        }
        CurrentlyWorking = false;
    }
    public async Task ApplyMareCharaFile(IGameObject? charaTarget, long expectedLength, MareCharaFileHeader loadedCharaFile, int mcdfApplicationType)
    {
        if (charaTarget.ObjectIndex == 0)
        {
            if (!pastCollections.ContainsKey(charaTarget.Name.TextValue))
            {
                playerCharaFileData = _factory.Create("description", _characterData);
                playerCustomization = CharacterCustomization.ReadCustomization(playerCharaFileData.GlamourerData);
                originalPlayerAppearanceString = playerCharaFileData.GlamourerData;
            }
        }

        pastAppearanceType[charaTarget.Name.TextValue] = "modded";
        var applicationType = (AppearanceSwapType)mcdfApplicationType;

        bool glamourerCanBeApplied = applicationType == AppearanceSwapType.EntireAppearance || applicationType == AppearanceSwapType.OnlyGlamourerData
            || applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity || applicationType == AppearanceSwapType.PreserveAllPhysicalTraits ||
               applicationType == AppearanceSwapType.PreserveRace;

        bool penumbraCanBeApplied = applicationType == AppearanceSwapType.EntireAppearance || applicationType == AppearanceSwapType.OnlyModData
                    || (applicationType != AppearanceSwapType.OnlyGlamourerData && applicationType != AppearanceSwapType.OnlyCustomizeData);

        if (charaTarget == null) return;
        Dictionary<string, string> extractedFiles = new(StringComparer.Ordinal);
        CurrentlyWorking = true;
        try
        {
            if (loadedCharaFile == null || !File.Exists(loadedCharaFile.FilePath)) return;
            var unwrapped = File.OpenRead(loadedCharaFile.FilePath);
            await using (unwrapped.ConfigureAwait(false))
            {
                CancellationTokenSource disposeCts = new();
                using var lz4Stream = new LZ4Stream(unwrapped, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression);
                using var reader = new BinaryReader(lz4Stream);
                MareCharaFileHeader.AdvanceReaderToData(reader);
                _Logger.Debug("Applying to {chara}, expected length of contents: {exp}, stream length: {len}", charaTarget.Name.TextValue, expectedLength, reader.BaseStream.Length);
                extractedFiles = ExtractFilesFromCharaFile(charaTarget.Name.TextValue, loadedCharaFile, reader, expectedLength);
                Dictionary<string, string> fileSwaps = new(StringComparer.Ordinal);
                var applicationId = Guid.NewGuid();
                var coll = Guid.NewGuid();
                if (penumbraCanBeApplied)
                {
                    foreach (var fileSwap in loadedCharaFile.CharaFileData.FileSwaps)
                    {
                        foreach (var path in fileSwap.GamePaths)
                        {
                            fileSwaps.Add(path, fileSwap.FileSwapPath);
                        }
                    }
                    coll = await _ipcManager.Penumbra.CreateTemporaryCollectionAsync(charaTarget.Name.TextValue).ConfigureAwait(false);
                    await _ipcManager.Penumbra.AssignTemporaryCollectionAsync(coll, charaTarget.ObjectIndex).ConfigureAwait(false);
                    await _ipcManager.Penumbra.SetTemporaryModsAsync(applicationId, coll, extractedFiles.Union(fileSwaps).ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal)).ConfigureAwait(false);
                    await _ipcManager.Penumbra.SetManipulationDataAsync(applicationId, coll, loadedCharaFile.CharaFileData.ManipulationData).ConfigureAwait(false);
                }
                GameObjectHandler tempHandler = await _gameObjectHandlerFactory.Create(ObjectKind.Player,
                    () => _dalamudUtil.GetCharacterFromObjectTableByName(charaTarget.Name.ToString())?.Address ?? IntPtr.Zero, isWatched: false).ConfigureAwait(false);

                if (glamourerCanBeApplied)
                {
                    string glamourerData = loadedCharaFile.CharaFileData.GlamourerData;

                    if ((applicationType == AppearanceSwapType.PreserveAllPhysicalTraits ||
                        applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity || applicationType == AppearanceSwapType.PreserveRace) && charaTarget.ObjectIndex == 0)
                    {
                        var mcdfCustomization = CharacterCustomization.ReadCustomization(glamourerData);
                        var customizeData = mcdfCustomization.Customize;
                        if (applicationType == AppearanceSwapType.PreserveAllPhysicalTraits)
                        {
                            mcdfCustomization.Customize = playerCustomization.Customize;
                            glamourerData = mcdfCustomization.ToBase64();
                        }
                        else if (applicationType == AppearanceSwapType.PreserveMasculinityAndFemininity)
                        {
                            mcdfCustomization.Customize.Gender = playerCustomization.Customize.Gender;
                            glamourerData = mcdfCustomization.ToBase64();
                        }
                        else if (applicationType == AppearanceSwapType.PreserveRace)
                        {
                            mcdfCustomization.Customize.Race = playerCustomization.Customize.Race;
                            mcdfCustomization.Customize.Clan = playerCustomization.Customize.Clan;
                            mcdfCustomization.Customize.SkinColor = playerCustomization.Customize.SkinColor;
                            mcdfCustomization.Customize.Face = playerCustomization.Customize.Face;
                            glamourerData = mcdfCustomization.ToBase64();
                        }
                    }

                    await _ipcManager.Glamourer.ApplyAllAsync(charaTarget, tempHandler, glamourerData, applicationId, disposeCts.Token, false, true).ConfigureAwait(false);
                }
                await _ipcManager.Penumbra.RedrawAsync(tempHandler, applicationId, disposeCts.Token).ConfigureAwait(false);
                _dalamudUtil.WaitWhileGposeCharacterIsDrawing(charaTarget.Address, 30000);

                Guid? id = Guid.NewGuid();
                if (applicationType == AppearanceSwapType.OnlyCustomizeData || applicationType == AppearanceSwapType.EntireAppearance)
                {
                    if (!string.IsNullOrEmpty(loadedCharaFile.CharaFileData.CustomizePlusData))
                    {
                        id = await _ipcManager.CustomizePlus.SetBodyScaleAsync(tempHandler.Address, loadedCharaFile.CharaFileData.CustomizePlusData).ConfigureAwait(false);
                    }
                    else
                    {
                        id = await _ipcManager.CustomizePlus.SetBodyScaleAsync(tempHandler.Address, Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))).ConfigureAwait(false);
                    }
                }
                string name = charaTarget.Name.TextValue;
                pastCollections[charaTarget.Name.TextValue] = async delegate
                {
                    if (penumbraCanBeApplied)
                    {
                        await _ipcManager.Penumbra.RemoveTemporaryCollectionAsync(applicationId, coll).ConfigureAwait(false);
                    }
                    if (glamourerCanBeApplied)
                    {
                        await _ipcManager.Glamourer.RevertAsync(name, tempHandler, applicationId, disposeCts.Token);
                        await Task.Delay(1000);
                        if (charaTarget.ObjectIndex == 0)
                        {
                            await _ipcManager.Glamourer.ApplyAllAsync(charaTarget, tempHandler, originalPlayerAppearanceString, applicationId, disposeCts.Token);
                        }
                    }
                    if (applicationType == AppearanceSwapType.OnlyCustomizeData || applicationType == AppearanceSwapType.EntireAppearance)
                    {
                        await _ipcManager.CustomizePlus.RevertByIdAsync(id).ConfigureAwait(false);
                    }
                    _resettingOldAppearance = false;
                    pastAppearanceType[charaTarget.Name.TextValue] = "";
                    pastCollections.Remove(charaTarget.Name.TextValue);
                };
            }
        }
        catch (Exception ex)
        {
            _Logger.Warning(ex, "Failure to read MCDF");
            OnMcdfFailed?.Invoke(this, new Tuple<IGameObject, long, MareCharaFileHeader>(charaTarget, expectedLength, loadedCharaFile));
        }
        finally
        {
            _Logger.Debug("Clearing local files");
        }
        CurrentlyWorking = false;
    }
    public async void RemoveAllTemporaryCollections()
    {
        Task.Run(() =>
        {
            foreach (var item in pastCollections)
            {
                item.Value.Invoke(this, EventArgs.Empty);
                Thread.Sleep(200);
            }
            pastCollections.Clear();
        });
    }
    public async void RemoveTemporaryCollection(string name)
    {
        Task.Run(() =>
        {
            if (pastCollections.ContainsKey(name))
            {
                pastCollections[name].Invoke(this, EventArgs.Empty);
                pastCollections.Remove(name);
            }
        });
    }
    public Tuple<long, MareCharaFileHeader> LoadMareCharaFile(string filePath)
    {
        CurrentlyWorking = true;
        try
        {
            using var unwrapped = File.OpenRead(filePath);
            using var lz4Stream = new LZ4Stream(unwrapped, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression);
            using var reader = new BinaryReader(lz4Stream);
            var loadedCharaFile = MareCharaFileHeader.FromBinaryReader(filePath, reader);

            _Logger.Information("Read Mare Chara File");
            _Logger.Information("Version: {ver}", (loadedCharaFile?.Version ?? -1));
            long expectedLength = 0;
            if (loadedCharaFile != null)
            {
                _Logger.Debug("Data");
                foreach (var item in loadedCharaFile.CharaFileData.FileSwaps)
                {
                    foreach (var gamePath in item.GamePaths)
                    {
                        _Logger.Debug("Swap: {gamePath} => {fileSwapPath}", gamePath, item.FileSwapPath);
                    }
                }

                var itemNr = 0;
                foreach (var item in loadedCharaFile.CharaFileData.Files)
                {
                    itemNr++;
                    expectedLength += item.Length;
                    foreach (var gamePath in item.GamePaths)
                    {
                        _Logger.Debug("File {itemNr}: {gamePath} = {len}", itemNr, gamePath, item.Length.ToByteString());
                    }
                }

                _Logger.Information("Expected length: {expected}", expectedLength.ToByteString());
            }
            return new Tuple<long, MareCharaFileHeader>(expectedLength, loadedCharaFile);
        }
        finally { CurrentlyWorking = false; }
    }

    public void SaveMareCharaFile(string description, string filePath)
    {
        CurrentlyWorking = true;
        var tempFilePath = filePath + ".tmp";

        try
        {
            if (_characterData == null) return;

            var mareCharaFileData = _factory.Create(description, _characterData);
            MareCharaFileHeader output = new(MareCharaFileHeader.CurrentVersion, mareCharaFileData);

            using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var lz4 = new LZ4Stream(fs, LZ4StreamMode.Compress, LZ4StreamFlags.HighCompression);
            using var writer = new BinaryWriter(lz4);
            output.WriteToStream(writer);

            foreach (var item in output.CharaFileData.Files)
            {
                var file = _manager.GetFileCacheByHash(item.Hash)!;
                _Logger.Debug("Saving to MCDF: {hash}:{file}", item.Hash, file.ResolvedFilepath);
                _Logger.Debug("\tAssociated GamePaths:");
                foreach (var path in item.GamePaths)
                {
                    _Logger.Debug("\t{path}", path);
                }
                using var fsRead = File.OpenRead(file.ResolvedFilepath);
                using var br = new BinaryReader(fsRead);
                byte[] buffer = new byte[item.Length];
                br.Read(buffer, 0, item.Length);
                writer.Write(buffer);
            }
            writer.Flush();
            lz4.Flush();
            fs.Flush();
            fs.Close();
            File.Move(tempFilePath, filePath, true);
        }
        catch (Exception ex)
        {
            _Logger.Error(ex, "Failure Saving Mare Chara File, deleting output");
            File.Delete(tempFilePath);
        }
        finally { CurrentlyWorking = false; }
    }

    private Dictionary<string, string> ExtractFilesFromCharaFile(string fileId, MareCharaFileHeader charaFileHeader, BinaryReader reader, long expectedLength)
    {
        long totalRead = 0;
        Dictionary<string, string> gamePathToFilePath = new(StringComparer.Ordinal);
        foreach (var fileData in charaFileHeader.CharaFileData.Files)
        {
            var fileName = Path.Combine(AppearanceAccessUtils.CacheLocation, fileId + "_mcdf_" + _globalFileCounter++ + ".tmp");
            var length = fileData.Length;
            if (!File.Exists(fileName))
            {
                try
                {
                    var bufferSize = length;
                    using var fs = File.OpenWrite(fileName);
                    using var wr = new BinaryWriter(fs);
                    _Logger.Debug("Reading {length} of {fileName}", length.ToByteString(), fileName);
                    var buffer = reader.ReadBytes(bufferSize);
                    wr.Write(buffer);
                    wr.Flush();
                    wr.Close();
                    if (buffer.Length == 0) throw new EndOfStreamException("Unexpected EOF");
                    foreach (var path in fileData.GamePaths)
                    {
                        gamePathToFilePath[path] = fileName;
                        _Logger.Debug("{path} => {fileName} [{hash}]", path, fileName, fileData.Hash);
                    }
                }
                catch
                {

                }
            }
            totalRead += length;
            _Logger.Debug("Read {read}/{expected} bytes", totalRead.ToByteString(), expectedLength.ToByteString());
        }

        return gamePathToFilePath;
    }
    public enum AppearanceSwapType
    {
        EntireAppearance = 0,
        RevertAppearance = 1,
        PreserveRace = 2,
        PreserveMasculinityAndFemininity = 3,
        PreserveAllPhysicalTraits = 4,
        OnlyGlamourerData = 5,
        OnlyCustomizeData = 6,
        OnlyModData = 7
    }
}