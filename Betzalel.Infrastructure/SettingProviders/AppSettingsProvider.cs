using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Betzalel.Infrastructure.SettingProviders
{
  public class AppSettingsProvider : ISettingsProvider
  {
    private readonly NameValueCollection _appSettings;

    public AppSettingsProvider()
    {
      _appSettings = ConfigurationManager.AppSettings;
    }

    public virtual T GetSetting<T>(string settingName)
    {
      var value = _appSettings[settingName];

      if (value == null)
        throw new Exception("Configuration file does not contain the setting \"" + settingName + "\".");

      return (T)Convert.ChangeType(value, typeof(T));
    }
  }
}
