using System;

namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupHistoryService
  {
    DateTime? GetLatestFullBackupDate();

    void AddBackupHistoryEntry(
      BackupHistoryType backupHistoryType,
      DateTime started,
      DateTime ended,
      TimeSpan uploadTime,
      int numberOfFilesBackedup);
  }
}