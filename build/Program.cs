using BuildTools;
using Terminator.Builder;
var app = CliBuilder.Initialize<RootCommand>();
await app.RunAsync(args);