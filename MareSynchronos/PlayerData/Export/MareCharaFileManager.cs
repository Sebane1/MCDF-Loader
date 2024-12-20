﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
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
using System.Text;
using CharacterData = MareSynchronos.API.Data.CharacterData;

namespace MareSynchronos.PlayerData.Export;

public class MareCharaFileManager : DisposableMediatorSubscriberBase
{
    private readonly MareConfigService _configService;
    private readonly DalamudUtilService _dalamudUtil;
    private readonly MareCharaFileDataFactory _factory;
    private readonly GameObjectHandlerFactory _gameObjectHandlerFactory;
    private readonly Dictionary<string, GameObjectHandler> _gposeGameObjects;
    private readonly List<Guid?> _gposeCustomizeObjects;
    private readonly IpcManager _ipcManager;
    private readonly IPluginLog _Logger;
    private readonly FileCacheManager _manager;
    private int _globalFileCounter = 0;
    private bool _isInGpose = true;

    public MareCharaFileManager(IPluginLog Logger, GameObjectHandlerFactory gameObjectHandlerFactory,
        FileCacheManager manager, IpcManager ipcManager, MareConfigService configService, DalamudUtilService dalamudUtil,
        McdfMediator mediator) : base(Logger, mediator)
    {
        _factory = new(manager);
        _Logger = Logger;
        _gameObjectHandlerFactory = gameObjectHandlerFactory;
        _manager = manager;
        _ipcManager = ipcManager;
        _configService = configService;
        _dalamudUtil = dalamudUtil;
        _gposeGameObjects = [];
        _gposeCustomizeObjects = [];
        Mediator.Subscribe<GposeStartMessage>(this, _ => _isInGpose = true);
        Mediator.Subscribe<GposeEndMessage>(this, async _ =>
        {
        });
    }

    public bool CurrentlyWorking { get; private set; } = false;
    public MareCharaFileHeader? LoadedCharaFile { get; private set; }

