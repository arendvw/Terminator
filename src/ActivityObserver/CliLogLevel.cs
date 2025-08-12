namespace Terminator.ActivityObserver;

/// <summary>
/// Log levels for the CLI
/// </summary>
public enum CliLogLevel
{
    /// <summary>
    /// Debug log level, show only when diving deep
    /// </summary>
    Debug = 0,
    /// <summary>
    /// Standard output log level
    /// Is produced by CliWrap commands
    /// </summary>
    StdOut = 1,
    /// <summary>
    /// Information notices on tasks starting or failing
    /// </summary>
    Information = 2,
    /// <summary>
    /// Success messages for completed tasks
    /// </summary>
    Success = 3,
    /// <summary>
    /// Warning messages for potential issues
    /// </summary>
    Warning = 4,
    /// <summary>
    /// Standard error messages from CliWrap commands
    /// </summary>
    StdErr = 5,
    /// <summary>
    /// Error messages for failed tasks
    /// </summary>
    Error = 6
}