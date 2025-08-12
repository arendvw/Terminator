using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Terminator.ActivityObserver;

namespace Terminator.Helper;

/// <summary>
/// Helper methods for CLI wrap in an ActivityScope 
/// </summary>
public static class CommandHelperExtensions
{

    /// <summary>
    /// Execute a CLI Wrap command, and bind it to the ActionTable
    /// Logs, exceptions and error output is captured, but can be hidden on request
    /// Errors are reported and thrown
    /// </summary>
    /// <param name="scope">ActivityScope of the current command</param>
    /// <param name="command">CliWrap Command</param>
    /// <param name="pipeToStdOut">If false: the stdout of this command is not logged (e.g. for authentication requests of sensitive data)</param>
    /// <returns></returns>
    /// <exception cref="BufferedCommandExecutionException"></exception>
    public static async Task<BufferedCommandResult> ExecuteAsync(this ActivityScope scope, Command command, bool pipeToStdOut = true)
    {
        scope.Log(CliLogLevel.Information, GetCommandString(command));
        var stdOutBuf = new StringBuilder();
        var stdErrBuf = new StringBuilder();

        var stdOutPipe = pipeToStdOut
            ? PipeTarget.Merge(
                PipeTarget.ToDelegate(s => scope.Log(CliLogLevel.StdOut, s)),
                PipeTarget.ToStringBuilder(stdOutBuf)
            ) : PipeTarget.ToStringBuilder(stdOutBuf);
        var cmd = command
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(stdOutPipe)
            .WithStandardErrorPipe(PipeTarget.Merge(
                PipeTarget.ToDelegate(s => scope.Log(CliLogLevel.StdErr, s)),
                PipeTarget.ToStringBuilder(stdErrBuf)
            ));
        var result = await cmd.ExecuteAsync();
        var bufferedCommandResult = new BufferedCommandResult(result.ExitCode, result.StartTime, result.ExitTime, stdOutBuf.ToString(), stdErrBuf.ToString());
        if (result.ExitCode != 0)
        {
            scope.Fail();
        }
        return result.ExitCode != 0 ? throw new BufferedCommandExecutionException(command, bufferedCommandResult) : bufferedCommandResult;
    }

    public static string GetCommandString(CliWrap.Command command)
    {
        return $"Executing {command.TargetFilePath} {command.Arguments} [{command.WorkingDirPath}]";
    }
}