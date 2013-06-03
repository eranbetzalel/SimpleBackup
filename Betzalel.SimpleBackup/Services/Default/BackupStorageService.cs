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
            var backupFilename = Path.GetFileName(tempBackupFilePath);

            _log.Info("Uploading " + backupFilename + "...");

            using (var ftpBackupFile = ftpClient.OpenWrite(ftpBackupPath + "/" + backupFilename))
            {
              using (var backupFile = File.OpenRead(tempBackupFilePath))
              {
                backupFile.CopyTo(ftpBackupFile);
              }
            }
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
  }
}