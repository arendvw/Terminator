using BuildTools;
using Terminator.Builder;
var app = CliBuilder.Initialize<RootCommand>();
try
{
    return await app.RunAsync(args);
}
catch (OperationCanceledException)
{
    // Ctrl+C / SIGTERM cooperative cancellation: exit with the conventional code.
    return 130;
}
