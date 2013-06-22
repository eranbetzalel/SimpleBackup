using System;

namespace Betzalel.Infrastructure.Scheduler
{
  public class Time
  {
    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public int Second { get; private set; }
    public int Millisecond { get; private set; }

    public Time(int hour, int minute, int second, int millisecond)
    {
      Hour = hour;
      Minute = minute;
      Second = second;
      Millisecond = millisecond;
    }

    public Time(DateTime dateTime)
    {
      Hour = dateTime.Hour;
      Minute = dateTime.Minute;
      Second = dateTime.Second;
      Millisecond = dateTime.Millisecond;
    }

    public TimeSpan ToTimeSpan()
    {
      return new TimeSpan(0, Hour, Minute, Second, Millisecond);
    }
  }
}