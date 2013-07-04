using System;
using System.Collections.Generic;
using System.Diagnostics;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Scheduler;
using Betzalel.SimpleBackup.Types;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupService : IBackupService
  {
    private readonly ILog _log;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IBackupHistoryService _backupHistoryService;
    private readonly IBackupCompressor _backupCompressor;
    private readonly IBackupStorageService _backupStorageService;
    private readonly Scheduler _scheduler;

    public BackupService(
      ILog log,
      ISettingsProvider settingsProvider,
      IBackupHistoryService backupHistoryService,
      IBackupCompressor backupCompressor,
      IBackupStorageService backupStorageService,
      Scheduler scheduler)
    {
      _log = log;
      _settingsProvider = settingsProvider;
      _backupHistoryService = backupHistoryService;
      _backupCompressor = backupCompressor;
      _backupStorageService = backupStorageService;
      _scheduler = scheduler;
    }

    public void StartBackup()
    {
      _log.Info("Starting backup tasks...");

      _scheduler.AddDailyTask(
        "BackupCompress",
        BackupCompressTask,
        new Time(DateTime.Parse(_settingsProvider.GetSetting<string>("DailyBackupCompressStartTime"))),
        TaskConcurrencyOptions.Skip);

      _scheduler.AddTask(
        "BackupStorage",
        BackupStorageTask,
        TimeSpan.Zero,
        TimeSpan.FromSeconds(_settingsProvider.GetSetting<long>("DailyBackupStorageInterval")),
        TaskConcurrencyOptions.Skip);
    }

    public void StopBackup()
    {
      _log.Info("Stopping backup tasks...");

      _scheduler.RemoveAll();
    }

    private void BackupCompressTask()
    {
      _log.Debug("Backup compress task started...");

      BackupType backupType;
      List<string> backedupFilePaths;
      List<string> storagePendingFilesPaths;

      var compressStarted = DateTime.Now;
      var backupState = BackupState.FileCompressSuccess;

      if (!_backupCompressor.CreateBackupFiles(out backupType, out backedupFilePaths, out storagePendingFilesPaths))
        backupState = BackupState.FileCompressFailed;

      if (backedupFilePaths.Count <= 0 || storagePendingFilesPaths.Count <= 0)
      {
        _log.Info("No files were backed up.");
      }
      else
      {
        _backupHistoryService.AddBackupCompressCompletedEntries(
          backupType,
          compressStarted,
          DateTime.Now,
          backupState,
          backedupFilePaths,
          storagePendingFilesPaths);
      }

      _log.Debug("Backup compress task ended...");
    }

    private void BackupStorageTask()
    {
      _log.Debug("Backup storage task started...");

      var backupEntriesToStorage = _backupHistoryService.GetBackupEntriesToStorage();

      foreach (var backupEntryToStorage in backupEntriesToStorage)
      {
        _log.Debug("Storing entry #" + backupEntryToStorage.Id + "...");

        var backupState = BackupState.Success;

        var storageTime = Stopwatch.StartNew();

        if (!_backupStorageService.ProcessStorageReadyBackupEntry(backupEntryToStorage))
        {
          backupState = BackupState.StorageFailed;
        }
        else
        {
          backupEntryToStorage.StoragePendingFilesPaths = null;
          backupEntryToStorage.StorageEnded = DateTime.Now;
        }

        backupEntryToStorage.TotalStorageTime += (int) Math.Round(storageTime.Elapsed.TotalSeconds);
        backupEntryToStorage.BackupState = backupState;

        _backupHistoryService.UpdateBackupHistoryEntry(backupEntryToStorage);
      }

      _log.Debug("Backup storage task ended...");
    }
  }
}