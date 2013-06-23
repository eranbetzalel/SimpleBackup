using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Extensions;
using Betzalel.SimpleBackup.Models;
using Betzalel.SimpleBackup.Types;

namespace Betzalel.SimpleBackup.Services.Default
{
  public class BackupHistoryService : IBackupHistoryService, IDisposable
  {
    private const char BackupLogFileSeperator = '|';
    private readonly FileStream _backupHistoryFileStream;
    private Lazy<XDocument> _backupHistoryDocument;
    private Lazy<HashSet<string>> _backupLogFileCache;
    private readonly StreamReader _backupLogFileReader;
    private readonly StreamWriter _backupLogFileWriter;

    public BackupHistoryService(ISettingsProvider settingsProvider)
    {
      InitializeBackupHistoryCache();
      InitializeBackupLogCache();

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

    public DateTime? GetLatestSuccessfullBackupCompressDate()
    {
      return
        _backupHistoryDocument.Value.Root
          .Elements("Backup")
          .Where(x => x.NotNullAttribute("state").Value == BackupState.Success.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("compressStarted").Value));
    }

    public DateTime? GetLatestSuccessfulFullBackupCompressDate()
    {
      return
        _backupHistoryDocument.Value.Root
          .Elements("Backup")
          .Where(x =>
            x.NotNullAttribute("type").Value == BackupType.Full.ToString() &&
            x.NotNullAttribute("state").Value != BackupState.FileCompressFailed.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("compressStarted").Value));
    }

    public BackupHistoryEntry[] GetBackupEntriesToStorage()
    {
      return
        _backupHistoryDocument.Value.Root
          .Elements("Backup")
          .Where(x => 
            x.NotNullAttribute("state").Value == BackupState.FileCompressSuccess.ToString() ||
            x.NotNullAttribute("state").Value == BackupState.StorageFailed.ToString())
          .Select(x => new BackupHistoryEntry(x))
          .ToArray();
    }

    public bool IsBackedUp(string fullName)
    {
      return _backupLogFileCache.Value.Contains(fullName);
    }

    public void AddBackupCompressCompletedEntries(
      BackupType backupType,
      DateTime compressStarted,
      DateTime compressEnded,
      BackupState backupState,
      ICollection<string> backedupFilePaths,
      ICollection<string> storagePendingFilesPaths)
    {
      if (backupState == BackupState.FileCompressSuccess)
        UpdateBackupLog(
          backedupFilePaths, backupType == BackupType.Full);

      var document = _backupHistoryDocument.Value;

      document.Root.Add(
        new BackupHistoryEntry
          {
            Id = GetLastBackupHistoryEntryId() + 1,
            BackupState = backupState,
            BackupType = backupType,
            CompressStarted = compressStarted,
            CompressEnded = compressEnded,
            NumberOfBackedupFiles = backedupFilePaths.Count,
            StoragePendingFilesPaths = storagePendingFilesPaths.ToArray()
          }.ToXElement());

      _backupHistoryFileStream.SetLength(0);

      document.Save(_backupHistoryFileStream);

      InitializeBackupHistoryCache();
    }

    public void UpdateBackupHistoryEntry(BackupHistoryEntry backupEntryToStorage)
    {
      var document = _backupHistoryDocument.Value;

      var oldBackupEntry =
        document.Root
          .Elements("Backup")
          .FirstOrDefault(x => long.Parse(x.NotNullAttribute("id").Value) == backupEntryToStorage.Id);

      if (oldBackupEntry == null)
        throw new Exception("Could not find backup history entry to update (" + backupEntryToStorage.Id + ").");

      oldBackupEntry.ReplaceWith(backupEntryToStorage.ToXElement());

      _backupHistoryFileStream.SetLength(0);

      document.Save(_backupHistoryFileStream);

      InitializeBackupHistoryCache();
    }

    public void Dispose()
    {
      _backupHistoryFileStream.Dispose();
      _backupLogFileReader.Dispose();
      _backupLogFileWriter.Dispose();
    }

    private long GetLastBackupHistoryEntryId()
    {
      var document = _backupHistoryDocument.Value;

      if (!document.Root.Elements("Backup").Any())
        return 0;

      return document.Root.Elements("Backup").Max(x => long.Parse(x.NotNullAttribute("id").Value));
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

        var loggedFilePaths = _backupLogFileCache.Value;

        newBackedupFilePaths = backedupFilePaths.Where(b => !loggedFilePaths.Contains(b));
      }

      backupLogFileStream.Seek(0, SeekOrigin.End);

      if (!newBackedupFilePaths.Any())
        return;

      if (!_backupLogFileCache.Value.IsEmpty())
        _backupLogFileWriter.Write(BackupLogFileSeperator);

      foreach (var backedupFilePath in newBackedupFilePaths)
      {
        _backupLogFileWriter.Write(backedupFilePath + BackupLogFileSeperator);
      }

      _backupLogFileWriter.Flush();

      //  Removes the last seperator
      backupLogFileStream.SetLength(backupLogFileStream.Length - 1);

      InitializeBackupLogCache();
    }

    private void InitializeBackupHistoryCache()
    {
      _backupHistoryDocument = new Lazy<XDocument>(LoadBackupHistoryFile);
    }

    private HashSet<string> LoadHistoryLogFile()
    {
      return new HashSet<string>(_backupLogFileReader.ReadToEnd().Split(BackupLogFileSeperator));
    }

    private void InitializeBackupLogCache()
    {
      _backupLogFileCache = new Lazy<HashSet<string>>(LoadHistoryLogFile);
    }

    private XDocument LoadBackupHistoryFile()
    {
      _backupHistoryFileStream.Seek(0, SeekOrigin.Begin);

      return XDocument.Load(_backupHistoryFileStream);
    }
  }
}