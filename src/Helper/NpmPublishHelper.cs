using System;
using System.Threading.Tasks;
using CliWrap;
using Terminator.ActivityObserver;
using Terminator.Helper;

namespace Terminator.Helper;

public static class NpmPublishHelper
{
    /// <summary>
    /// Publishes a .nupkg without any console output. Throws on errors.
    /// </summary>
    public static async Task PublishAsync(
        ActivityScope scope,
        string packagePath,
        string npmRepository,
        string apiKey)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));


        //npm publish --registry=https://npm.pkg.github.com/arendvw --//npm.pkg.github.com/:_authToken=TOKEN
        var isValid = Uri.TryCreate(npmRepository, UriKind.Absolute, out var uri);
        if (!isValid || uri == null)
            throw new ArgumentException("Invalid npm repository URL: " + npmRepository, nameof(npmRepository));
        var shortRepositoryName = uri?.Host;
        

        var path = Environment.GetEnvironmentVariable("PATH");
        var cmd = CliWrap.Cli.Wrap("npm")
        .WithArguments([$"--registry={npmRepository}", $"--{shortRepositoryName}:_authToken=NPM_TOKEN", "publish"])
        .WithEnvironmentVariables(e => e.Set("NPM_TOKEN", apiKey))
        .WithWorkingDirectory(packagePath);

        await scope.ExecuteAsync(cmd);
    }
}