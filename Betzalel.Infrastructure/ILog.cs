using System;

namespace Betzalel.Infrastructure
{
  public interface ILog
  {
    void Debug(string message);
    void Debug(Func<string> messageFunc);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Error(string message, Exception exception);
    void Fatal(string message, Exception exception);
  }
}
