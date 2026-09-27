# Terminator guide

A task-oriented reference for building CLIs with Terminator. For an overview, see the
[README](../README.md).

## Getting started

### Minimal CLI App

```csharp
using CommandDotNet;
using Terminator.Builder;

var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);

public class RootCommand
{
    [Command(Description = "Say hello")]
    public void Hello()
    {
        Console.WriteLine("Hello, World!");
    }
}
```

Running this without arguments shows an interactive command picker. Running with `hello` executes the command directly.

### With Subcommands

```csharp
using CommandDotNet;
using Spectre.Console;
using Terminator.Builder;

var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);

public class RootCommand
{
    [Subcommand]
    public BuildCommands Build { get; set; } = new();

    [Subcommand]
    public DeployCommands Deploy { get; set; } = new();
}

public class BuildCommands
{
    [Command(Description = "Build the project")]
    public void Run(IAnsiConsole console)
    {
        console.MarkupLine("[green]Building...[/]");
    }

    [Command(Description = "Clean build artifacts")]
    public void Clean()
    {
        Console.WriteLine("Cleaning...");
    }
}

public class DeployCommands
{
    [Command(Description = "Deploy to staging")]
    public void Staging(string environment = "staging")
    {
        Console.WriteLine($"Deploying to {environment}");
    }
}
```

Commands are auto-converted to kebab-case. The above produces: `build run`, `build clean`, `deploy staging`.

### Dependency Injection

Any class implementing `IServiceRegistrar` in the root command's assembly is auto-discovered and invoked:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Terminator.DependencyInjection;

public interface IMyService
{
    string GetData();
}

public class MyService : IMyService
{
    public string GetData() => "data from service";
}

public class ServiceRegistrations : IServiceRegistrar
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
    }
}

// Services are injected into command methods automatically
public class RootCommand
{
    [Command(Description = "Use injected service")]
    public void Fetch(IMyService service, IAnsiConsole console)
    {
        console.WriteLine(service.GetData());
    }
}
```

### Activity Tracking with CliTracker

The activity system provides live progress UI for multi-step operations:

```csharp
using Spectre.Console;
using Terminator.ActivityObserver;
using Terminator.Helper;
using CliWrap;

public class BuildCommands
{
    [Command(Description = "Build and release")]
    public async Task Release(IAnsiConsole console)
    {
        await using var tracker = new CliTracker(console);
        var table = tracker.Table;

        // Announce steps upfront (shows them as pending)
        var step1 = table.Announce("check", "Checking prerequisites");
        var step2 = table.Announce("build", "Building project");
        var step3 = table.Announce("publish", "Publishing package");

        // Start live UI
        tracker.Show();

        // Execute step 1
        step1.Start();
        // ... do work ...
        step1.Stop();

        // Execute step 2 with progress
        step2.Start();
        step2.Report(0.25, "Compiling...");
        step2.Report(0.75, "Linking...");
        step2.Stop("Build complete");

        // Execute step 3
        step3.Start();
        step3.Log(CliLogLevel.Information, "Pushing to NuGet...");
        // ... do work ...
        step3.Stop();
    }
}
```

### ActivityScope - Single Activity Tracking

```csharp
using Terminator.ActivityObserver;

// Create and manage an activity scope
var scope = new ActivityScope("build", "Building the project");

// Subscribe to events
scope.Started += s => Console.WriteLine($"Started: {s.Name}");
scope.Stopped += s => Console.WriteLine($"Stopped: {s.Name}");
scope.ProgressReported += (s, value, msg) => Console.WriteLine($"Progress: {value:P0} {msg}");
scope.LogAdded += (s, level, msg) => Console.WriteLine($"[{level}] {msg}");

scope.Start("Initializing...");
scope.Report(0.5, "Halfway done");
scope.Log(CliLogLevel.Information, "Processing files...");
scope.Stop("Complete");

// Or use using pattern - auto-stops on dispose
using (var step = new ActivityScope("cleanup", "Cleaning up"))
{
    step.Start();
    // ... work ...
} // auto-stops here
```

### ActivityObservationTable - Multi-Activity Aggregation

```csharp
using Terminator.ActivityObserver;

var table = new ActivityObservationTable();

// Subscribe to aggregated events
table.Update += scope => Console.WriteLine($"Update from: {scope.Name}");
table.LogAdded += (scope, level, msg) => Console.WriteLine($"[{scope.Name}] [{level}] {msg}");

// Start creates and immediately starts
using var step1 = table.Start("init", "Initializing");
step1.Log(CliLogLevel.Information, "Loading config...");
step1.Stop();

// Announce creates without starting (shows as pending)
var step2 = table.Announce("build", "Building");
// ... later ...
step2.Start();
step2.Report(0.5, "Compiling");
step2.Stop();

