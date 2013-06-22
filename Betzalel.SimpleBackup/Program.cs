using System;
using System.Reflection;
using Autofac;
using Betzalel.Infrastructure;
using Betzalel.Infrastructure.Loggers;
using Betzalel.Infrastructure.Scheduler;
using Betzalel.SimpleBackup.Services;
using Betzalel.SimpleBackup.Services.Default;

namespace Betzalel.SimpleBackup
{
  class Program
  {
    static void Main()
    {
      InitializeConsole();

      var container = InitializeIoc();

      var log = container.Resolve<ILog>();

      var version = Assembly.GetExecutingAssembly().GetName().Version;

      log.Info("---------------------- Simple Backup v" + version);
      log.Info("------ Console mode - press Escape to exit ------");

      var backupService = container.Resolve<IBackupService>();

      backupService.StartBackup();

      while (Console.ReadKey(true).Key != ConsoleKey.Escape)
      {
      }

      backupService.StopBackup();
    }

    private static void InitializeConsole()
    {
      try
      {
        Console.WindowWidth = 180;
        Console.WindowHeight = 25;

        Console.BufferWidth = 180;
        Console.BufferHeight = 3000;
      }
      catch (Exception e)
      {
        Console.WriteLine(
          "Could not change console size to wide screen mode.\r\n\r\n" + e);
      }
    }

    private static IContainer InitializeIoc()
    {
      var builder = new ContainerBuilder();

      builder.RegisterType<Log4NetLog>().AsImplementedInterfaces().SingleInstance();
      builder.RegisterType<BackupSettingsProvider>().AsImplementedInterfaces().SingleInstance();
      builder.RegisterType<Scheduler>().SingleInstance();

      builder.RegisterType<BackupService>().AsImplementedInterfaces().SingleInstance();
      builder.RegisterType<BackupCompressor>().AsImplementedInterfaces().SingleInstance();
      builder.RegisterType<BackupStorageService>().AsImplementedInterfaces().SingleInstance();
      builder.RegisterType<BackupHistoryService>().AsImplementedInterfaces().SingleInstance();

      return builder.Build();
    }
  }
}
