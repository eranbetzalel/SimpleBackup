namespace Betzalel.Infrastructure
{
  public interface ISettingsProvider
  {
    T GetSetting<T>(string settingName);
  }
}
