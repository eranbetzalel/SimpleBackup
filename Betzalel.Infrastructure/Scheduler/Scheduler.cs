using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Betzalel.Infrastructure.Scheduler
{
  public class Scheduler
  {
    private readonly ILog _log;

    private readonly ConcurrentDictionary<string, Timer> _tasksTimers;

    public Scheduler(ILog log)
    {
      _log = log;

      _tasksTimers = new ConcurrentDictionary<string, Timer>();
    }


    public void AddDailyTask(string taskName, Action task, Time time, TaskConcurrencyOptions taskConcurrencyOptions)
    {
      var timeToStart = (DateTime.Today.Add(time.ToTimeSpan())) - DateTime.Now;

      if (timeToStart.Ticks < 0)
        timeToStart = timeToStart.Add(TimeSpan.FromDays(1));

      AddTask(taskName, task, timeToStart, TimeSpan.FromDays(1), taskConcurrencyOptions);
    }

    public void AddTask(
      string taskName, 
      Action task,
      TimeSpan dueTime,
      TimeSpan period, 
      TaskConcurrencyOptions taskConcurrencyOptions)
    {
      var scheduledTask = new ScheduledTask(taskName, task, taskConcurrencyOptions);

      var taskTimer =
        new Timer(
          RunTask,
          scheduledTask,
          Timeout.Infinite,
          Timeout.Infinite);

      if (!_tasksTimers.TryAdd(taskName, taskTimer))
      {
        taskTimer.Dispose();

        throw new Exception("Could not add task " + taskName + ".");
      }

      taskTimer.Change(dueTime, period);
    }

    public void RemoveAll()
    {
      var tasksNames = _tasksTimers.Keys.ToArray();

      Parallel.ForEach(tasksNames, RemoveTask);
    }

    public void RemoveTask(string taskName)
    {
      Timer taskTimer;

      if(!_tasksTimers.TryRemove(taskName, out taskTimer))
        throw new Exception("Could not remove task.");

      taskTimer.Dispose();
    }

    private void RunTask(object state)
    {
      var scheduledTask = (ScheduledTask)state;

      switch (scheduledTask.ConcurrencyOptions)
      {
        case TaskConcurrencyOptions.Skip:
          if (!Monitor.TryEnter(scheduledTask.ConcurrencyLock))
            return;
          break;
        case TaskConcurrencyOptions.Wait:
          Monitor.Enter(scheduledTask.ConcurrencyLock);
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      try
      {
        _log.Debug("Task " + scheduledTask.Name + ": Starting...");

        scheduledTask.Method();
      }
      catch (Exception e)
      {
        _log.Error("Task " + scheduledTask.Name + ": Error occurred while running task.", e);
      }
      finally
      {
        Monitor.Exit(scheduledTask.ConcurrencyLock);

        _log.Debug("Task " + scheduledTask.Name + ": Done.");
      }
    }
  }
}