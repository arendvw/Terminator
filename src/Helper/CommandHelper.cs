using System;

namespace Terminator.Helper;

/// <summary>
/// Helper methods for Command Execution
/// </summary>
public class CommandHelper
{

    public static bool CommandExists(string command)
    {
        // If command is an absolute or relative path, check directly
        if (command.Contains(System.IO.Path.DirectorySeparatorChar) || command.Contains(System.IO.Path.AltDirectorySeparatorChar))
            return System.IO.File.Exists(command) && (IsExecutable(command) || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows));

        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (isWindows)
        {
            psi.FileName = "where";
            psi.ArgumentList.Add(command);
        }
        else
        {
            psi.FileName = "/bin/sh";
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add($"command -v {command}");
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Process was null");
            }
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsExecutable(string path)
    {
        var fileInfo = new System.IO.FileInfo(path);
        return fileInfo.Exists && (fileInfo.Attributes & System.IO.FileAttributes.Directory) == 0 && (fileInfo.Attributes & System.IO.FileAttributes.ReadOnly) == 0;
    }
}