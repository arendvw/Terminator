using CommandDotNet;
using JetBrains.Annotations;
using Spectre.Console;
using Terminator;
using Terminator.ActivityObserver;
using Terminator.Helper;

namespace BuildTools.Build;

[UsedImplicitly]
public class BuildCommands
{
    [UsedImplicitly]
    [Command(Description = "Tag a new release. Pushing the tag triggers the publish workflow.")]
    public async Task Release(IAnsiConsole console)
    {
        // Versions are derived from git tags by MinVer and packages are published by
        // .github/workflows/publish.yml on tag push, so releasing is just creating a tag.
        await using var tracker = new CliTracker(console);
        var table = tracker.Table;

        // Interactive steps must run before the live UI starts.
        Version version;
        using (var gitCheck = table.Start("git", "Checking git state"))
        {
            if (!await GitHelper.CheckAndAskForStagedChanges(gitCheck, console))
            {
                return;
            }

            var latest = await GetLatestVersionTagAsync(gitCheck);
            var suggested = latest is null
                ? new Version(1, 0, 0)
                : new Version(latest.Major, latest.Minor, latest.Build + 1);

            var prompt = new TextPrompt<string>("Version to release:")
                .DefaultValue(suggested.ToString(3))
                .Validate(value => Version.TryParse(value, out _), "[red]Not a valid version[/]");
            version = new Version(console.PromptWithCancel(prompt)!);
        }

        var tag = version.ToString(3);
        var tagStep = table.Announce("tag", $"Tagging release {tag}");
        tracker.Show();

        try
        {
            tagStep.Start();
            await GitHelper.RunGit(tagStep, "tag", "-a", tag, "-m", $"[Release] release of {tag}");
            await GitHelper.RunGit(tagStep, "push", "origin", tag);
            tagStep.Stop($"Pushed tag {tag} - the publish workflow will release it");
        }
        catch (BufferedCommandExecutionException ex)
        {
            await tracker.Stop();
            console.MarkupLine("[bold red]Release failed[/]");
            console.MarkupLine($"[red]{ex.Command.TargetFilePath} {ex.Command.Arguments}[/]");
            console.MarkupLine($"[grey]{ex.BufferedCommandResult.StandardError}[/]");
        }
    }

    /// <summary>Returns the highest existing version tag, or null if there are none.</summary>
    private static async Task<Version?> GetLatestVersionTagAsync(ActivityScope scope)
    {
        var result = await GitHelper.RunGit(scope, "tag", "--list", "--sort=-v:refname");
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Version.TryParse(line.Trim(), out var version))
            {
                return version;
            }
        }
        return null;
    }
}
