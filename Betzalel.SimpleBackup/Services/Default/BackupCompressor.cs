using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Extensions;
using Betzalel.SimpleBackup.Types;
using Ionic.Zip;
using Ionic.Zlib;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupCompressor : IBackupCompressor
  {
    private readonly ILog _log;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IBackupHistoryService _backupHistoryService;

    private readonly string _tempDirectory;
    private readonly string _storagePendingDirectory;
    private readonly string[] _pathsToExclude;
    private readonly string[] _fileTypesToExclude;
    private readonly string[] _pathsToBackup;

    private float _entriesSavedLogPoint;

    public BackupCompressor(
      ILog log,
      ISettingsProvider settingsProvider,
      IBackupHistoryService backupHistoryService)
    {
      _log = log;
      _settingsProvider = settingsProvider;
      _backupHistoryService = backupHistoryService;

      _tempDirectory = _settingsProvider.GetSetting<string>("TempDirectory");
      _storagePendingDirectory = _settingsProvider.GetSetting<string>("StoragePendingDirectory");

      var backupPaths = _settingsProvider.GetSetting<string>("BackupPaths");
      var excludedBackupPaths = _settingsProvider.GetSetting<string>("ExcludedBackupPaths");
      var excludedFileTypes = _settingsProvider.GetSetting<string>("ExcludedFileTypes");

      if (backupPaths.Length == 0)
        throw new Exception("No Backup Paths configured.");

      var splitChar = ",".ToCharArray();

      _pathsToBackup =
        backupPaths
        .Split(splitChar, StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim().TrimEnd('\\', '/'))
        .ToArray();

      _pathsToExclude =
        excludedBackupPaths
          .Split(splitChar, StringSplitOptions.RemoveEmptyEntries)
          .Select(p => p.Trim().TrimEnd('\\', '/'))
          .ToArray();

      _fileTypesToExclude =
        excludedFileTypes
          .Split(splitChar, StringSplitOptions.RemoveEmptyEntries)
          .Select(p => p.Trim())
          .ToArray();
    }

    public bool CreateBackupFiles(
      out BackupType backupType, 
      out List<string> backedupFilePaths,
      out List<string> storagePendingFilesPaths)
    {
      var backupFilePaths = new List<string>();

      backedupFilePaths = new List<string>();
      storagePendingFilesPaths = new List<string>();

      backupType = GetCurrentBackupType();

      _log.Info("Starting " + backupType + " backup...");

      CreateOrEmptyTempDirectory();

      if (!ValidateBackupDirectories(_pathsToBackup))
      {
        RemoveTempDirectory();

        return false;
      }

      if (!CreateBackupFilesInternal(backupType, backedupFilePaths, backupFilePaths))
      {
        RemoveTempDirectory();

        return false;
      }

      MoveFilesToStorageDirectory(backupFilePaths, out storagePendingFilesPaths);

      RemoveTempDirectory();

      return true;
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

      return true;
    }

    private void CreateOrEmptyTempDirectory()
    {
      if (Directory.Exists(_tempDirectory))
      {
        _log.Debug("Deleting old temp dir...");

        Directory.Delete(_tempDirectory, true);
      }

      Directory.CreateDirectory(_tempDirectory);

      _log.Debug("Temp directory created.");
    }

    private void RemoveTempDirectory()
    {
      Directory.Delete(_tempDirectory, true);

      _log.Debug("Temp directory removed.");
    }

    private void MoveFilesToStorageDirectory(List<string> backupFilePaths, out List<string> storagePendingFilesPaths)
    {
      storagePendingFilesPaths = new List<string>();

      if (!Directory.Exists(_storagePendingDirectory))
        Directory.CreateDirectory(_storagePendingDirectory);

      foreach (var backupFilePath in backupFilePaths)
      {
        var storagePendingFilePath =
          Path.Combine(_storagePendingDirectory, Path.GetFileName(backupFilePath));

        File.Move(backupFilePath, storagePendingFilePath);

        storagePendingFilesPaths.Add(storagePendingFilePath);
      }
    }

    private BackupType GetCurrentBackupType()
    {
      var latestFullBackup = _backupHistoryService.GetLatestSuccessfulFullBackupDate();

      var minimumDaysBetweenFullBackups =
        _settingsProvider.GetSetting<int>("MinimumDaysBetweenFullBackups");

      if (!latestFullBackup.HasValue ||
          latestFullBackup.Value.AddDays(minimumDaysBetweenFullBackups) <= DateTime.Now)
      {
        return BackupType.Full;
      }

      return BackupType.Differential;
    }

    private bool CreateBackupFilesInternal(
      BackupType backupType, List<string> backedupFilePaths, List<string> backupFilePaths)
    {
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
            backupFile.ZipErrorAction = ZipErrorAction.Skip;

            backupFile.ZipError += BackupFileOnZipError;
            backupFile.SaveProgress += BackupFileOnSaveProgress;

            long currentBackupPathTotalFileSize;
            ICollection<string> currentBackupPathBackedupFilePaths;

            switch (backupType)
            {
              case BackupType.Full:
                AddFilesToBackup(
                  backupFile,
                  BackupType.Full,
                  pathToBackup,
                  out currentBackupPathBackedupFilePaths,
                  out currentBackupPathTotalFileSize);
                break;
              case BackupType.Differential:
                AddFilesToBackup(
                  backupFile,
                  BackupType.Differential,
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
              currentBackupPathTotalFileSize.ToString("N0") + " bytes)...");

            _entriesSavedLogPoint = 0.1f;

            backupFile.Save();

            backupFile.SaveProgress -= BackupFileOnSaveProgress;
            backupFile.ZipError -= BackupFileOnZipError;

            backupFilePaths.Add(tempBackupFileName);

            _log.Info("Compressing completed.");
          }
        }
        catch (Exception e)
        {
          _log.Error("Failed to backup " + pathToBackup + ".", e);

          //  TODO: take the "hit" for failed backups
          return false;
        }
      }

      return true;
    }

    private void BackupFileOnZipError(object sender, ZipErrorEventArgs zipErrorEventArgs)
    {
      _log.Error(
        "An error occurred while compressing " + zipErrorEventArgs.FileName + " - file will be skipped.",
        zipErrorEventArgs.Exception);

      zipErrorEventArgs.CurrentEntry.ZipErrorAction = ZipErrorAction.Skip;
    }

    private void BackupFileOnSaveProgress(object sender, SaveProgressEventArgs saveProgressEventArgs)
    {
      if (saveProgressEventArgs.EventType != ZipProgressEventType.Saving_AfterWriteEntry)
        return;

      var entriesPercent = (float)saveProgressEventArgs.EntriesSaved / saveProgressEventArgs.EntriesTotal;

      if (entriesPercent < _entriesSavedLogPoint)
        return;

      _log.Info(
        string.Format(
          "Compress file progress: {0:N0} of {1:N0} ({2:P}) of {3}.",
          saveProgressEventArgs.EntriesSaved,
          saveProgressEventArgs.EntriesTotal,
          entriesPercent,
          Path.GetFileName(saveProgressEventArgs.ArchiveName)));

      _entriesSavedLogPoint += 0.1f;
    }

    private void AddFilesToBackup(
      ZipFile backupFile,
      BackupType backupType,
      string pathToBackup,
      out ICollection<string> backedupFilePaths,
      out long totalSize)
    {
      totalSize = 0;
      backedupFilePaths = new List<string>();

      var backupPathInfo = new DirectoryInfo(pathToBackup);

      var filesToBackup = backupPathInfo.GetAllFiles().AsEnumerable();

      if (backupType == BackupType.Differential)
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
            f =>
            !_pathsToExclude.Any(e => f.DirectoryName.StartsWith(e)) &&
            !_fileTypesToExclude.Contains(f.Extension.TrimStart('.')));

      foreach (var fileToBackup in filesToBackup)
      {
        if (fileToBackup.DirectoryName == null)
          throw new Exception(fileToBackup.FullName + " has no Directory Name.");

        backupFile.AddFile(
          fileToBackup.FullName, fileToBackup.DirectoryName.Substring(pathToBackup.Length));

        backedupFilePaths.Add(fileToBackup.FullName);

        totalSize += fileToBackup.Length;

        _log.Debug(
          () =>
          string.Format(
            "Adding file to backup: {0} ({1:N0} bytes).",
            fileToBackup.FullName, fileToBackup.Length));
      }
    }
  }
}