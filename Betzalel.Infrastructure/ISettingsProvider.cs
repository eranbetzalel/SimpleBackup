namespace Betzalel.Infrastructure
{
  public interface ISettingsProvider
  {
    T GetSetting<T>(string settingName);
    T GetSettingOrDefault<T>(string settingName, T defaultValue);
  }
}
