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
  public class BackupHistoryService : IBackupHistoryService
  {
    private const string BackupLogFileSeperator = "|";

    private readonly object _backupLogFileLock;
    private readonly object _backupHistoryFileLock;
    private readonly string _backupLogFilePath;
    private readonly string _backupHistoryFilePath;
    private Lazy<XDocument> _backupHistoryDocument;
    private Lazy<HashSet<string>> _backupLogFileCache;

    public BackupHistoryService(ISettingsProvider settingsProvider)
    {
      _backupLogFileLock = new object();
      _backupHistoryFileLock = new object();

      InitializeBackupHistoryCache();
      InitializeBackupLogCache();

      _backupHistoryFilePath = settingsProvider.GetSetting<string>("BackupHistoryFile");
      _backupLogFilePath = settingsProvider.GetSetting<string>("BackupLogFile");
    }

    public DateTime? GetLatestSuccessfullBackupCompressDate()
    {
      if (_backupHistoryDocument.Value.Root == null)
        throw new Exception("Root element is missing.");

      return
        _backupHistoryDocument.Value.Root
          .Elements("Backup")
          .Where(x => x.NotNullAttribute("state").Value == BackupState.Success.ToString())
          .Max(x => (DateTime?)DateTime.Parse(x.NotNullAttribute("compressStarted").Value));
    }

    public DateTime? GetLatestSuccessfulFullBackupCompressDate()
    {
      if (_backupHistoryDocument.Value.Root == null)
        throw new Exception("Root element is missing.");

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
      if (_backupHistoryDocument.Value.Root == null)
        throw new Exception("Root element is missing.");

      return
        _backupHistoryDocument.Value.Root
          .Elements("Backup")
          .Where(x =>
            x.NotNullAttribute("state").Value == BackupState.FileCompressSuccess.ToString() ||
            x.NotNullAttribute("state").Value == BackupState.StorageFailed.ToString())
          .Select(x => new BackupHistoryEntry(x))
          .ToArray();
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

      lock (_backupHistoryFileLock)
      {
        var document = _backupHistoryDocument.Value;

        if (document.Root == null)
          throw new Exception("Root element is missing.");

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

        document.Save(_backupHistoryFilePath);

        InitializeBackupHistoryCache();
      }
    }

    public void UpdateBackupHistoryEntry(BackupHistoryEntry backupEntryToStorage)
    {
      lock (_backupHistoryFileLock)
      {
        var document = _backupHistoryDocument.Value;

        if (document.Root == null)
          throw new Exception("Root element is missing.");

        var oldBackupEntry =
          document.Root
            .Elements("Backup")
            .FirstOrDefault(x => long.Parse(x.NotNullAttribute("id").Value) == backupEntryToStorage.Id);

        if (oldBackupEntry == null)
          throw new Exception("Could not find backup history entry to update (" + backupEntryToStorage.Id + ").");

        oldBackupEntry.ReplaceWith(backupEntryToStorage.ToXElement());

        document.Save(_backupHistoryFilePath);

        InitializeBackupHistoryCache();
      }
    }

    private long GetLastBackupHistoryEntryId()
    {
      var document = _backupHistoryDocument.Value;

      if (document.Root == null)
        throw new Exception("Root element is missing.");

      if (!document.Root.Elements("Backup").Any())
        return 0;

      return document.Root.Elements("Backup").Max(x => long.Parse(x.NotNullAttribute("id").Value));
    }

    public bool IsBackedUp(string fullName)
    {
      return _backupLogFileCache.Value.Contains(fullName);
    }

    private void UpdateBackupLog(ICollection<string> backedupFilePaths, bool clearLogFile)
    {
      lock (_backupLogFileLock)
      {
        using (var backupLogFile = File.Open(_backupLogFilePath, FileMode.OpenOrCreate))
        {
          var backupLogFileCache = _backupLogFileCache.Value;
          var newBackedupFilePaths = backedupFilePaths;

          if (clearLogFile)
          {
            backupLogFile.SetLength(0);
          }
          else
          {
            newBackedupFilePaths = backedupFilePaths.Where(b => !backupLogFileCache.Contains(b)).ToArray();

            if (!newBackedupFilePaths.Any())
              return;
          }

          backupLogFile.Seek(0, SeekOrigin.End);

          foreach (var backedupFilePath in newBackedupFilePaths)
          {
            backupLogFile.Write(backedupFilePath + BackupLogFileSeperator, Encoding.UTF8);
          }
        }

        InitializeBackupLogCache();
      }
    }

    private void InitializeBackupHistoryCache()
    {
      _backupHistoryDocument = new Lazy<XDocument>(LoadBackupHistoryFile);
    }

    private XDocument LoadBackupHistoryFile()
    {
      if (!File.Exists(_backupHistoryFilePath))
      {
        using (var sw = File.CreateText(_backupHistoryFilePath))
        {
          var document = new XDocument(new XElement("Backups"));

          document.Save(sw);
        }
      }

      return XDocument.Load(_backupHistoryFilePath);
    }

    private void InitializeBackupLogCache()
    {
      _backupLogFileCache = new Lazy<HashSet<string>>(LoadBackupLogFile);
    }

    private HashSet<string> LoadBackupLogFile()
    {
      using (var backupLogFileReader = File.OpenText(_backupLogFilePath))
      {
        return
          new HashSet<string>(
            backupLogFileReader
              .ReadToEnd()
              .Split(new[] { BackupLogFileSeperator }, StringSplitOptions.RemoveEmptyEntries));
      }
    }
  }
}