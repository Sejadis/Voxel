using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Timing
{
    public int amount;
    public List<TimeSpan> spans = new List<TimeSpan>();
    public double min;
    public double max;
    public double avg;

    public void Add(TimeSpan span)
    {
        amount++;
        spans.Add(span);
        min = spans.Min().TotalMilliseconds;
        max = spans.Max().TotalMilliseconds;
        avg = new TimeSpan((long) spans.Select(ts => ts.Ticks).Average()).TotalMilliseconds;
    }
}