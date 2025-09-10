namespace BuildTools.Build;

public class BuildRuntimeState
{
    // User choices
    public bool IsPublish { get; set; }
    public bool IsPreRelease { get; set; }
    public bool BuildDocker { get; set; } = true;
    public bool PublishDocker { get; set; } = false;
    
    // Version info
    public Version? NewVersion { get; set; }
    
    // Git state
    public string[] GitStatus { get; set; } = [];
    public string[] GitStaged { get; set; } = [];
    // Command availability
    public bool HasTypeScriptTools { get; set; }
    public bool HasDockerCommand { get; set; }
    
    // File paths (resolved at runtime)
    public string MigrationSqlScript { get; set; } = string.Empty;
    public string? GitHubToken { get; set; }
}