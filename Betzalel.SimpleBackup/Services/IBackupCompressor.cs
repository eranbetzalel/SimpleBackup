using System.Collections.Generic;
using Betzalel.SimpleBackup.Types;

namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupCompressor
  {
    bool CreateBackupFiles(
      out BackupType backupType,
      out List<string> backedupFilePaths,
      out List<string> storagePendingFilesPaths);
  }
}