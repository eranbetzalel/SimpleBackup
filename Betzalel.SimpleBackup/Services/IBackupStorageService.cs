using Betzalel.SimpleBackup.Models;

namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupStorageService
  {
    bool ProcessStorageReadyBackupEntry(BackupHistoryEntry backupEntryToStorage);
  }
}