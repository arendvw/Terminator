using System.Reflection.Metadata.Ecma335;
using Spectre.Console;
using CliWrap;
using CliWrap.Buffered;
using System.Threading.Tasks;
using Terminator.ActivityObserver;
using System.Collections.Generic;
using System;
using System.Linq;
using Terminator.Helper;

namespace Terminator.Helper;
public class GitStatusResult
{
    public int StagedCount { get; set; }
    public int UnstagedCount { get; set; }
    public bool Proceed { get; set; }
}

public static class GitHelper
{
    public static async Task<bool> CheckAndAskForStagedChanges(ActivityScope scope, IAnsiConsole console)
    {
        var stagedCount = await RunGitAndCountLines(scope,"diff", "--name-only", "--cached");
        var unstagedCount = await RunGitAndCountLines(scope,"diff","--name-only");

        if (stagedCount != 0)
        {
            console.MarkupLine("[red]There are staged changes. Please unstage or commit them before proceeding.[/]");
            return false;
        }
        if (unstagedCount != 0)
        {
            return console.Confirm("There are unstaged changes in the repository. Do you want to proceed?");
        }
        return true;
    }
    
    public static async Task CommitAndTag(ActivityScope scope, List<string> files, Version version)
    {
        var shortVersion = new Version(version.Major, version.Minor, version.Build);
        await RunGit(scope,["commit", ..files, "-m", $"(release) {shortVersion}"]);
        await RunGit(scope,["tag", "-a", shortVersion.ToString(), "-m", $"[Release] release of {shortVersion}"]);
        await RunGit(scope,["push", "origin", $"refs/tags/{shortVersion}"]);
    }

    public static async Task<BufferedCommandResult> RunGit(ActivityScope scope, params string[] args)
    {
        return await RunGit(scope,args.ToList());
    }
    
    public static async Task<BufferedCommandResult> RunGit(ActivityScope scope, IEnumerable<string> args)
    {
        var cmd = Cli.Wrap("git")
            .WithArguments(args);
        return await scope.ExecuteAsync(cmd);
    }
    private static Task<int> RunGitAndCountLines(ActivityScope scope, params string[] args)
    {
        return RunGitAndCountLines(scope, args.ToList());
        
    }
    private static async Task<int> RunGitAndCountLines(ActivityScope scope, IEnumerable<string> args)
    {
        var output = await RunGit(scope, args);
        return output.StandardOutput.Split(['\n', '\r' ], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}