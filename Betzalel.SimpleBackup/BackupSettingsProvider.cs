using System;
using System.Collections.Generic;
using Betzalel.Infrastructure.SettingProviders;
using Betzalel.Infrastructure.Utilities;

namespace Betzalel.SimpleBackup
{
  public class BackupSettingsProvider : AppSettingsProvider
  {
    private readonly Dictionary<string, object> _backupConfiguration;

    public BackupSettingsProvider()
    {
      _backupConfiguration =
        new Dictionary<string, object>
          {
            {"TempDirectory", PathUtil.MapToExecutable("Temp")}
          };
    }

    public override T GetSetting<T>(string settingName)
    {
      object customValue;

      if (_backupConfiguration.TryGetValue(settingName, out customValue))
        return (T)Convert.ChangeType(customValue, typeof(T));

      return base.GetSetting<T>(settingName);
    }
  }
}