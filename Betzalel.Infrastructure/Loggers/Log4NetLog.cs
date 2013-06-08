using System;
using System.IO;
using System.Reflection;
using Betzalel.Infrastructure.Utilities;

namespace Betzalel.Infrastructure.Loggers
{
  public class Log4NetLog : ILog
  {
    private readonly log4net.ILog _logger;
    private readonly bool _isDebug;

    public Log4NetLog()
    {
      var assembly = Assembly.GetEntryAssembly();

      var log4NetConfigFilePath = PathUtil.MapToExecutable("Log4Net.xml");

      if (!File.Exists(log4NetConfigFilePath))
      {
        log4net.Config.XmlConfigurator.Configure();
      }
      else
      {
        log4net.Config.XmlConfigurator.Configure(new FileInfo(log4NetConfigFilePath));
      }

      _logger = log4net.LogManager.GetLogger(assembly.GetName().Name);
      _isDebug = _logger.IsDebugEnabled;
    }

    public void Debug(string message)
    {
      if (!_isDebug)
        return;

      _logger.Debug(message);
    }

    public void Debug(Func<string> messageFunc)
    {
      if (!_isDebug)
        return;

      _logger.Debug(messageFunc());
    }

    public void Info(string message)
    {
      _logger.Info(message);
    }

    public void Warn(string message)
    {
      _logger.Warn(message);
    }

    public void Error(string message)
    {
      _logger.Error(message);
    }

    public void Error(string message, Exception exception)
    {
      _logger.Error(message, exception);
    }

    public void Fatal(string message, Exception exception)
    {
      _logger.Fatal(message, exception);
    }
  }
}
