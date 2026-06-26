using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Terminator;

namespace Terminator.Helper;

public static class NuGetHelper
{
    /// <summary>
    /// Publishes a .nupkg without any console output. Throws on errors.
    /// </summary>
    public static async Task PublishAsync(
        string sourceNameOrUrl,
        string packagePath,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));

        // Default to the global Ctrl+C / SIGTERM token so an in-flight upload is cancellable.
        if (cancellationToken == default)
        {
            cancellationToken = CtrlCSupport.CancellationTokenSource.Token;
        }

        var settings = Settings.LoadDefaultSettings(root: null);

        // Resolve source (allow well-known fallback for "nuget.org")
        var provider = new PackageSourceProvider(settings);
        var source = provider.LoadPackageSources()
            .FirstOrDefault(s =>
                s.IsEnabled &&
                (s.Name.Equals(sourceNameOrUrl, StringComparison.OrdinalIgnoreCase) ||
                 s.Source.Equals(sourceNameOrUrl, StringComparison.OrdinalIgnoreCase)));

        if (source == null)
            throw new InvalidOperationException($"Package source '{sourceNameOrUrl}' not found.");

        var repo = Repository.Factory.GetCoreV3(source.Source);
        var update = await repo.GetResourceAsync<PackageUpdateResource>(cancellationToken);

        // NuGet.Protocol's PackageUpdateResource.Push has no CancellationToken overload, so an
        // in-flight upload cannot be interrupted; the timeout below bounds it. Honor cancellation
        // at the boundary instead, so a pending cancel aborts before the upload starts.
        cancellationToken.ThrowIfCancellationRequested();

        await update.Push(
            [packagePath],
            symbolSource: null,
            timeoutInSecond: 300,
            disableBuffering: false,
            getApiKey: _ => apiKey,
            getSymbolApiKey: null,
            noServiceEndpoint: false,
            skipDuplicate: false,
            symbolPackageUpdateResource: null,
            log: NullLogger.Instance
        );
    }
}