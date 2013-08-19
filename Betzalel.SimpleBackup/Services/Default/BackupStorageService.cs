using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Extensions;
using Betzalel.SimpleBackup.Models;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupStorageService : IBackupStorageService
  {
    private readonly ILog _log;
    private readonly ISettingsProvider _settingsProvider;
    private readonly FtpDataConnectionType _dataConnectionType;

    public BackupStorageService(ILog log, ISettingsProvider settingsProvider)
    {
      _log = log;
      _settingsProvider = settingsProvider;

      var ftpDataConnectionTypeName = _settingsProvider.GetSetting<string>("FtpDataConnectionType");

      if (!Enum.TryParse(ftpDataConnectionTypeName, out _dataConnectionType))
        throw new Exception(
          "FtpDataConnectionType value is invalid. Please use one of the following values: " +
          Enum.GetNames(typeof(FtpDataConnectionType)).OrderBy(x => x).ToStringList() + ".");
    }

    public bool ProcessStorageReadyBackupEntry(BackupHistoryEntry backupEntryToStorage)
    {
      try
      {
        if (!backupEntryToStorage.StoragePendingFilesPaths.Any())
        {
          _log.Debug("No files to store.");

          return true;
        }

        var relativeFtpBackupDirectory =
          _settingsProvider.GetSetting<string>("RelativeFtpBackupDirectory").TrimEnd('/', '\\');

        using (var ftpClient = new FtpClient())
        {
          ftpClient.DataConnectionType = _dataConnectionType;

          ftpClient.Host = _settingsProvider.GetSetting<string>("FtpHost");
          ftpClient.Port = _settingsProvider.GetSetting<int>("FtpPort");

          ftpClient.Credentials =
            new NetworkCredential(
              _settingsProvider.GetSetting<string>("FtpUsername"),
              _settingsProvider.GetSetting<string>("FtpPassword"));

          _log.Info("Connecting to FTP server (" + ftpClient.Host + ":" + ftpClient.Port + ")...");

          ftpClient.Connect();

          var ftpBackupPath =
            string.Format(
              "{0}/{1} {2} Backup",
              relativeFtpBackupDirectory,
              backupEntryToStorage.CompressStarted.ToString("yyyyMMdd_HHmmss"),
              backupEntryToStorage.BackupType);

          ftpClient.CreateDirectory(ftpBackupPath);

          //  Upload compressed backups to FTP server
          foreach (var storagePendingFilePath in backupEntryToStorage.StoragePendingFilesPaths)
          {
            UploadBackupFile(ftpClient, ftpBackupPath, storagePendingFilePath);
          }

          //  Removed already uploaded backup files
          foreach (var storagePendingFilePath in backupEntryToStorage.StoragePendingFilesPaths)
          {
            File.Delete(storagePendingFilePath);
          }

          _log.Info("Finished uploading files to the FTP server.");
        }

        return true;
      }
      catch (Exception e)
      {
        _log.Error("Failed to upload files to FTP server.", e);

        return false;
      }
    }

    private void UploadBackupFile(FtpClient ftpClient, string ftpBackupPath, string backupFilePath)
    {
      Stream ftpBackupFile;
      long totalBytesWritten = 0;

      var backupFilename = Path.GetFileName(backupFilePath);

      var ftpBackupFilePath = ftpBackupPath + "/" + backupFilename;

      if (ftpClient.FileExists(ftpBackupFilePath))
      {
        var ftpBackupFileSize = ftpClient.GetFileSize(ftpBackupFilePath);

        totalBytesWritten = ftpBackupFileSize;

        ftpBackupFile = ftpClient.OpenAppend(ftpBackupPath + "/" + backupFilename, FtpDataType.Binary);
      }
      else
      {
        ftpBackupFile = ftpClient.OpenWrite(ftpBackupPath + "/" + backupFilename, FtpDataType.Binary);
      }

      try
      {
        using (var backupFile = File.OpenRead(backupFilePath))
        {
          int bytesRead;
          var totalBytes = backupFile.Length;
          var bytesWrittenLogPoint = 0.1;

          var buffer = new byte[4096];

          _log.Info(string.Format("Uploading {0} ({1:N0} bytes)...", backupFilename, totalBytes));

          if (totalBytesWritten > 0)
            backupFile.Seek(totalBytesWritten, SeekOrigin.Begin);

          while ((bytesRead = backupFile.Read(buffer, 0, buffer.Length)) > 0)
          {
            ftpBackupFile.Write(buffer, 0, bytesRead);

            totalBytesWritten += bytesRead;

            var writtenBytesPercentage = ((float)totalBytesWritten / totalBytes);

            if (writtenBytesPercentage >= bytesWrittenLogPoint)
            {
              _log.Info(
                string.Format(
                  "Backup file upload progress: {0:P} ({1:N0} bytes) of {2}.",
                  writtenBytesPercentage,
                  totalBytesWritten,
                  backupFilename));

              bytesWrittenLogPoint += 0.1;
            }
          }
        }
      }
      finally
      {
        ftpBackupFile.Dispose();
      }
    }
  }
}