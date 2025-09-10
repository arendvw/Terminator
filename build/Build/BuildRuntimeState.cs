namespace BuildTools.Build;

public class BuildRuntimeState
{
    // User choices
    public bool IsPublish { get; set; }
    public bool IsPreRelease { get; set; }
    // Version info
    public Version? NewVersion { get; set; }
    public string? GitHubToken { get; set; }
}