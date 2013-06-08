using System;
using System.Collections.Generic;
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

      TimeSpan? uploadTime = null;
      BackupHistoryType backupType;
      List<string> backedupFilePaths;
      var backupResult = BackupResult.Success;

      if (!CreateBackupFiles(out backupType, out backedupFilePaths))
      {
        backupResult = BackupResult.FileCompressFailed;
      }
      else
      {
        var uploadTimeStopwatch = Stopwatch.StartNew();

        if (!_backupStorageService.UploadBackupFilesToFtp())
          backupResult = BackupResult.FtpUploadFailed;

        uploadTime = uploadTimeStopwatch.Elapsed;
      }

      _backupHistoryService.AddBackupHistoryEntry(
        backupType,
        backupStarted,
        DateTime.Now,
        uploadTime,
        backedupFilePaths,
        backupResult);

      Directory.Delete(_tempDirectory, true);

      _log.Debug("Temp directory removed.");
    }

    private bool ValidateBackupDirectories(string[] backupPathsParts)
    {
      _log.Debug("Validating backup directories...");

      var missingDirectories =
        backupPathsParts.Where(backupPathsPart => !Directory.Exists(backupPathsPart)).ToArray();

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

    private bool CreateBackupFiles(out BackupHistoryType backupType, out List<string> backedupFilePaths)
    {
      var latestFullBackup = _backupHistoryService.GetLatestSuccessfulFullBackupDate();

      var minimumDaysBetweenFullBackups =
        _settingsProvider.GetSetting<int>("MinimumDaysBetweenFullBackups");

      if (!latestFullBackup.HasValue ||
        latestFullBackup.Value.AddDays(minimumDaysBetweenFullBackups) <= DateTime.Now)
      {
        backupType = BackupHistoryType.Full;
      }
      else
      {
        backupType = BackupHistoryType.Differential;
      }

      _log.Info("Starting " + backupType + " backup...");

      backedupFilePaths = new List<string>();

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

            long currentBackupPathTotalFileSize;
            ICollection<string> currentBackupPathBackedupFilePaths;

            switch (backupType)
            {
              case BackupHistoryType.Full:
                AddFilesToBackup(
                  backupFile,
                  BackupHistoryType.Full,
                  pathToBackup,
                  out currentBackupPathBackedupFilePaths,
                  out currentBackupPathTotalFileSize);
                break;
              case BackupHistoryType.Differential:
                AddFilesToBackup(
                  backupFile,
                  BackupHistoryType.Differential,
                  pathToBackup,
                  out currentBackupPathBackedupFilePaths,
                  out currentBackupPathTotalFileSize);
                break;
              default:
                throw new ArgumentOutOfRangeException("backupType");
            }

            if (currentBackupPathBackedupFilePaths.Count == 0)
            {
              _log.Info("No files needed to backup.");

              continue;
            }

            backedupFilePaths.AddRange(currentBackupPathBackedupFilePaths);

            _log.Info(
              "Compressing " + backupFile.Count + " files (" +
              currentBackupPathTotalFileSize.ToString("N3") + " bytes)...");

            backupFile.Save();

            _log.Info("Compressing completed.");
          }
        }
        catch (Exception e)
        {
          _log.Error("Failed to backup " + pathToBackup + ".", e);

          return false;
        }
      }

      return true;
    }

    private void AddFilesToBackup(
      ZipFile backupFile,
      BackupHistoryType backupHistoryType,
      string pathToBackup,
      out ICollection<string> backedupFilePaths,
      out long totalSize)
    {
      totalSize = 0;
      backedupFilePaths = new List<string>();

      var backupPathInfo = new DirectoryInfo(pathToBackup);

      var filesToBackup = backupPathInfo.GetAllFiles().AsEnumerable();

      if (backupHistoryType == BackupHistoryType.Differential)
      {
        var latestBackup = _backupHistoryService.GetLatestSuccessfullBackupDate();

        if (!latestBackup.HasValue)
          throw new Exception("Backup history is empty - could not perform differential backup.");

        filesToBackup =
          filesToBackup.Where(
            f => f.LastWriteTime > latestBackup || !_backupHistoryService.IsBackedUp(f.FullName));
      }

      if (_pathsToExclude.Any())
        filesToBackup =
          filesToBackup.Where(
            f => !_pathsToExclude.Contains(f.DirectoryName) && !_fileTypesToExclude.Contains(f.Extension.TrimStart('.')));

      foreach (var fileToBackup in filesToBackup)
      {
        if (fileToBackup.DirectoryName == null)
          throw new Exception(fileToBackup.FullName + " has no Directory Name.");

        backupFile.AddFile(
          fileToBackup.FullName, fileToBackup.DirectoryName.Substring(pathToBackup.Length));

        backedupFilePaths.Add(fileToBackup.FullName);

        totalSize += fileToBackup.Length;
      }
    }
  }
}