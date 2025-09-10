# Terminator

Terminator is a framework for building user-friendly command-line interfaces (CLIs) in .NET, built on `CommandDotNet`, `Spectre.Console` and `Microsoft.Extensions.DependencyInjection`. 

It's designed for creating tools that are easy to use and maintain, especially those that act as a "runner" for other projects.

## Minimal example

When a `Terminator`-based app is run without arguments, it shows an interactive drop-down list of all available commands. This makes the tool's capabilities discoverable without needing to memorize commands.

```dotnet new console
dotnet add Terminator
dotnet add CommandDotNet
```

This is enabled by a simple bootstrap process in your `Program.cs`:

```csharp
using CommandDotNet;
using Terminator.Builder;
// Initialize the CLI application with your root command class
var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);

// Define your commands
public class RootCommand
{
    [Command]
    public void TestCommand()
    {
        Console.WriteLine("Hello, World!");
    }
}
```

## Dependency injection

Terminator uses `Microsoft.Extensions.DependencyInjection` to manage services. You can register your own services by creating a class that implements the `IServiceRegistrar` interface.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Terminator.DependencyInjection;

// Define your service
public interface IMyService
{
    string GetMessage();
}

public class MyService : IMyService
{
    public string GetMessage() => "Hello from a service!";
}

// Register your service
public class MyServiceInjections : IServiceRegistrar
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IMyService, MyService>();
    }
}
```

You can then inject your services into your command methods or constructors.

```csharp
public class RootCommand
{
    [Command]
    public void TestCommand(IMyService myService)
    {
        Console.WriteLine(myService.GetMessage());
    }
}
```

## Running the Application

The templates generate helper scripts to make running your application easy across different platforms.

*   **On Linux/macOS:** Use the `run.sh` script.
    ```bash
    ./run.sh
    ```

*   **On Windows:** Use the `run.ps1` script with PowerShell.
    ```powershell
    ./run.ps1
    ```

### Getting Help

All commands include generated help, which you can access by passing the `-h` or `--help` flag.

```bash
# Get help for the root command
./run.sh -h

# Get help for a subcommand
./run.sh sub -h
```

## Command Completions

`CommandDotNet` provides support for tab completions in your shell, which can significantly improve the user experience by autocompleting command and option names.

To enable completions, you first need to generate a script for your shell and then configure your shell to use it.

### Generating the Script

You can generate the completion script by running the `complete` command, specifying your shell (`bash`, `zsh`, `fish`, or `pwsh`).

```bash
# For zsh
./run.sh complete zsh > ~/.your-app-completions.zsh

# For bash
./run.sh complete bash > ~/.your-app-completions.bash

# For fish
./run.sh complete fish > ~/.config/fish/completions/your-app.fish

# For PowerShell
./run.ps1 complete pwsh | Out-File -FilePath $PROFILE -Append
```

### Enabling the Script

After generating the script, you need to source it in your shell's profile.

*   **For zsh:** Add the following line to your `~/.zshrc` file:
    ```bash
    source ~/.your-app-completions.zsh
    ```

*   **For bash:** Add the following line to your `~/.bashrc` or `~/.bash_profile`:
    ```bash
    source ~/.your-app-completions.bash
    ```

*   **For fish:** The script is automatically loaded because it's in the `completions` directory.

*   **For PowerShell:** The previous command already appended the necessary line to your PowerShell profile.

After updating your profile, restart your shell or source the profile (e.g., `source ~/.zshrc`) for the changes to take effect. You can then use the tab key to autocomplete your application's commands.

## Templates

This package comes with templates to get you started with a new Terminator-based CLI application.

### Installation

To use the templates, you first need to install them from NuGet. If you haven't already, you'll need to add the GitHub package repository for `arendvw` as a source. You can do this with the following command:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/arendvw/index.json" -n "github-arendvw"
```

Once the source is configured, you can install the templates:

```bash
dotnet new install Terminator.Templates
```

### Usage

Once the templates are installed, you can create new projects using `dotnet new`.

#### Simple CLI App

This template creates a new command-line application with a basic project structure. It includes a root command and an example subcommand, and is pre-configured to use `CommandDotNet` for argument parsing and dependency injection.

To create a minimal Terminator-based CLI app with dependency injection, run:

```bash
dotnet new terminator-simple -n YourAppName
```

#### Build Tool

This template creates a project for build and release automation. The project includes helper scripts (`run.sh` for Linux/macOS and `run.ps1` for Windows) to execute build commands. The project structure is set up for build automation tasks.

To create a new build and release tool, run:

```bash
dotnet new terminator-build -n YourBuildToolName
```
