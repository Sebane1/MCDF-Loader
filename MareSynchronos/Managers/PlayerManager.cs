﻿using MareSynchronos.Factories;
using MareSynchronos.Models;
using MareSynchronos.Utils;
using MareSynchronos.WebAPI;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MareSynchronos.API;
using Penumbra.GameData.Structs;

namespace MareSynchronos.Managers
{
    public delegate void PlayerHasChanged(CharacterCacheDto characterCache);

    public class PlayerManager : IDisposable
    {
        private readonly ApiController _apiController;
        private readonly CharacterDataFactory _characterDataFactory;
        private readonly DalamudUtil _dalamudUtil;
        private readonly IpcManager _ipcManager;
        public event PlayerHasChanged? PlayerHasChanged;
        public bool SendingData { get; private set; }
        public CharacterData? LastSentCharacterData { get; private set; }

        private CancellationTokenSource? _playerChangedCts;
        private DateTime _lastPlayerObjectCheck;
        private CharacterEquipment? _currentCharacterEquipment;

        public PlayerManager(ApiController apiController, IpcManager ipcManager,
            CharacterDataFactory characterDataFactory, DalamudUtil dalamudUtil)
        {
            Logger.Debug("Creating " + nameof(PlayerManager));

            _apiController = apiController;
            _ipcManager = ipcManager;
            _characterDataFactory = characterDataFactory;
            _dalamudUtil = dalamudUtil;

            _apiController.Connected += ApiController_Connected;
            _apiController.Disconnected += ApiController_Disconnected;

            Logger.Debug("Watching Player, ApiController is Connected: " + _apiController.IsConnected);
            if (_apiController.IsConnected)
            {
                ApiController_Connected(null, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Logger.Debug("Disposing " + nameof(PlayerManager));

            _apiController.Connected -= ApiController_Connected;
            _apiController.Disconnected -= ApiController_Disconnected;

            _ipcManager.PenumbraRedrawEvent -= IpcManager_PenumbraRedrawEvent;
            _dalamudUtil.FrameworkUpdate -= DalamudUtilOnFrameworkUpdate;
        }

        private void DalamudUtilOnFrameworkUpdate()
        {
            if (!_dalamudUtil.IsPlayerPresent || !_ipcManager.Initialized || !_apiController.IsConnected) return;

            if (DateTime.Now < _lastPlayerObjectCheck.AddSeconds(0.25)) return;

            if (_dalamudUtil.IsPlayerPresent && !_currentCharacterEquipment!.CompareAndUpdate(_dalamudUtil.PlayerCharacter))
            {
                OnPlayerChanged();
            }

            _lastPlayerObjectCheck = DateTime.Now;
        }

        private void ApiController_Connected(object? sender, EventArgs args)
        {
            Logger.Debug("ApiController Connected");

            _ipcManager.PenumbraRedrawEvent += IpcManager_PenumbraRedrawEvent;
            _dalamudUtil.FrameworkUpdate += DalamudUtilOnFrameworkUpdate;

            _currentCharacterEquipment = new CharacterEquipment(_dalamudUtil.PlayerCharacter);
            PlayerChanged();
        }

        private void ApiController_Disconnected(object? sender, EventArgs args)
        {
            Logger.Debug(nameof(ApiController_Disconnected));

            _ipcManager.PenumbraRedrawEvent -= IpcManager_PenumbraRedrawEvent;
            _dalamudUtil.FrameworkUpdate -= DalamudUtilOnFrameworkUpdate;
        }

        private async Task<CharacterData> CreateFullCharacterCache(CancellationToken token)
        {
            var cache = _characterDataFactory.BuildCharacterData();

            await Task.Run(async () =>
            {
                while (!cache.IsReady && !token.IsCancellationRequested)
                {
                    await Task.Delay(50, token);
                }

                if (token.IsCancellationRequested) return;

                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);

                cache.CacheHash = Crypto.GetHash(json);
            }, token);

            return cache;
        }

        private void IpcManager_PenumbraRedrawEvent(object? objectTableIndex, EventArgs e)
        {
            var player = _dalamudUtil.GetPlayerCharacterFromObjectTableByIndex((int)objectTableIndex!);
            if (player != null && player.Name.ToString() != _dalamudUtil.PlayerName) return;
            Logger.Debug("Penumbra Redraw Event for " + _dalamudUtil.PlayerName);
            PlayerChanged();
        }

        private void PlayerChanged()
        {
            Logger.Debug("Player changed: " + _dalamudUtil.PlayerName);
            _playerChangedCts?.Cancel();
            _playerChangedCts = new CancellationTokenSource();
            var token = _playerChangedCts.Token;

            // fix for redraw from anamnesis
            while ((!_dalamudUtil.IsPlayerPresent || _dalamudUtil.PlayerName == "--") && !token.IsCancellationRequested)
            {
                Logger.Debug("Waiting Until Player is Present");
                Thread.Sleep(100);
            }

            if (token.IsCancellationRequested)
            {
                Logger.Debug("Cancelled");
                return;
            }

            if (!_ipcManager.Initialized)
            {
                Logger.Warn("Penumbra not active, doing nothing.");
                return;
            }

            Task.Run(async () =>
            {
                SendingData = true;
                int attempts = 0;
                while (!_apiController.IsConnected && attempts < 10 && !token.IsCancellationRequested)
                {
                    Logger.Warn("No connection to the API");
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                    attempts++;
                }

                if (attempts == 10 || token.IsCancellationRequested) return;

                Stopwatch st = Stopwatch.StartNew();
                _dalamudUtil.WaitWhileSelfIsDrawing(token);

                var characterCache = await CreateFullCharacterCache(token);

                if (token.IsCancellationRequested) return;

                var cacheDto = characterCache.ToCharacterCacheDto();
                st.Stop();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                Logger.Debug("Elapsed time PlayerChangedTask: " + st.Elapsed);
                if (cacheDto.Hash == (LastSentCharacterData?.CacheHash ?? "-"))
                {
                    Logger.Debug("Not sending data, already sent");
                    return;
                }

                LastSentCharacterData = characterCache;
                PlayerHasChanged?.Invoke(cacheDto);
                SendingData = false;

            }, token);
        }

        private void OnPlayerChanged()
        {
            Task.Run(() =>
            {
                Logger.Debug("Watcher: PlayerChanged");
                PlayerChanged();
            });
        }


    }
}