// Get ordered snapshot of all entries
var entries = table.SnapshotOrderedByStartUtc();
foreach (var entry in entries)
{
    Console.WriteLine($"{entry.Name}: started={entry.StartedUtc}, ended={entry.EndedUtc}");
}
```

### Executing Shell Commands with ActivityScope

CliWrap commands can be bound to an ActivityScope for automatic logging:

```csharp
using CliWrap;
using Terminator.ActivityObserver;
using Terminator.Helper;

var table = new ActivityObservationTable();
var scope = table.Start("dotnet-build", "Building .NET project");

// Execute a CliWrap command - stdout/stderr auto-logged to scope
var cmd = Cli.Wrap("dotnet")
    .WithArguments(["build", "MyProject.csproj", "--configuration", "Release"])
    .WithWorkingDirectory("/path/to/project");

var result = await scope.ExecuteAsync(cmd);
// result.StandardOutput, result.StandardError, result.ExitCode

// Suppress stdout logging (e.g., for sensitive output)
var tokenCmd = Cli.Wrap("gh").WithArguments(["auth", "token"]);
var tokenResult = await scope.ExecuteAsync(tokenCmd, pipeToStdOut: false);

scope.Stop();
```

If the command exits with non-zero, `BufferedCommandExecutionException` is thrown:

```csharp
try
{
    await scope.ExecuteAsync(cmd);
}
catch (BufferedCommandExecutionException ex)
{
    Console.WriteLine($"Exit code: {ex.BufferedCommandResult.ExitCode}");
    Console.WriteLine($"Stderr: {ex.BufferedCommandResult.StandardError}");
    Console.WriteLine($"Stdout: {ex.BufferedCommandResult.StandardOutput}");
}
```

### Git Operations

```csharp
using Terminator.Helper;
using Terminator.ActivityObserver;
using Spectre.Console;

var table = new ActivityObservationTable();
var scope = table.Start("git", "Git operations");
var console = AnsiConsole.Console;

// Check for staged/unstaged changes, prompt user if needed
bool canProceed = await GitHelper.CheckAndAskForStagedChanges(scope, console);

// Run arbitrary git commands
var result = await GitHelper.RunGit(scope, "status", "--porcelain");
var logResult = await GitHelper.RunGit(scope, "log", "--oneline", "-5");

// Commit, tag, and push
var version = new Version(1, 2, 3);
await GitHelper.CommitAndTag(scope, ["src/MyProject.csproj"], version);
// Creates commit "(release) 1.2.3", tags "1.2.3", pushes tag
```

### Version Management

```csharp
using Terminator.Helper;

// Read version from .csproj <Version> element
Version current = VersionHelper.GetDotNetProjectVersion("src/MyProject.csproj");

// Increment patch version (1.2.3 -> 1.2.4)
Version bumped = VersionHelper.IncrementDotNetProjectVersion("src/MyProject.csproj");

// Set specific version
VersionHelper.SetDotNetProjectVersion("src/MyProject.csproj", new Version(2, 0, 0));

// Update npm package.json version
VersionHelper.UpdateNpmPackageVersion("package.json", new Version(2, 0, 0));
```

### NuGet Publishing

```csharp
using Terminator.Helper;

// Publish to a configured NuGet source (by name or URL)
await NuGetHelper.PublishAsync(
    sourceNameOrUrl: "github",          // matches name in NuGet.Config
    packagePath: "bin/Release/MyPackage.1.0.0.nupkg",
    apiKey: "ghp_xxxxxxxxxxxx"
);
```

### npm Publishing

```csharp
using Terminator.Helper;
using Terminator.ActivityObserver;

var scope = new ActivityScope("npm", "Publishing npm package");
scope.Start();

await NpmPublishHelper.PublishAsync(
    scope,
    packagePath: "/path/to/package",
    npmRepository: "https://npm.pkg.github.com/myorg",
    apiKey: "ghp_xxxxxxxxxxxx"
);

scope.Stop();
```

### GitHub Token Helper

```csharp
using Terminator.Helper;
using Terminator.ActivityObserver;

var scope = new ActivityScope("auth", "Getting GitHub token");
scope.Start();

// Requires `gh` CLI to be installed and authenticated
string token = await GithubTokenHelper.GetTokenAsync(scope);

scope.Stop();
```

### Command Existence Check

```csharp
using Terminator.Helper;

// Cross-platform check (uses `where` on Windows, `command -v` on Unix)
bool hasDocker = CommandHelper.CommandExists("docker");
bool hasNode = CommandHelper.CommandExists("node");
bool hasGit = CommandHelper.CommandExists("git");
```

### Ctrl+C / Cancellation Support

```csharp
using Spectre.Console;
using Terminator;

// Enable Ctrl+C handling (called automatically by CliBuilder.Initialize)
CtrlCSupport.EnableCtrlC();

var console = AnsiConsole.Console;

// Prompts that respect Ctrl+C
var name = console.AskWithCancel<string>("What is your name?");
var age = await console.AskWithCancelAsync<int>("What is your age?");

