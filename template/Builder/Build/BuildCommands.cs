using CliWrap;
using CommandDotNet;
using JetBrains.Annotations;
using Spectre.Console;
using Terminator.ActivityObserver;
using Terminator.Helper;

namespace BuildTools.Build;

[UsedImplicitly]
public class BuildCommands
{
    private readonly BuildRuntimeState _state = new();

    [UsedImplicitly]
    [Command( Description = "Build and release a new version")]
    public async Task Release(IAnsiConsole console, bool silent = false)
    {
        // Create a tracker to show the steps
        await using var tracker = new CliTracker(console);
        var table = tracker.Table;
        using (var gitCheckStep = table.Start("git", "Checking git state"))
        {
            CheckPrerequisites();
            if (!await GitHelper.CheckAndAskForStagedChanges(gitCheckStep, console))
            {
                return;
            }
            GetBuildPrompt(console);
        }

        // Define all possible build steps
        var tokenStep = table.Announce("token", "Getting github token");
        var updateVersionStep = table.Announce("version", "Updating version information");
        var buildApiStep = table.Announce("build", "Generate nuget package");
        var nugetStep = table.Announce("nuget", "Push nuget");
        
        tracker.Show();
        try
        {
            {
                tokenStep.Start();
                _state.GitHubToken = await GithubTokenHelper.GetTokenAsync(tokenStep);
                tokenStep.Stop();
            }
            
            {
                updateVersionStep.Start();
                _state.NewVersion = VersionHelper.IncrementDotNetProjectVersion(BuildConfig.CoreProject);
                updateVersionStep.Stop($"{_state.NewVersion}");
            }

            {
                buildApiStep.Start();
                var serverBuild = Cli.Wrap("dotnet")
                .WithArguments([
                    "build", BuildConfig.CoreProject, "--configuration", BuildConfig.Configuration
                ])
                .WithWorkingDirectory(Environment.CurrentDirectory);
                await buildApiStep.ExecuteAsync(serverBuild);

                if (!File.Exists(BuildConfig.NugetPackage(_state.NewVersion)))
                {
                    throw new FileNotFoundException("NuGet package was not built", BuildConfig.NugetPackage(_state.NewVersion));
                }
                buildApiStep.Stop();
            }

                        {
                nugetStep.Start();
                var nugetPackage = BuildConfig.NugetPackage(_state.NewVersion);
                // Publish NuGet package
                await NuGetHelper.PublishAsync(
                    BuildConfig.NuGetSource,
                    nugetPackage,
                    _state.GitHubToken
                );
                nugetStep.Stop();
            }
        }
        catch (BufferedCommandExecutionException ex)
        {
            await tracker.Stop();
            console.MarkupLine($"[bold red]Exception[/]");
            console.MarkupLine($"[bold red]Command: {ex.Command.TargetFilePath} {ex.Command.Arguments}[/]");
            console.MarkupLine($"[grey] {ex.StackTrace}[/]");
        }
        catch (Exception ex)
        {
            await tracker.Stop();
            console.MarkupLine($"[bold red]Exception[/]");
            console.MarkupLine($"[bold red]{ex.Message}[/]");
            console.MarkupLine($"[grey] {ex.StackTrace}[/]");

        }
        // write a table of results?
    }

    private void CheckPrerequisites()
    {
        _state.HasTypeScriptTools = CommandHelper.CommandExists("tsc") && CommandHelper.CommandExists("npm");
        _state.HasDockerCommand = CommandHelper.CommandExists("docker");
    }

    private void GetBuildPrompt(IAnsiConsole console)
    {
        var choice = console.Prompt(
            new SelectionPrompt<string>()
                .Title("What kind of version do you wish to build?")
                .AddChoices(["Just Build Client", "Prerelease", "Release"]));

        _state.IsPublish = choice != "Just Build Client";
        _state.IsPreRelease = choice == "Prerelease";
        _state.BuildDocker = true;
        _state.PublishDocker = false;
    }
}