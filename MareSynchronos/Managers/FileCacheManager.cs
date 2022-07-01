﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Logging;
using MareSynchronos.FileCacheDB;
using MareSynchronos.Utils;

namespace MareSynchronos.Managers
{
    public class FileCacheManager : IDisposable
    {
        private readonly IpcManager _ipcManager;
        private readonly ConcurrentBag<string> _modifiedFiles = new();
        private readonly Configuration _pluginConfiguration;
        private FileSystemWatcher? _cacheDirWatcher;
        private FileSystemWatcher? _penumbraDirWatcher;
        private Task? _rescanTask;
        private CancellationTokenSource _rescanTaskCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource _rescanTaskRunCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource? _scanCancellationTokenSource;
        private Task? _scanTask;
        public FileCacheManager(IpcManager ipcManager, Configuration pluginConfiguration)
        {
            Logger.Debug("Creating " + nameof(FileCacheManager));

            _ipcManager = ipcManager;
            _pluginConfiguration = pluginConfiguration;

            StartWatchersAndScan();

            _ipcManager.PenumbraInitialized += IpcManagerOnPenumbraInitialized;
            _ipcManager.PenumbraDisposed += IpcManagerOnPenumbraDisposed;
        }

        public long CurrentFileProgress { get; private set; }

        public long FileCacheSize { get; set; }

        public bool IsScanRunning => !_scanTask?.IsCompleted ?? false;

        public long TotalFiles { get; private set; }

        public string WatchedCacheDirectory => (_cacheDirWatcher?.EnableRaisingEvents ?? false) ? _cacheDirWatcher!.Path : "Not watched";

        public string WatchedPenumbraDirectory => (_penumbraDirWatcher?.EnableRaisingEvents ?? false) ? _penumbraDirWatcher!.Path : "Not watched";

        public FileCache Create(string file)
        {
            FileInfo fileInfo = new(file);
            while (IsFileLocked(fileInfo))
            {
                Thread.Sleep(1000);
                Logger.Debug("Waiting for file release " + fileInfo.FullName);
            }
            var sha1Hash = Crypto.GetFileHash(fileInfo.FullName);
            return new FileCache()
            {
                Filepath = fileInfo.FullName.ToLower(),
                Hash = sha1Hash,
                LastModifiedDate = fileInfo.LastWriteTimeUtc.Ticks.ToString(),
            };
        }

        public void Dispose()
        {
            Logger.Debug("Disposing " + nameof(FileCacheManager));

            _ipcManager.PenumbraInitialized -= IpcManagerOnPenumbraInitialized;
            _ipcManager.PenumbraDisposed -= IpcManagerOnPenumbraDisposed;
            _rescanTaskCancellationTokenSource?.Cancel();
            _rescanTaskRunCancellationTokenSource?.Cancel();
            _scanCancellationTokenSource?.Cancel();

            StopWatchersAndScan();
        }

        public void StartInitialScan()
        {
            _scanCancellationTokenSource = new CancellationTokenSource();
            _scanTask = Task.Run(() => StartFileScan(_scanCancellationTokenSource.Token));
        }

        public void StartWatchers()
        {
            if (!_ipcManager.Initialized || !_pluginConfiguration.HasValidSetup()) return;
            Logger.Verbose("Starting File System Watchers");
            _penumbraDirWatcher?.Dispose();
            _cacheDirWatcher?.Dispose();

            _penumbraDirWatcher = new FileSystemWatcher(_ipcManager.PenumbraModDirectory()!)
            {
                IncludeSubdirectories = true,
            };
            _penumbraDirWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
            _penumbraDirWatcher.Deleted += OnModified;
            _penumbraDirWatcher.Changed += OnModified;
            _penumbraDirWatcher.Renamed += OnModified;
            _penumbraDirWatcher.Filters.Add("*.mtrl");
            _penumbraDirWatcher.Filters.Add("*.mdl");
            _penumbraDirWatcher.Filters.Add("*.tex");
            _penumbraDirWatcher.Error += (sender, args) => PluginLog.Error(args.GetException(), "Error in Penumbra Dir Watcher");
            _penumbraDirWatcher.EnableRaisingEvents = true;

            _cacheDirWatcher = new FileSystemWatcher(_pluginConfiguration.CacheFolder)
            {
                IncludeSubdirectories = false,
            };
            _cacheDirWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
            _cacheDirWatcher.Deleted += OnModified;
            _cacheDirWatcher.Changed += OnModified;
            _cacheDirWatcher.Renamed += OnModified;
            _cacheDirWatcher.Filters.Add("*");
            _cacheDirWatcher.Error +=
                (sender, args) => PluginLog.Error(args.GetException(), "Error in Cache Dir Watcher");
            _cacheDirWatcher.EnableRaisingEvents = true;

            Task.Run(RecalculateFileCacheSize);
        }

        private void IpcManagerOnPenumbraDisposed(object? sender, EventArgs e)
        {
            StopWatchersAndScan();
        }

        private void IpcManagerOnPenumbraInitialized(object? sender, EventArgs e)
        {
            StartWatchersAndScan();
        }

        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch
            {
                return true;
            }

            return false;
        }

        private void OnModified(object sender, FileSystemEventArgs e)
        {
            _modifiedFiles.Add(e.FullPath);
            _ = StartRescan();
        }

        private void RecalculateFileCacheSize()
        {
            FileCacheSize = 0;
            foreach (var file in Directory.EnumerateFiles(_pluginConfiguration.CacheFolder))
            {
                try
                {
                    FileCacheSize += new FileInfo(file).Length;
                }
                catch
                {
                    // whatever
                }
            }
        }

