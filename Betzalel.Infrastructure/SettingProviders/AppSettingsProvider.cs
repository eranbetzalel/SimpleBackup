using System;
using System.Collections.Specialized;
using System.Configuration;

namespace Betzalel.Infrastructure.SettingProviders
{
  public class AppSettingsProvider : ISettingsProvider
  {
    private readonly NameValueCollection _appSettings;
    private readonly bool _trimValueSpaces;

    public AppSettingsProvider(bool trimValueSpaces = true)
    {
      _appSettings = ConfigurationManager.AppSettings;

      _trimValueSpaces = trimValueSpaces;
    }

    public virtual T GetSetting<T>(string settingName)
    {
      var value = _appSettings[settingName];

      if (value == null)
        throw new Exception("Configuration file does not contain the setting \"" + settingName + "\".");

      if (_trimValueSpaces)
        value = value.Trim();

      return (T)Convert.ChangeType(value, typeof(T));
    }

    public virtual T GetSettingOrDefault<T>(string settingName, T defaultValue)
    {
      var value = _appSettings[settingName];

      if (value == null)
        return defaultValue;

      if (_trimValueSpaces)
        value = value.Trim();

      return (T)Convert.ChangeType(value, typeof(T));
    }
  }
}