// Selection prompts with cancellation
var prompt = new SelectionPrompt<string>()
    .Title("Pick one:")
    .AddChoices(["Option A", "Option B", "Option C"]);
var choice = prompt.ShowWithCancel(console);

// Text prompts with cancellation
var textPrompt = new TextPrompt<string>("Enter value:");
var result = console.PromptWithCancel(textPrompt);

// Access the global cancellation token
CancellationToken token = CtrlCSupport.CancellationTokenSource.Token;
```

### Console Logging Helpers

```csharp
using Spectre.Console;
using Terminator.ActivityObserver;

var console = AnsiConsole.Console;

// Formatted log output with timestamps and icons
console.LogStepStarted("Building project");
// Output: [12:30:45] ▶ Starting: Building project

console.LogStepCompleted("Building project", 3.5);
// Output: [12:30:48] ✓ Completed: Building project (3.5s)

console.LogProgress("Building project", 0.75, "Linking...");
// Output: [12:30:47] ↻ Progress: Building project 75% - Linking...

console.LogMessage("Build", CliLogLevel.Warning, "Deprecated API usage detected");
// Output: [12:30:47] ⚠ Build: Deprecated API usage detected

console.LogMessage("Build", CliLogLevel.Error, "Compilation failed");
// Output: [12:30:47] ✗ Build: Compilation failed
```

### Using IConfiguration for Default Values

```csharp
using Microsoft.Extensions.Configuration;
using Terminator.Builder;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var runner = CliBuilder.Configure<RootCommand>(config);
// Arguments can now fall back to configuration values
```

## Templates

Install templates:
```bash
dotnet new install Terminator.Templates
```

### Simple CLI App
```bash
dotnet new terminator-simple -n MyCli
cd MyCli
dotnet run
```

### Build Tool
```bash
dotnet new terminator-build -n MyBuildTool
cd MyBuildTool
./run.sh        # Linux/macOS
./run.ps1       # Windows
```

## Key Dependencies

| Package | Purpose |
|---------|---------|
| CommandDotNet | Command parsing, help generation, argument binding |
| CommandDotNet.Spectre | Spectre.Console integration for rich output |
| CommandDotNet.NameCasing | Kebab-case command names |
| CommandDotNet.IoC.MicrosoftDependencyInjection | DI integration |
| Spectre.Console | Rich terminal UI (tables, prompts, markup) |
| CliWrap | Process execution wrapper |
| NuGet.Protocol | Programmatic NuGet publishing |

## CliLogLevel Values

| Level | Value | Use |
|-------|-------|-----|
| Debug | 0 | Deep diagnostics |
| StdOut | 1 | Command standard output |
| Information | 2 | Task start/fail notices |
| Success | 3 | Task completion |
| Warning | 4 | Potential issues |
| StdErr | 5 | Command standard error |
| Error | 6 | Task failures |

## Common Patterns

### Complete Build Tool Example

```csharp
using CliWrap;
using CommandDotNet;
using Spectre.Console;
using Terminator.ActivityObserver;
using Terminator.Builder;
using Terminator.Helper;

var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);

public class RootCommand
{
    [Subcommand]
    public BuildCommands Build { get; set; } = new();
}

public class BuildCommands
{
    [Command(Description = "Build and release a new version")]
    public async Task Release(IAnsiConsole console)
    {
        await using var tracker = new CliTracker(console);
        var table = tracker.Table;

        // Check git state first (before starting live UI)
        using (var gitCheck = table.Start("git-check", "Checking git state"))
        {
            if (!await GitHelper.CheckAndAskForStagedChanges(gitCheck, console))
                return;
        }

        // Announce all steps upfront
        var versionStep = table.Announce("version", "Updating version");
        var buildStep = table.Announce("build", "Building project");
        var publishStep = table.Announce("publish", "Publishing package");
        var gitStep = table.Announce("git", "Tagging and pushing");

        tracker.Show();

        try
        {
            versionStep.Start();
            var newVersion = VersionHelper.IncrementDotNetProjectVersion("src/MyProject.csproj");
            versionStep.Stop($"v{newVersion}");

            buildStep.Start();
            var buildCmd = Cli.Wrap("dotnet")
                .WithArguments(["build", "src/MyProject.csproj", "-c", "Release"]);
            await buildStep.ExecuteAsync(buildCmd);
            buildStep.Stop();

            publishStep.Start();
            await NuGetHelper.PublishAsync("nuget.org", $"src/bin/Release/MyProject.{newVersion}.nupkg");
            publishStep.Stop();

            gitStep.Start();
            await GitHelper.CommitAndTag(gitStep, ["src/MyProject.csproj"], newVersion);
            gitStep.Stop();
        }
        catch (BufferedCommandExecutionException ex)
        {
            await tracker.Stop();
            console.MarkupLine($"[red]Command failed: {ex.Message}[/]");
        }
    }
}
```