    public async Task ApplyMareCharaFile(IGameObject? charaTarget, long expectedLength)
    {
        if (charaTarget == null) return;
        Dictionary<string, string> extractedFiles = new(StringComparer.Ordinal);
        CurrentlyWorking = true;
        try
        {
            if (LoadedCharaFile == null || !File.Exists(LoadedCharaFile.FilePath)) return;
            var unwrapped = File.OpenRead(LoadedCharaFile.FilePath);
            await using (unwrapped.ConfigureAwait(false))
            {
                CancellationTokenSource disposeCts = new();
                using var lz4Stream = new LZ4Stream(unwrapped, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression);
                using var reader = new BinaryReader(lz4Stream);
                MareCharaFileHeader.AdvanceReaderToData(reader);
                _Logger.Debug("Applying to {chara}, expected length of contents: {exp}, stream length: {len}", charaTarget.Name.TextValue, expectedLength, reader.BaseStream.Length);
                extractedFiles = ExtractFilesFromCharaFile(LoadedCharaFile, reader, expectedLength);
                Dictionary<string, string> fileSwaps = new(StringComparer.Ordinal);
                foreach (var fileSwap in LoadedCharaFile.CharaFileData.FileSwaps)
                {
                    foreach (var path in fileSwap.GamePaths)
                    {
                        fileSwaps.Add(path, fileSwap.FileSwapPath);
                    }
                }
                var applicationId = Guid.NewGuid();
                var coll = await _ipcManager.Penumbra.CreateTemporaryCollectionAsync(_Logger, charaTarget.Name.TextValue).ConfigureAwait(false);
                await _ipcManager.Penumbra.AssignTemporaryCollectionAsync(_Logger, coll, charaTarget.ObjectIndex).ConfigureAwait(false);
                await _ipcManager.Penumbra.SetTemporaryModsAsync(_Logger, applicationId, coll, extractedFiles.Union(fileSwaps).ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal)).ConfigureAwait(false);
                await _ipcManager.Penumbra.SetManipulationDataAsync(_Logger, applicationId, coll, LoadedCharaFile.CharaFileData.ManipulationData).ConfigureAwait(false);

                GameObjectHandler tempHandler = await _gameObjectHandlerFactory.Create(ObjectKind.Player,
                    () => _dalamudUtil.GetGposeCharacterFromObjectTableByName(charaTarget.Name.ToString(), _isInGpose)?.Address ?? IntPtr.Zero, isWatched: false).ConfigureAwait(false);

                if (!_gposeGameObjects.ContainsKey(charaTarget.Name.ToString()))
                    _gposeGameObjects[charaTarget.Name.ToString()] = tempHandler;

                await _ipcManager.Glamourer.ApplyAllAsync(_Logger, tempHandler, LoadedCharaFile.CharaFileData.GlamourerData, applicationId, disposeCts.Token).ConfigureAwait(false);
                await _ipcManager.Penumbra.RedrawAsync(_Logger, tempHandler, applicationId, disposeCts.Token).ConfigureAwait(false);
                _dalamudUtil.WaitWhileGposeCharacterIsDrawing(charaTarget.Address, 30000);
                await _ipcManager.Penumbra.RemoveTemporaryCollectionAsync(_Logger, applicationId, coll).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(LoadedCharaFile.CharaFileData.CustomizePlusData))
                {
                    var id = await _ipcManager.CustomizePlus.SetBodyScaleAsync(tempHandler.Address, LoadedCharaFile.CharaFileData.CustomizePlusData).ConfigureAwait(false);
                    _gposeCustomizeObjects.Add(id);
                }
                else
                {
                    var id = await _ipcManager.CustomizePlus.SetBodyScaleAsync(tempHandler.Address, Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))).ConfigureAwait(false);
                    _gposeCustomizeObjects.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            _Logger.Warning(ex, "Failure to read MCDF");
            throw;
        }
        finally
        {
            CurrentlyWorking = false;

            _Logger.Debug("Clearing local files");
            foreach (var file in Directory.EnumerateFiles(CachePath.CacheLocation, "*.tmp"))
            {
                File.Delete(file);
            }
        }
    }

    public void ClearMareCharaFile()
    {
        LoadedCharaFile = null;
    }

    public long LoadMareCharaFile(string filePath)
    {
        CurrentlyWorking = true;
        try
        {
            using var unwrapped = File.OpenRead(filePath);
            using var lz4Stream = new LZ4Stream(unwrapped, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression);
            using var reader = new BinaryReader(lz4Stream);
            LoadedCharaFile = MareCharaFileHeader.FromBinaryReader(filePath, reader);

            _Logger.Information("Read Mare Chara File");
            _Logger.Information("Version: {ver}", (LoadedCharaFile?.Version ?? -1));
            long expectedLength = 0;
            if (LoadedCharaFile != null)
            {
                _Logger.Debug("Data");
                foreach (var item in LoadedCharaFile.CharaFileData.FileSwaps)
                {
                    foreach (var gamePath in item.GamePaths)
                    {
                        _Logger.Debug("Swap: {gamePath} => {fileSwapPath}", gamePath, item.FileSwapPath);
                    }
                }

                var itemNr = 0;
                foreach (var item in LoadedCharaFile.CharaFileData.Files)
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
            return expectedLength;
        }
        finally { CurrentlyWorking = false; }
    }

    public void SaveMareCharaFile(CharacterData? dto, string description, string filePath)
    {
        CurrentlyWorking = true;
        var tempFilePath = filePath + ".tmp";

        try
        {
            if (dto == null) return;

            var mareCharaFileData = _factory.Create(description, dto);
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

    private Dictionary<string, string> ExtractFilesFromCharaFile(MareCharaFileHeader charaFileHeader, BinaryReader reader, long expectedLength)
    {
        long totalRead = 0;
        Dictionary<string, string> gamePathToFilePath = new(StringComparer.Ordinal);
        foreach (var fileData in charaFileHeader.CharaFileData.Files)
        {
            var fileName = Path.Combine(CachePath.CacheLocation, "mare_" + _globalFileCounter++ + ".tmp");
            var length = fileData.Length;
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
            totalRead += length;
            _Logger.Debug("Read {read}/{expected} bytes", totalRead.ToByteString(), expectedLength.ToByteString());
        }

        return gamePathToFilePath;
    }
}