namespace Betzalel.SimpleBackup.Services
{
  public interface IBackupService
  {
    void StartBackup();
    void StopBackup();
  }
}