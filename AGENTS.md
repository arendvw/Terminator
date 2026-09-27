# Contributor notes

Guidance for people and coding agents working in this repository. For how to *use* Terminator,
see the [README](README.md) and the [guide](docs/guide.md).

## Layout

```
src/                         Terminator library (NuGet package "Terminator")
  Builder/                   CliBuilder: entry point, DI + CommandDotNet wiring
  RootCommand/               Interactive command picker and executor
  DependencyInjection/       IServiceRegistrar discovery
  ActivityObserver/          ActivityScope, ActivityObservationTable, CliTracker, live renderer
  Helper/                    Git, version, NuGet, npm, GitHub token and CliWrap helpers
  CtrlCSupport.cs            Ctrl+C handling and cancellable prompts
tests/Terminator.Tests/      xUnit v3 tests
template/                    dotnet new templates (NuGet package "Terminator.Templates")
  Simple/                    terminator-simple
  Builder/                   terminator-build
build/                       This repo's own build tool, built with Terminator (run via ./build.sh)
docs/                        Usage guide and maintainer release guide
.github/                     CI, publish workflow and Dependabot config
```

## Commands

```bash
dotnet build Terminator.sln
dotnet test --solution Terminator.sln   # Microsoft.Testing.Platform, opted in via global.json
```

CI (`.github/workflows/ci.yml`) also packs both packages and builds a project generated from each
template against the freshly packed library. Run those steps locally when changing templates.

## Conventions

- **Target frameworks:** the library and tests target every .NET release that is currently in
  support; the build tool and templates target the latest stable release. The lists live in
  `Directory.Build.props` (`TerminatorTargetFrameworks`, `TerminatorLatestTargetFramework`).
  Template files can't import that file, so they hard-code the latest framework; CI fails if
  they drift. When a new .NET version ships or one leaves support, update `Directory.Build.props`,
  the templates and their run scripts, and `dotnet-version` in the CI workflow.
- **Versions come from git tags** (MinVer). Never add a `<Version>` to a project file. Releasing
  is pushing a tag; see [docs/internal-release.md](docs/internal-release.md).
- **Spectre.Console** is pinned transitively by `CommandDotNet.Spectre`. Don't add an explicit
  Spectre.Console reference in consumers of the library; two versions cause CS0012. Update the
  CommandDotNet packages together.
- Commands are exposed in kebab-case via `CommandDotNet.NameCasing`.
- Keep the [guide](docs/guide.md) in sync when changing public APIs.
