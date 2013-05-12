using System;
using Betzalel.Infrastructure.SettingProviders;
using Betzalel.Infrastructure.Utilities;

namespace Betzalel.SimpleBackup
{
  public class BackupSettingsProvider : AppSettingsProvider
  {
    private readonly string _tempDirectory;

    public BackupSettingsProvider()
    {
      _tempDirectory = PathUtil.MapToExecutable("Temp");      
    }

    public override T GetSetting<T>(string settingName)
    {
      object customValue;

      switch (settingName)
      {
        case "TempDirectory":
          customValue = _tempDirectory;
          break;
        default:
          return base.GetSetting<T>(settingName);
      }

      return (T)Convert.ChangeType(customValue, typeof(T));
    }
  }
}