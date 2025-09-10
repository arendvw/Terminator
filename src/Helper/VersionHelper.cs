using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Spectre.Console;

namespace Terminator.Helper;


public class VersionHelper
{
    public static Version IncrementDotNetProjectVersion(string project, Spectre.Console.IAnsiConsole? console = null)
    {
        var currentVersion = GetDotNetProjectVersion(project, console);
        console?.WriteLine($"[yellow]Current version: {currentVersion}[/]");
        // Increment the version
        var newVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build + 1);
        console?.WriteLine($"[yellow]New version: {newVersion}[/]");
        return SetDotNetProjectVersion(project, newVersion, console);
    }
    public static Version SetDotNetProjectVersion(string project, Version version, Spectre.Console.IAnsiConsole? console = null)
    {
        console?.WriteLine("[yellow]Incrementing version...[/]");
        // Read current version from .csproj
        var doc = XDocument.Load(project);

        // Only update if the element exists
        var fileVersionElement = doc.Descendants("FileVersion").FirstOrDefault();
        if (fileVersionElement != null)
        {
            fileVersionElement.Value = version.ToString(3);
        }
        var versionElement = doc.Descendants("Version").FirstOrDefault();
        if (versionElement != null)
        {
            versionElement.Value = version.ToString(3);
        }
        var assemblyVersion = doc.Descendants("AssemblyVersion").FirstOrDefault();
        if (assemblyVersion != null)
        {
            assemblyVersion.Value = version.ToString(3);
        }

        if (versionElement == null)
        {
            throw new InvalidOperationException("Could not find version property in " + project);
        }
        // Save without XML declaration
        var settings = new System.Xml.XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true
        };
        using var writer = System.Xml.XmlWriter.Create(project, settings);
        doc.Save(writer);
        return version;
    }
    
    public static Version GetDotNetProjectVersion(string project, Spectre.Console.IAnsiConsole? console = null)
    {
        // Read current version from .csproj
        var doc = XDocument.Load(project);
        var versionElement = doc.Descendants("Version").FirstOrDefault()
            ?? doc.Descendants("FileVersion").FirstOrDefault()
            ?? doc.Descendants("AssemblyVersion").FirstOrDefault();

        if (versionElement == null)
        {
            throw new InvalidOperationException($"No version found in project file: {project}");
        }

        return new Version(versionElement.Value);
    }
    public static void UpdateNpmPackageVersion(string packageJsonPath, Version version)
    {
        var json = File.ReadAllText(packageJsonPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var jsonObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (jsonObj == null)
            throw new InvalidOperationException($"Could not parse package.json: {packageJsonPath}");
        jsonObj["version"] = version.ToString();
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(packageJsonPath, updatedJson);
    }
}