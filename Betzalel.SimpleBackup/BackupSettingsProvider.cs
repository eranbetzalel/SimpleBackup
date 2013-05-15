using System;
using System.Collections.Generic;
using Betzalel.Infrastructure.SettingProviders;
using Betzalel.Infrastructure.Utilities;

namespace Betzalel.SimpleBackup
{
  public class BackupSettingsProvider : AppSettingsProvider
  {
    private readonly Dictionary<string, string> _backupConfiguration;

    public BackupSettingsProvider()
    {
      _backupConfiguration =
        new Dictionary<string, string>
          {
            {"TempDirectory", PathUtil.MapToExecutable("Temp")},
            {"BackupHistoryFile", PathUtil.MapToExecutable("BackupHistory.xml")},
            {"BackupLogFile", PathUtil.MapToExecutable("BackupLog.txt")}
          };
    }

    public override T GetSetting<T>(string settingName)
    {
      string customValue;

      if (_backupConfiguration.TryGetValue(settingName, out customValue))
        return (T)Convert.ChangeType(customValue.Trim(), typeof(T));

      return base.GetSetting<T>(settingName);
    }
  }
}