        public async Task StartRescan(bool force = false)
        {
            _rescanTaskRunCancellationTokenSource.Cancel();
            _rescanTaskRunCancellationTokenSource = new CancellationTokenSource();
            var token = _rescanTaskRunCancellationTokenSource.Token;
            if (!force)
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            while ((!_rescanTask?.IsCompleted ?? false) && !token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }

            if (token.IsCancellationRequested) return;

            PluginLog.Debug("File changes detected, scanning the changes ");

            if (!_modifiedFiles.Any()) return;

            _rescanTask = Task.Run(async () =>
            {
                var listCopy = _modifiedFiles.ToList();
                _modifiedFiles.Clear();
                await using var db = new FileCacheContext();
                foreach (var item in listCopy.Distinct())
                {

                    var fi = new FileInfo(item);
                    if (!fi.Exists)
                    {
                        PluginLog.Verbose("Removed: " + item);

                        db.RemoveRange(db.FileCaches.Where(f => f.Filepath.ToLower() == item.ToLower()));
                    }
                    else
                    {
                        PluginLog.Verbose("Changed :" + item);
                        var fileCache = Create(item);
                        db.RemoveRange(db.FileCaches.Where(f => f.Hash == fileCache.Hash));
                        await db.AddAsync(fileCache, _rescanTaskCancellationTokenSource.Token);
                    }
                }

                await db.SaveChangesAsync(_rescanTaskCancellationTokenSource.Token);

                RecalculateFileCacheSize();
            }, _rescanTaskCancellationTokenSource.Token);
        }

        private async Task StartFileScan(CancellationToken ct)
        {
            _scanCancellationTokenSource = new CancellationTokenSource();
            var penumbraDir = _ipcManager.PenumbraModDirectory()!;
            Logger.Debug("Getting files from " + penumbraDir + " and " + _pluginConfiguration.CacheFolder);
            var scannedFiles = new ConcurrentDictionary<string, bool>(
                Directory.EnumerateFiles(penumbraDir, "*.*", SearchOption.AllDirectories)
                                .Select(s => s.ToLowerInvariant())
                                .Where(f => f.Contains(@"\chara\"))
                                .Concat(Directory.EnumerateFiles(_pluginConfiguration.CacheFolder, "*.*", SearchOption.AllDirectories)
                                    .Select(s => s.ToLowerInvariant()))
                                .Where(f => (f.EndsWith(".tex", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mtrl", StringComparison.OrdinalIgnoreCase)))
                                .Select(p => new KeyValuePair<string, bool>(p, false)));
            List<FileCache> fileCaches;
            await using (var db = new FileCacheContext())
                fileCaches = db.FileCaches.ToList();

            TotalFiles = scannedFiles.Count;

            var fileCachesToDelete = new ConcurrentBag<FileCache>();
            var fileCachesToAdd = new ConcurrentBag<FileCache>();

            Logger.Debug("Getting file list from Database");
            // scan files from database
            Parallel.ForEach(fileCaches, new ParallelOptions()
            {
                MaxDegreeOfParallelism = _pluginConfiguration.MaxParallelScan,
                CancellationToken = ct,
            },
            cache =>
            {
                if (ct.IsCancellationRequested) return;
                if (!File.Exists(cache.Filepath))
                {
                    fileCachesToDelete.Add(cache);
                }
                else
                {
                    if (scannedFiles.ContainsKey(cache.Filepath))
                    {
                        scannedFiles[cache.Filepath] = true;
                    }
                    FileInfo fileInfo = new(cache.Filepath);
                    if (fileInfo.LastWriteTimeUtc.Ticks == long.Parse(cache.LastModifiedDate)) return;
                    fileCachesToAdd.Add(Create(cache.Filepath));
                    fileCachesToDelete.Add(cache);
                }

                var files = CurrentFileProgress;
                Interlocked.Increment(ref files);
                CurrentFileProgress = files;
            });

            if (ct.IsCancellationRequested) return;

            // scan new files
            Parallel.ForEach(scannedFiles.Where(c => c.Value == false), new ParallelOptions()
            {
                MaxDegreeOfParallelism = _pluginConfiguration.MaxParallelScan,
                CancellationToken = ct
            },
            file =>
            {
                fileCachesToAdd.Add(Create(file.Key));

                var files = CurrentFileProgress;
                Interlocked.Increment(ref files);
                CurrentFileProgress = files;
            });

            if (fileCachesToAdd.Any() || fileCachesToDelete.Any())
            {
                await using FileCacheContext db = new();

                Logger.Debug("Found " + fileCachesToAdd.Count + " additions and " + fileCachesToDelete.Count + " deletions");
                try
                {
                    foreach (var deletion in fileCachesToDelete)
                    {
                        var entry = db.FileCaches.SingleOrDefault(f =>
                            f.Hash == deletion.Hash && f.Filepath == deletion.Filepath);
                        if (entry != null)
                            db.FileCaches.Remove(entry);
                    }
                    await db.SaveChangesAsync(ct);
                    foreach (var entry in fileCachesToAdd)
                    {
                        try
                        {
                            db.FileCaches.Add(entry);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, ex.Message);
                }
            }

            Logger.Debug("Scan complete");
            TotalFiles = 0;
            CurrentFileProgress = 0;

            if (!_pluginConfiguration.InitialScanComplete)
            {
                _pluginConfiguration.InitialScanComplete = true;
                _pluginConfiguration.Save();
            }
        }

        private void StartWatchersAndScan()
        {
            if (!_ipcManager.Initialized || !_pluginConfiguration.HasValidSetup()) return;
            Logger.Verbose("Penumbra is active, configuration is valid, starting watchers and scan");
            StartWatchers();
            StartInitialScan();
        }
        private void StopWatchersAndScan()
        {
            _cacheDirWatcher?.Dispose();
        }
    }
}