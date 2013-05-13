using System;
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

      if (backupPaths.Length == 0)
        throw new Exception("No Backup Paths configured.");

      _pathsToBackup = backupPaths.Split(',');
    }

    public void StartBackup()
    {
      var backupStarted = DateTime.Now;

      if (!ValidateBackupDirectories(_pathsToBackup))
        return;

      Directory.CreateDirectory(_tempDirectory);

      BackupHistoryType backupType;

      var numberOfFilesBackedup = CreateBackupFiles(out backupType);

      var uploadTime = Stopwatch.StartNew();

      _backupStorageService.UploadBackupFilesToFtp();

      _backupHistoryService.AddBackupHistoryEntry(
        backupType, backupStarted, DateTime.Now, uploadTime.Elapsed, numberOfFilesBackedup);

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

    private int CreateBackupFiles(out BackupHistoryType backupType)
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

      var numberOfBackedupFiles = 0;

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
                AddFilesToFullBackup(backupFile, pathToBackup);
                break;
              case BackupHistoryType.Differential:
                AddFilesToDifferentialBackup(backupFile, pathToBackup);
                break;
            }

            if (numberOfBackedupFiles == 0)
            {
              _log.Info("No files needed to backup.");

              continue;
            }

            _log.Info("Backed up " + backupFile.Count + " files.");

            numberOfBackedupFiles += backupFile.Count;

            backupFile.Save();
          }
        }
        catch (Exception e)
        {
          _log.Error("Failed to backup " + pathToBackup + ".", e);
        }
      }

      return numberOfBackedupFiles;
    }

    private void AddFilesToFullBackup(ZipFile backupFile, string pathToBackup)
    {
      backupFile.AddDirectory(pathToBackup);
    }

    private void AddFilesToDifferentialBackup(ZipFile backupFile, string pathToBackup)
    {
      var latestBackup = _backupHistoryService.GetLatestBackupDate();

      var backupPathInfo = new DirectoryInfo(pathToBackup);

      var filesToBackup = backupPathInfo.GetFiles("*.*", SearchOption.AllDirectories).AsEnumerable();

      if (latestBackup.HasValue)
        filesToBackup = filesToBackup.Where(f => f.LastWriteTime > latestBackup.Value);

      foreach (var fileToBackup in filesToBackup)
      {
        backupFile.AddFile(
          fileToBackup.Name, fileToBackup.DirectoryName.Substring(pathToBackup.Length));
      }
    }
  }
}