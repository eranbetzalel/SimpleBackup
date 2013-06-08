using System;
using System.Collections.Generic;

namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupHistoryService
  {
    DateTime? GetLatestSuccessfullBackupDate();
    DateTime? GetLatestSuccessfulFullBackupDate();

    void AddBackupHistoryEntry(
      BackupHistoryType backupHistoryType, 
      DateTime started, 
      DateTime ended, 
      TimeSpan? uploadTime, 
      ICollection<string> backedupFilePaths, BackupResult 
      backupResult);

    bool IsBackedUp(string fullName);
  }
}