using System;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class BrowserRefreshClientScriptTests
{
    [Fact(DisplayName = "Viu Cohesion Web - browser refresh client: the stylesheet branch never reloads the page")]
    public void Create_StylesheetUpdateBranch_ShouldNeverReload()
    {
        // The Viu run-host bridge replays one UpdateStaticFile per tracked stylesheet on every
        // browser connection. A reload anywhere in the stylesheet branch therefore reconnects
        // and replays forever - the continuous-refresh loop observed under Visual Studio.
        // The dotnet-watch client contract reloads only for non-stylesheet paths.
        string script = BrowserRefreshClientScript.Create(new BrowserRefreshConfiguration(
            "ws://127.0.0.1:1/refresh",
            string.Empty,
            string.Empty));

        string stylesheetBranch = ExtractFunction(script, "updateStaticFile");
        string nonStylesheetGuard = "if (!path || !path.endsWith('.css')) { location.reload(); return; }";
        stylesheetBranch.ShouldContain(nonStylesheetGuard);
        stylesheetBranch
            .Replace(nonStylesheetGuard, string.Empty, StringComparison.Ordinal)
            .ShouldNotContain("location.reload");
        stylesheetBranch.ShouldContain("ignoring the update");
    }

    private static string ExtractFunction(string script, string name)
    {
        Match match = Regex.Match(
            script,
            @"function\s+" + Regex.Escape(name) + @"\s*\([^)]*\)\s*\{",
            RegexOptions.Singleline);
        match.Success.ShouldBeTrue($"The client script must declare {name}.");

        int depth = 0;
        for (int index = match.Index + match.Length - 1; index < script.Length; index++)
        {
            if (script[index] == '{')
            {
                depth++;
            }
            else if (script[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return script[match.Index..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"The {name} function body is unbalanced.");
    }
}
