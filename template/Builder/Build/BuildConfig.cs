namespace BuildTools.Build;


public class BuildConfig
{
    // Project paths
    public static readonly string CoreProject = Path.Combine("src", "Terminator.csproj");   
    public static string NugetPackage(Version version) => Path.Combine("src", "bin", "Release", $"Terminator.{version.Major}.{version.Minor}.{version.Build}.nupkg");
    public static string TemplatePackage(Version version) => Path.Combine("template", "bin", "Release", $"Terminator.Templates.{version.Major}.{version.Minor}.{version.Build}.nupkg");
    // Build settings
    public static readonly string Configuration = "Release";
    // NuGet settings for GitHub Packages
    public static readonly string NuGetSource = "https://nuget.pkg.github.com/arendvw/index.json";
}