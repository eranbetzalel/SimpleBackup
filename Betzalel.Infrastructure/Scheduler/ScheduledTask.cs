using System;

namespace Betzalel.Infrastructure.Scheduler
{
  public class ScheduledTask
  {
    public string Name { get; private set; }
    public Action Method { get; private set; }
    public TaskConcurrencyOptions ConcurrencyOptions { get; private set; }
    public object ConcurrencyLock { get; private set; }

    public ScheduledTask(string name, Action method, TaskConcurrencyOptions concurrencyOptions)
    {
      Name = name;
      Method = method;
      ConcurrencyOptions = concurrencyOptions;

      ConcurrencyLock = new object();
    }
  }
}