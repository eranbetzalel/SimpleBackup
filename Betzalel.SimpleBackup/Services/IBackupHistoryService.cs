using System;
using System.Collections.Generic;
using Betzalel.SimpleBackup.Models;
using Betzalel.SimpleBackup.Types;

namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupHistoryService
  {
    bool IsBackedUp(string fullName);

    DateTime? GetLatestSuccessfullBackupCompressDate();
    DateTime? GetLatestSuccessfulFullBackupCompressDate();
    BackupHistoryEntry[] GetBackupEntriesToStorage();

    void AddBackupCompressCompletedEntries(
      BackupType backupType,
      DateTime compressStarted,
      DateTime compressEnded,
      BackupState backupState,
      ICollection<string> backedupFilesPaths,
      ICollection<string> storagePendingFilesPaths);

    void UpdateBackupHistoryEntry(BackupHistoryEntry backupEntryToStorage);
  }
}