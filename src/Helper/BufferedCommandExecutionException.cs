using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;

namespace Terminator.Helper;

public class BufferedCommandExecutionException(Command command, BufferedCommandResult result)
    : CommandExecutionException(command, result.ExitCode, $"Command failed:\n" +
                                                          $"{command.TargetFilePath} {command.Arguments} in {command.WorkingDirPath}" +
                                                          $" {result.StandardError?.Trim()}")
{
    public BufferedCommandResult BufferedCommandResult { get; set; } = result;
}