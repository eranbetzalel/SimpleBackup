using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Extensions;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupHistoryService : IBackupHistoryService, IDisposable
  {
    private const char BackupLogFileSeperator = '|';
    private readonly FileStream _backupHistoryFileStream;
    private readonly StreamReader _backupLogFileReader;
    private readonly StreamWriter _backupLogFileWriter;
    private HashSet<string> _backupLogFileCache;

    public BackupHistoryService(ISettingsProvider settingsProvider)
    {
      var backupHistoryFilePath = settingsProvider.GetSetting<string>("BackupHistoryFile");
      var backupLogFilePath = settingsProvider.GetSetting<string>("BackupLogFile");

      if (!File.Exists(backupHistoryFilePath))
      {
        using (var sw = File.CreateText(backupHistoryFilePath))
        {
          var document = new XDocument(new XElement("Backups"));

          document.Save(sw);
        }
      }

      _backupHistoryFileStream =
        File.Open(backupHistoryFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

      var backupLogFileStream =
        File.Open(backupLogFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

      _backupLogFileReader = new StreamReader(backupLogFileStream, Encoding.UTF8);
      _backupLogFileWriter = new StreamWriter(backupLogFileStream, Encoding.UTF8);
    }

    public DateTime? GetLatestSuccessfulFullBackupDate()
    {
      _backupHistoryFileStream.Seek(0, SeekOrigin.Begin);

      var document = XDocument.Load(_backupHistoryFileStream);

      return
        document.Root
          .Elements("Backup")
          .Where(x =>
            x.NotNullAttribute("type").Value == BackupHistoryType.Full.ToString() &&
            x.NotNullAttribute("result").Value == BackupResult.Success.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("started").Value));
    }

    public DateTime? GetLatestSuccessfullBackupDate()
    {
      _backupHistoryFileStream.Seek(0, SeekOrigin.Begin);

      var document = XDocument.Load(_backupHistoryFileStream);

      return
        document.Root
          .Elements("Backup")
          .Where(x => x.NotNullAttribute("result").Value == BackupResult.Success.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("started").Value));
    }

    public void AddBackupHistoryEntry(
      BackupHistoryType backupHistoryType, 
      DateTime started, 
      DateTime ended, 
      TimeSpan? uploadTime, 
      ICollection<string> backedupFilePaths, 
      BackupResult backupResult)
    {
      if (backupResult == BackupResult.Success)
        UpdateBackupLog(
          backedupFilePaths, backupHistoryType == BackupHistoryType.Full);

      _backupHistoryFileStream.Seek(0, SeekOrigin.Begin);

      var document = XDocument.Load(_backupHistoryFileStream);

      document.Root.Add(
        new XElement(
          "Backup",
          new XAttribute("type", backupHistoryType.ToString()),
          new XAttribute("started", started.ToString()),
          new XAttribute("ended", ended.ToString()),
          new XAttribute("uploadTime", uploadTime.ToString()),
          new XAttribute("numberOfFilesBackedup", backedupFilePaths.Count.ToString()),
          new XAttribute("result", backupResult.ToString())));

      _backupHistoryFileStream.SetLength(0);

      document.Save(_backupHistoryFileStream);
    }

    public bool IsBackedUp(string fullName)
    {
      return GetHistoryLog().Contains(fullName);
    }

    public void Dispose()
    {
      _backupHistoryFileStream.Dispose();
      _backupLogFileReader.Dispose();
      _backupLogFileWriter.Dispose();
    }

    private void UpdateBackupLog(ICollection<string> backedupFilePaths, bool clearLogFile)
    {
      IEnumerable<string> newBackedupFilePaths = backedupFilePaths;

      var backupLogFileStream = _backupLogFileReader.BaseStream;

      if (clearLogFile)
      {
        backupLogFileStream.SetLength(0);
      }
      else if (!_backupLogFileReader.EndOfStream)
      {
        backupLogFileStream.Seek(0, SeekOrigin.Begin);

        var loggedFilePaths = GetHistoryLog();

        newBackedupFilePaths = backedupFilePaths.Where(b => !loggedFilePaths.Contains(b));
      }

      backupLogFileStream.Seek(0, SeekOrigin.End);

      if (!newBackedupFilePaths.Any())
        return;

      if (!_backupLogFileCache.IsEmpty())
        _backupLogFileWriter.Write(BackupLogFileSeperator);

      foreach (var backedupFilePath in newBackedupFilePaths)
      {
        _backupLogFileWriter.Write(backedupFilePath + BackupLogFileSeperator);
      }

      _backupLogFileWriter.Flush();

      //  Removes the last seperator
      backupLogFileStream.SetLength(backupLogFileStream.Length - 1);

      _backupLogFileCache = null;
    }

    private HashSet<string> GetHistoryLog()
    {
      return _backupLogFileCache ??
             (_backupLogFileCache = new HashSet<string>(_backupLogFileReader.ReadToEnd().Split(BackupLogFileSeperator)));
    }
  }
}