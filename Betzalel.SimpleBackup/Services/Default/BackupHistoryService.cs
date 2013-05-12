using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Betzalel.Infrastructure.Extensions;
using Betzalel.Infrastructure.Utilities;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupHistoryService : IBackupHistoryService, IDisposable
  {
    private readonly FileStream _backupFileStream;

    public BackupHistoryService()
    {
      var backupHistoryPath = PathUtil.MapToExecutable("BackupHistory.xml");

      if (!File.Exists(backupHistoryPath))
      {
        using (var sw = File.CreateText(backupHistoryPath))
        {
          var document = new XDocument(new XElement("Backups"));

          document.Save(sw);
        }
      }

      _backupFileStream =
        File.Open(backupHistoryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    }

    public DateTime? GetLatestFullBackupDate()
    {
      _backupFileStream.Seek(0, SeekOrigin.Begin);

      var document = XDocument.Load(_backupFileStream);

      return
        document.Root
          .Elements("Backup")
          .Where(x => x.NotNullAttribute("type").Value == BackupHistoryType.Full.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("started").Value));
    }

    public void AddBackupHistoryEntry(
      BackupHistoryType backupHistoryType, 
      DateTime started, 
      DateTime ended, 
      TimeSpan uploadTime, 
      int numberOfFilesBackedup)
    {
      _backupFileStream.Seek(0, SeekOrigin.Begin);

      var document = XDocument.Load(_backupFileStream);

      document.Root.Add(
        new XElement(
          "Backup",
          new XAttribute("type", backupHistoryType.ToString()),
          new XAttribute("started", started.ToString()),
          new XAttribute("ended", ended.ToString()),
          new XAttribute("uploadTime", uploadTime.ToString()),
          new XAttribute("numberOfFilesBackedup", numberOfFilesBackedup.ToString())
          ));

      _backupFileStream.SetLength(0);

      document.Save(_backupFileStream);
    }

    public void Dispose()
    {
      _backupFileStream.Dispose();
    }
  }
}