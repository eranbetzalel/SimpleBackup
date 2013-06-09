using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using Betzalel.Infrastructure;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupStorageService : IBackupStorageService
  {
    private readonly ILog _log;
    private readonly ISettingsProvider _settingsProvider;

    public BackupStorageService(ILog log, ISettingsProvider settingsProvider)
    {
      _log = log;
      _settingsProvider = settingsProvider;
    }

    public bool UploadBackupFilesToFtp()
    {
      try
      {
        var tempDirectory = _settingsProvider.GetSetting<string>("TempDirectory");

        var tempBackupFilePaths = Directory.GetFiles(tempDirectory, "*.zip");

        if (!tempBackupFilePaths.Any())
        {
          _log.Info("No files to upload.");

          return false;
        }

        var relativeFtpBackupDirectory =
          _settingsProvider.GetSetting<string>("RelativeFtpBackupDirectory").TrimEnd('/', '\\');

        using (var ftpClient = new FtpClient())
        {
          ftpClient.DataConnectionType = FtpDataConnectionType.AutoActive;

          ftpClient.Host = _settingsProvider.GetSetting<string>("FtpHost");
          ftpClient.Port = _settingsProvider.GetSetting<int>("FtpPort");

          ftpClient.Credentials =
            new NetworkCredential(
              _settingsProvider.GetSetting<string>("FtpUsername"),
              _settingsProvider.GetSetting<string>("FtpPassword"));

          _log.Info("Connecting to FTP server (" + ftpClient.Host + ":" + ftpClient.Port + ")...");

          ftpClient.Connect();

          var ftpBackupPath =
            relativeFtpBackupDirectory + "/" + DateTime.Now.ToString("yyyyMMdd_HHmm") + " Full Backup";

          ftpClient.CreateDirectory(ftpBackupPath);

          foreach (var tempBackupFilePath in tempBackupFilePaths)
          {
            UploadBackupFile(ftpClient, ftpBackupPath, tempBackupFilePath);
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
      var backupFilename = Path.GetFileName(backupFilePath);

      using (var ftpBackupFile = ftpClient.OpenWrite(ftpBackupPath + "/" + backupFilename, FtpDataType.Binary))
      {
        using (var backupFile = File.OpenRead(backupFilePath))
        {
          int bytesRead;
          long totalBytesWritten = 0;
          var totalBytes = backupFile.Length;
          var bytesWrittenLogPoint = 0.1;

          var buffer = new byte[4096];

          _log.Info(string.Format("Uploading {0} ({1:N0} bytes)...", backupFilename, totalBytes));

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
    }
  }
}