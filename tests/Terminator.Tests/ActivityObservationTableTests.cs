using Terminator.ActivityObserver;

namespace Terminator.Tests;

public class ActivityObservationTableTests
{
    [Fact]
    public void Announce_AddsPendingEntryWithoutStarting()
    {
        using var table = new ActivityObservationTable();

        table.Announce("build", "Building");

        var entry = Assert.Single(table.SnapshotOrderedByStartUtc());
        Assert.Equal("build", entry.Name);
        Assert.Equal("Building", entry.Description);
        Assert.Null(entry.StartedUtc);
        Assert.Null(entry.EndedUtc);
    }

    [Fact]
    public void Start_RecordsStartAndStopTimes()
    {
        using var table = new ActivityObservationTable();

        var scope = table.Start("build");
        scope.Stop();

        var entry = Assert.Single(table.Entries);
        Assert.NotNull(entry.StartedUtc);
        Assert.NotNull(entry.EndedUtc);
        Assert.True(entry.EndedUtc >= entry.StartedUtc);
    }

    [Fact]
    public void ScopeEvents_AreAggregatedIntoEntry()
    {
        using var table = new ActivityObservationTable();
        var updates = 0;
        table.Update += _ => updates++;

        var scope = table.Start("build");
        scope.Report(0.5, "Halfway");
        scope.Log(CliLogLevel.Error, "boom");

        var entry = Assert.Single(table.Entries);
        Assert.Equal(0.5, entry.LastProgress);
        Assert.Equal("Halfway", entry.LastProgressMessage);
        var log = Assert.Single(entry.Logs);
        Assert.Equal(CliLogLevel.Error, log.Level);
        Assert.Equal("boom", log.Message);
        Assert.True(updates >= 3);
    }

    [Fact]
    public void SnapshotOrderedByStartUtc_PreservesAnnouncementOrder()
    {
        using var table = new ActivityObservationTable();

        table.Announce("first");
        table.Announce("second");
        table.Announce("third");

        Assert.Equal(
            ["first", "second", "third"],
            table.SnapshotOrderedByStartUtc().Select(e => e.Name));
    }

    [Fact]
    public void Dispose_UnbindsScopes()
    {
        var table = new ActivityObservationTable();
        var updates = 0;
        table.Update += _ => updates++;
        var scope = table.Announce("build");

        table.Dispose();
        scope.Start();

        Assert.Equal(0, updates);
        Assert.Empty(table.Entries);
    }
}
