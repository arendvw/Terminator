using System;
using System.Diagnostics;

namespace Terminator.ActivityObserver;

/// <summary>
/// It's a place where you can show which activities will happen, and if they happened yet. You can pass logs, and it should somehow catch exceptions. 
/// </summary>
public class ActivityScope : IDisposable
{
    public event Action<ActivityScope>? Started;
    public event Action<ActivityScope>? Stopped;
    public event Action<ActivityScope, double, string?>? ProgressReported;
    public event Action<ActivityScope, CliLogLevel, string>? LogAdded;

    public bool Failed { get; set; }
    public string Name { get; }
    public string? Description { get; }
    
    public Activity? Activity { get; private set; }

    public ActivityScope(string name, string? description = null)
    {
        Activity = new Activity(name);
        Name = name;
        Description = description;
        Initialize();
    }
    
    private void Initialize()
    {
        if (!string.IsNullOrWhiteSpace(Description))
            Activity?.SetTag("description", Description); // safe pre-start
    }
    

    public void Start(string? message = null)
    {
        if (message != null)
        {
            ProgressMessage = message;
            Report(Progress, message);    
        }
        if (Activity is null) throw new ObjectDisposedException(nameof(ActivityScope));
        if (Activity.Id is not null) return; // already started
        Activity.Start();
        Started?.Invoke(this);
    }

    public double Progress
    {
        protected set
        {
            var v = Math.Clamp(value, 0d, 1d);
            Activity?.SetTag("progress", v);
        }
        get
        {
            var item = Activity?.GetTagItem("progress");
            return item == null ?  0d : Convert.ToDouble(item);
        }
    }

    public string? ProgressMessage
    {
        get
        {
            var item  = Activity?.GetTagItem("progress.message");
            return item?.ToString();
        }
        protected set
        {
            if (!string.IsNullOrWhiteSpace(value))
                Activity?.SetTag("progress.message", value);
        }
    }

    public void Report(double value, string? message = null)
    {
        Progress = value;
        ProgressMessage = message;
        ProgressReported?.Invoke(this, Progress, ProgressMessage);

    }
    
    public void Report(string message)
    {
        ProgressMessage = message;
        ProgressReported?.Invoke(this, Progress, ProgressMessage);
    }

    public void Log(CliLogLevel level, string message)
    {
        Activity!.AddEvent(new ActivityEvent("log",
            tags: new ActivityTagsCollection { ["message"] = message, ["level"] = level.ToString() }));
        LogAdded?.Invoke(this, level, message);
    }

    public void Stop(string? message = null)
    {
        Progress = 1;
        if (message != null)
        {
            ProgressMessage = message;
        }

        Report(Progress, ProgressMessage);
        Activity!.Stop();
        Stopped?.Invoke(this);
    }

    public void Dispose()
    {
        if (Activity?.Id == null) return;
        Stop();
        Activity = null;
    }

    public void Fail()
    {
        Failed = true;
    }
}