using Terminator.ActivityObserver;

namespace Terminator.Tests;

public class ActivityScopeTests
{
    [Fact]
    public void Start_RaisesStartedOnlyOnce()
    {
        var scope = new ActivityScope("build", "Building");
        var started = 0;
        scope.Started += _ => started++;

        scope.Start();
        scope.Start();

        Assert.Equal(1, started);
    }

    [Fact]
    public void Report_ClampsProgressBetweenZeroAndOne()
    {
        var scope = new ActivityScope("build");
        scope.Start();

        scope.Report(1.5);
        Assert.Equal(1d, scope.Progress);

        scope.Report(-0.5);
        Assert.Equal(0d, scope.Progress);
    }

    [Fact]
    public void Report_RaisesProgressWithMessage()
    {
        var scope = new ActivityScope("build");
        (double Value, string? Message)? reported = null;
        scope.ProgressReported += (_, value, message) => reported = (value, message);
        scope.Start();

        scope.Report(0.25, "Compiling");

        Assert.Equal((0.25, "Compiling"), reported);
        Assert.Equal("Compiling", scope.ProgressMessage);
    }

    [Fact]
    public void Stop_CompletesProgressAndRaisesStopped()
    {
        var scope = new ActivityScope("build");
        var stopped = false;
        scope.Stopped += _ => stopped = true;
        scope.Start();

        scope.Stop("Done");

        Assert.True(stopped);
        Assert.Equal(1d, scope.Progress);
        Assert.Equal("Done", scope.ProgressMessage);
    }

    [Fact]
    public void Log_RaisesLogAdded()
    {
        var scope = new ActivityScope("build");
        (CliLogLevel Level, string Message)? logged = null;
        scope.LogAdded += (_, level, message) => logged = (level, message);
        scope.Start();

        scope.Log(CliLogLevel.Warning, "careful");

        Assert.Equal((CliLogLevel.Warning, "careful"), logged);
    }

    [Fact]
    public void Dispose_StopsStartedScope()
    {
        var scope = new ActivityScope("build");
        var stopped = false;
        scope.Stopped += _ => stopped = true;
        scope.Start();

        scope.Dispose();

        Assert.True(stopped);
        Assert.Null(scope.Activity);
    }

    [Fact]
    public void Dispose_DoesNotStopUnstartedScope()
    {
        var scope = new ActivityScope("build");
        var stopped = false;
        scope.Stopped += _ => stopped = true;

        scope.Dispose();

        Assert.False(stopped);
    }
}
