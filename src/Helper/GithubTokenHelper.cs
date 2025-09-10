using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using Spectre.Console;
using Terminator.ActivityObserver;
using Terminator.Helper;

namespace Terminator.Helper;

public class GithubTokenHelper
{
    public static async Task<string> GetTokenAsync(ActivityScope scope)
    {
        var cmd = Cli.Wrap("gh").WithArguments(["auth", "token"]);
        var result = await scope.ExecuteAsync(cmd, false);
        var token = result.StandardOutput.Trim();
        if (string.IsNullOrEmpty(token) || token.StartsWith("gh_"))
        {
            throw new InvalidOperationException("Github returned empty or invalid token");
        }

        return token;
    }
}