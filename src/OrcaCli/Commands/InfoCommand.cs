using System.Diagnostics;
using System.Runtime.InteropServices;
using Orca;

namespace OrcaCli;

/// <summary><c>orca-cli info</c> — shows the resolved configuration and, with <c>--check</c>, proves ORCA loads.</summary>
internal sealed class InfoCommand : Command
{
    public override string Name => "info";

    public override string Summary => "Show resolved paths and verify that ORCA loads.";

    public override IReadOnlyList<string> ValueOptions { get; } = new[]
    {
        "--dll", "--runtime",
    };

    public override IReadOnlyList<string> FlagOptions { get; } = new[]
    {
        "--check",
    };

    public override CommandHelp Help => new()
    {
        Name = Name,
        Summary = Summary,
        Usage =
        [
            "orca-cli info [--check] [--json]",
        ],
        Details =
            "Prints the tool version, process width, the pborc.dll and runtime folder that would be " +
            "used, whether they exist, the DLL's file version and which appsettings.json files were " +
            "read. --check additionally loads the DLL and opens/closes an ORCA session.",
        Options = WithCommon(
            new OptionHelp("--check", "load pborc.dll and open an ORCA session")),
        Examples =
        [
            "orca-cli info",
            "orca-cli info --check --json"
        ],
        ExitCodeLines = ExitCodeHelp(),
    };

    public override int Execute(CommandContext context, ArgSet args)
    {
        context.ApplyCommonOptions(args);
        ValidateOptions(args);

        string dll = context.Config.Resolve(context.DllPath ?? OrcaRuntime.DefaultDllPath);
        string runtime = context.Config.Resolve(context.RuntimePath ?? OrcaRuntime.DefaultRuntimePath);
        bool dllExists = File.Exists(dll);
        bool runtimeExists = Directory.Exists(runtime);
        string? dllVersion = dllExists ? FileVersionInfo.GetVersionInfo(dll).FileVersion : null;

        string? orcaStatus = null;
        bool sessionOpened = false;

        if (args.Has("--check"))
        {
            try
            {
                context.InitializeOrca();
                using OrcaSession session = OrcaSession.Open();
                session.ConfigureSession(new OrcaSessionOptions { Debug = true });
                sessionOpened = true;
                orcaStatus = "session opened and closed";
            }
            catch (Exception ex) when (ex is OrcaException or InvalidOperationException or DllNotFoundException)
            {
                orcaStatus = ex.Message;
                context.Log.Error($"ORCA check failed: {ex.Message}");
            }
        }

        string process = $"{RuntimeInformation.ProcessArchitecture} " +
                         $"({(Environment.Is64BitProcess ? "64-bit" : "32-bit")} process, .NET {Environment.Version})";

        context.Log.Info($"Version : orca-cli {Program.ToolVersion}");
        context.Log.Info($"Process : {process}");
        context.Log.Info($"DLL     : {dll}{(dllExists ? string.Empty : "  (missing)")}" +
                         (dllVersion is null ? string.Empty : $"  v{dllVersion}"));
        context.Log.Info($"Runtime : {runtime}{(runtimeExists ? string.Empty : "  (missing)")}");
        context.Log.Info($"Encoding: {context.Encoding}");
        if (args.Has("--check"))
        {
            context.Log.Info(sessionOpened
                ? "Check   : ORCA loaded, session opened"
                : "Check   : ORCA failed to load");
        }

        context.Log.Info(context.Config.Files.Count == 0
            ? "Config  : none (no appsettings.json found)"
            : $"Config  : {string.Join(", ", context.Config.Files)}");

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                version = Program.ToolVersion,
                process,
                dll = new { path = dll, exists = dllExists, fileVersion = dllVersion },
                runtime = new { path = runtime, exists = runtimeExists },
                encoding = context.Encoding.ToString(),
                config = context.Config.Files,
                check = args.Has("--check")
                    ? new { attempted = true, sessionOpened, status = orcaStatus }
                    : null,
            });
        }

        if (args.Has("--check") && !sessionOpened)
        {
            return ExitCodes.Error;
        }

        return ExitCodes.Ok;
    }
}
