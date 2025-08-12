using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Terminator.ActivityObserver;

/// <summary>
/// Monitor a table of activities
/// </summary>
public class ActivityObservationTable : IDisposable
{
    private readonly ConcurrentDictionary<ActivityScope, ObservationEntry> _entries = new();

    public ActivityObservationTable()
    {

    }

    public event Action<ActivityScope>? Update;
    public event Action<ActivityScope>? Announced;
    public event Action<ActivityScope>? Started;
    public event Action<ActivityScope>? Stopped;
    public event Action<ActivityScope, double, string?>? ProgressReported;
    public event Action<ActivityScope, CliLogLevel, string>? LogAdded;

    public IReadOnlyCollection<ObservationEntry> Entries => (IReadOnlyCollection<ObservationEntry>)_entries.Values;


    private void OnStarted(ActivityScope scope)
    {

        if (_entries.TryGetValue(scope, out var e))
            e.StartedUtc = DateTime.UtcNow;

        Started?.Invoke(scope);
        Update?.Invoke(scope);
    }

    private void OnStopped(ActivityScope scope)
    {
        if (_entries.TryGetValue(scope, out var e))
            e.EndedUtc = DateTime.UtcNow;

        Stopped?.Invoke(scope);
        Update?.Invoke(scope);
    }

    private void OnProgress(ActivityScope scope, double value, string? message)
    {
        if (_entries.TryGetValue(scope, out var e))
        {
            e.LastProgress = value;
            e.LastProgressMessage = message;
            e.Progress.Enqueue(new ProgressRecord(DateTime.UtcNow, value, message));
        }
        ProgressReported?.Invoke(scope, value, message);
        Update?.Invoke(scope);
    }

    private void OnLog(ActivityScope scope, CliLogLevel level, string message)
    {
        if (_entries.TryGetValue(scope, out var e))
            e.Logs.Enqueue(new LogRecord(DateTime.UtcNow, level, message));
        LogAdded?.Invoke(scope, level, message);
        Update?.Invoke(scope);
    }

    public IEnumerable<LogRecord> LatestLogs => _entries.Values.SelectMany(e => e.Logs).OrderByDescending(e => e.TimeUtc);

    public ActivityScope Announce(string shortName, string? description = null)
    {
        var scope = new ActivityScope(shortName, description);
        Announce(scope);
        return scope;
    }
    public ActivityScope Start(string shortName, string? description = null)
    {
        var scope = new ActivityScope(shortName, description);
        Announce(scope);
        scope.Start();
        return scope;
    }
    private void Announce(ActivityScope scope)
    {
        Bind(scope);
        _entries[scope] = new ObservationEntry(
            name: scope.Name,
            description: scope.Description,
            announcedUtc: DateTime.UtcNow,
            scope
        );
        Announced?.Invoke(scope);
    }


    public IReadOnlyList<ObservationEntry> SnapshotOrderedByStartUtc() =>
        _entries.Values.OrderBy(v => v.AnnouncedUtc).ToList();

    private void Bind(ActivityScope scope)
    {
        scope.Started += OnStarted;
        scope.Stopped += OnStopped;
        scope.ProgressReported += OnProgress;
        scope.LogAdded += OnLog;
    }

    private void Unbind(ActivityScope scope)
    {
        scope.Started -= OnStarted;
        scope.Stopped -= OnStopped;
        scope.ProgressReported -= OnProgress;
        scope.LogAdded -= OnLog;
    }

    public void Dispose()
    {
        foreach (var item in _entries.Keys)
        {
            Unbind(item);
        }
        _entries.Clear();
    }

    public class ObservationEntry
    {
        public string Name { get; }
        public string? Description { get; }
        public DateTime? StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }

        public double? LastProgress { get; set; }
        public string? LastProgressMessage { get; set; }

        public ConcurrentQueue<ProgressRecord> Progress { get; } = new();
        public ConcurrentQueue<LogRecord> Logs { get; } = new();

        internal ObservationEntry(string name, string? description, DateTime announcedUtc, ActivityScope scope)
        {
            Name = name;
            Description = description;
            AnnouncedUtc = announcedUtc;
        }

        public DateTime AnnouncedUtc { get; set; }
    }

    public readonly record struct ProgressRecord(DateTime TimeUtc, double Value, string? Message);
    public readonly record struct LogRecord(DateTime TimeUtc, CliLogLevel Level, string Message);
}