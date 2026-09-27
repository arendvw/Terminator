using Terminator.Helper;

namespace Terminator.Tests;

public sealed class VersionHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("terminator-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteProject(string propertyGroupContent)
    {
        var path = Path.Combine(_dir, "Test.csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {propertyGroupContent}
              </PropertyGroup>
            </Project>
            """);
        return path;
    }

    [Fact]
    public void GetDotNetProjectVersion_ReadsVersionElement()
    {
        var project = WriteProject("<Version>1.2.3</Version>");

        Assert.Equal(new Version(1, 2, 3), VersionHelper.GetDotNetProjectVersion(project));
    }

    [Fact]
    public void GetDotNetProjectVersion_FallsBackToFileVersion()
    {
        var project = WriteProject("<FileVersion>4.5.6</FileVersion>");

        Assert.Equal(new Version(4, 5, 6), VersionHelper.GetDotNetProjectVersion(project));
    }

    [Fact]
    public void GetDotNetProjectVersion_ThrowsWhenNoVersionPresent()
    {
        var project = WriteProject("<OutputType>Exe</OutputType>");

        Assert.Throws<InvalidOperationException>(() => VersionHelper.GetDotNetProjectVersion(project));
    }

    [Fact]
    public void IncrementDotNetProjectVersion_BumpsPatchAndSyncsAllVersionElements()
    {
        var project = WriteProject("""
            <Version>1.2.3</Version>
            <FileVersion>1.2.3</FileVersion>
            <AssemblyVersion>1.2.3</AssemblyVersion>
            """);

        var bumped = VersionHelper.IncrementDotNetProjectVersion(project);

        Assert.Equal(new Version(1, 2, 4), bumped);
        var content = File.ReadAllText(project);
        Assert.Contains("<Version>1.2.4</Version>", content);
        Assert.Contains("<FileVersion>1.2.4</FileVersion>", content);
        Assert.Contains("<AssemblyVersion>1.2.4</AssemblyVersion>", content);
        Assert.DoesNotContain("<?xml", content);
    }

    [Fact]
    public void SetDotNetProjectVersion_ThrowsWhenVersionElementMissing()
    {
        var project = WriteProject("<FileVersion>1.0.0</FileVersion>");

        Assert.Throws<InvalidOperationException>(
            () => VersionHelper.SetDotNetProjectVersion(project, new Version(2, 0, 0)));
    }

    [Fact]
    public void UpdateNpmPackageVersion_ReplacesVersionAndKeepsOtherFields()
    {
        var packageJson = Path.Combine(_dir, "package.json");
        File.WriteAllText(packageJson, """{ "name": "my-pkg", "version": "0.0.1" }""");

        VersionHelper.UpdateNpmPackageVersion(packageJson, new Version(2, 0, 0));

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJson));
        Assert.Equal("2.0.0", doc.RootElement.GetProperty("version").GetString());
        Assert.Equal("my-pkg", doc.RootElement.GetProperty("name").GetString());
    }
}
