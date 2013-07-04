using System;
using System.Linq;
using System.Xml.Linq;
using Betzalel.Infrastructure.Extensions;
using Betzalel.SimpleBackup.Types;

namespace Betzalel.SimpleBackup.Models
{
  public class BackupHistoryEntry
  {
    public long Id { get; set; }
    public BackupType BackupType { get; set; }
    public DateTime CompressStarted { get; set; }
    public DateTime CompressEnded { get; set; }
    public int TotalStorageTime { get; set; }
    public DateTime StorageEnded { get; set; }
    public long NumberOfBackedupFiles { get; set; }
    public BackupState BackupState { get; set; }
    public string[] StoragePendingFilesPaths { get; set; }

    public BackupHistoryEntry()
    {
    }

    public BackupHistoryEntry(XElement element)
    {
      Id = long.Parse(element.NotNullAttribute("id").Value);
      BackupType = (BackupType)Enum.Parse(typeof(BackupType), element.NotNullAttribute("type").Value);
      CompressStarted = DateTime.Parse(element.NotNullAttribute("compressStarted").Value);
      CompressEnded = DateTime.Parse(element.NotNullAttribute("compressEnded").Value);
      TotalStorageTime = int.Parse(element.NotNullAttribute("totalStorageTime").Value);
      StorageEnded = DateTime.Parse(element.NotNullAttribute("storageEnded").Value);
      NumberOfBackedupFiles = long.Parse(element.NotNullAttribute("numberOfBackedupFiles").Value);
      BackupState = (BackupState)Enum.Parse(typeof(BackupState), element.NotNullAttribute("state").Value);

      var storagePendingFilesPathsElement = element.Element("StoragePendingFilesPaths");

      if (storagePendingFilesPathsElement != null)
      {
        StoragePendingFilesPaths =
          storagePendingFilesPathsElement.Elements().Select(p => p.Value).ToArray();
      }
    }

    public XElement ToXElement()
    {
      XElement storagePendingFilesPathsElement = null;

      if (StoragePendingFilesPaths != null && StoragePendingFilesPaths.Length > 0)
      {
        storagePendingFilesPathsElement =
          new XElement("StoragePendingFilesPaths",
            StoragePendingFilesPaths.Select(p => new XElement("StoragePendingFilePath", p)));
      }

      return new XElement(
        "Backup",
        new XAttribute("id", Id.ToString()),
        new XAttribute("type", BackupType.ToString()),
        new XAttribute("compressStarted", CompressStarted.ToString()),
        new XAttribute("compressEnded", CompressEnded.ToString()),
        new XAttribute("totalStorageTime", TotalStorageTime.ToString()),
        new XAttribute("storageEnded", StorageEnded.ToString()),
        new XAttribute("numberOfBackedupFiles", NumberOfBackedupFiles.ToString()),
        new XAttribute("state", BackupState.ToString()),
        storagePendingFilesPathsElement);
    }
  }
}