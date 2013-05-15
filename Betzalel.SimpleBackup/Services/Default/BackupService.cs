﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Extensions;
using Ionic.Zip;
using Ionic.Zlib;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupService : IBackupService
  {
    private readonly ILog _log;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IBackupHistoryService _backupHistoryService;
    private readonly IBackupStorageService _backupStorageService;

    private readonly string _tempDirectory;
    private readonly string[] _pathsToBackup;
    private readonly string[] _pathsToExclude;
    private readonly string[] _fileTypesToExclude;

    public BackupService(
      ILog log,
      ISettingsProvider settingsProvider,
      IBackupHistoryService backupHistoryService,
      IBackupStorageService backupStorageService)
    {
      _log = log;
      _settingsProvider = settingsProvider;
      _backupHistoryService = backupHistoryService;
      _backupStorageService = backupStorageService;

      _tempDirectory = _settingsProvider.GetSetting<string>("TempDirectory");

      var backupPaths = _settingsProvider.GetSetting<string>("BackupPaths");
      var excludedBackupPaths = _settingsProvider.GetSetting<string>("ExcludedBackupPaths");
      var excludedFileTypes = _settingsProvider.GetSetting<string>("ExcludedFileTypes");

      if (backupPaths.Length == 0)
        throw new Exception("No Backup Paths configured.");

      _pathsToBackup = backupPaths.Split(',').Select(p => p.Trim().TrimEnd('\\', '/')).ToArray();
      _pathsToExclude = excludedBackupPaths.Split(',').Select(p => p.Trim().TrimEnd('\\', '/')).ToArray();
      _fileTypesToExclude = excludedFileTypes.Split(',').Select(p => p.Trim()).ToArray();
    }

    public void StartBackup()
    {
      var backupStarted = DateTime.Now;

      if (!ValidateBackupDirectories(_pathsToBackup))
        return;

      Directory.CreateDirectory(_tempDirectory);

      BackupHistoryType backupType;

      var backedupFilePaths = CreateBackupFiles(out backupType);

      var uploadTime = Stopwatch.StartNew();

      _backupStorageService.UploadBackupFilesToFtp();

      _backupHistoryService.AddBackupHistoryEntry(
        backupType, backupStarted, DateTime.Now, uploadTime.Elapsed, backedupFilePaths);

      Directory.Delete(_tempDirectory, true);

      _log.Debug("Temp directory removed.");
    }

    private bool ValidateBackupDirectories(string[] backupPathsParts)
    {
      _log.Debug("Validating backup directories...");

      var missingDirectories =
        backupPathsParts.Where(backupPathsPart => !Directory.Exists(backupPathsPart));

      if (missingDirectories.Any())
      {
        _log.Error("Could not find directories: " + missingDirectories.ToStringList() + ".");

        return false;
      }

      if (Directory.Exists(_tempDirectory))
      {
        _log.Debug("Deleting old temp dir...");

        Directory.Delete(_tempDirectory, true);
      }

      return true;
    }

    private string[] CreateBackupFiles(out BackupHistoryType backupType)
    {
      var latestFullBackup = _backupHistoryService.GetLatestFullBackupDate();

      var minimumDaysBetweenFullBackups =
        _settingsProvider.GetSetting<int>("MinimumDaysBetweenFullBackups");

      if (!latestFullBackup.HasValue ||
        DateTime.Now.AddDays(minimumDaysBetweenFullBackups) <= latestFullBackup.Value)
      {
        backupType = BackupHistoryType.Full;
      }
      else
      {
        backupType = BackupHistoryType.Differential;
      }

      _log.Info("Starting " + backupType + " backup...");

      List<string> backedupFilePaths;
      var totalBackedupFilePaths = new List<string>();

      for (var i = 0; i < _pathsToBackup.Length; i++)
      {
        var pathToBackup = _pathsToBackup[i];

        _log.Info("Searching files to backup at \"" + pathToBackup + "\"...");

        try
        {
          var tempBackupFileName =
            _tempDirectory + "\\Backup" + (i + 1) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip";

          using (var backupFile = new ZipFile(tempBackupFileName, Encoding.UTF8))
          {
            _log.Debug("Adding directory to backup " + pathToBackup + " directory...");

            backupFile.CompressionMethod = CompressionMethod.BZip2;
            backupFile.CompressionLevel = CompressionLevel.BestCompression;

            switch (backupType)
            {
              case BackupHistoryType.Full:
                backedupFilePaths = AddFilesToBackup(backupFile, BackupHistoryType.Full, pathToBackup);
                break;
              case BackupHistoryType.Differential:
                backedupFilePaths = AddFilesToBackup(backupFile, BackupHistoryType.Differential, pathToBackup);
                break;
              default:
                throw new ArgumentOutOfRangeException("backupType");
            }

            if (backedupFilePaths.Count == 0)
            {
              _log.Info("No files needed to backup.");

              continue;
            }

            _log.Info("Backed up " + backupFile.Count + " files.");

            totalBackedupFilePaths.AddRange(backedupFilePaths);

            backupFile.Save();
          }
        }
        catch (Exception e)
        {
          _log.Error("Failed to backup " + pathToBackup + ".", e);
        }
      }

      return totalBackedupFilePaths.ToArray();
    }

    private List<string> AddFilesToBackup(ZipFile backupFile, BackupHistoryType backupHistoryType, string pathToBackup)
    {
      var backedupFilePaths = new List<string>();

      var latestBackup = _backupHistoryService.GetLatestBackupDate();

      var backupPathInfo = new DirectoryInfo(pathToBackup);

      var filesToBackup = backupPathInfo.GetFiles("*.*", SearchOption.AllDirectories).AsEnumerable();

      if (backupHistoryType == BackupHistoryType.Differential && latestBackup.HasValue)
        filesToBackup = filesToBackup.Where(f => f.LastWriteTime > latestBackup);

      if (_pathsToExclude.Any())
        filesToBackup =
          filesToBackup.Where(
            f => !_pathsToExclude.Contains(f.DirectoryName) && !_fileTypesToExclude.Contains(f.Extension.TrimStart('.')));

      foreach (var fileToBackup in filesToBackup)
      {
        backupFile.AddFile(
          fileToBackup.FullName, fileToBackup.DirectoryName.Substring(pathToBackup.Length));

        backedupFilePaths.Add(fileToBackup.FullName);
      }

      return backedupFilePaths;
    }
  }
}