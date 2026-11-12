using Orca;

namespace OrcaCli;

/// <summary>Entry point: command dispatch, global options and exit-code mapping.</summary>
internal static class Program
{
    private static readonly Command[] Registry =
    {
        new BuildCommand(),
        new ExportCommand(),
        new InfoCommand(),
        new ListCommand(),
        new VerifyCommand(),
    };

    /// <summary>Tool version, e.g. <c>1.0.0</c>.</summary>
    internal static string ToolVersion { get; } = ReadVersion();

    private static int Main(string[] argv)
    {
        var log = new Log();

        try
        {
            var args = argv.ToList();

            if (Take(args, "-q", "--quiet"))
            {
                log.Verbosity = Verbosity.Quiet;
            }

            if (Take(args, "-v", "--verbose"))
            {
                log.Verbosity = Verbosity.Verbose;
            }

            bool help = Take(args, "-h", "--help");

            if (Take(args, "-V", "--version"))
            {
                Console.WriteLine($"orca-cli {ToolVersion}");
                return ExitCodes.Ok;
            }

            string? name = args.Count > 0 && !args[0].StartsWith('-') ? args[0] : null;

            if (help)
            {
                if (name is not null && Find(name) is { } command)
                {
                    HelpRenderer.Command(command.Help);
                }
                else
                {
                    HelpRenderer.Overview(HelpList(), ToolVersion);
                }

                return ExitCodes.Ok;
            }

            if (name is null)
            {
                HelpRenderer.Overview(HelpList(), ToolVersion);
                return ExitCodes.Error;
            }

            if (Is(name, "help"))
            {
                string? topic = args.Count > 1 ? args[1] : null;
                if (topic is null)
                {
                    HelpRenderer.Overview(HelpList(), ToolVersion);
                }
                else if (Find(topic) is { } subject)
                {
                    HelpRenderer.Command(subject.Help);
                }
                else
                {
                    throw new CliException($"Unknown command '{topic}'. Run 'orca-cli --help'.");
                }

                return ExitCodes.Ok;
            }

            if (Is(name, "version"))
            {
                Console.WriteLine($"orca-cli {ToolVersion}");
                return ExitCodes.Ok;
            }

            Command selected = Find(name)
                ?? throw new CliException($"Unknown command '{name}'. Run 'orca-cli --help' for the command list.");

            args.RemoveAt(0);

            Configuration configuration = Configuration.Load();
            var context = new CommandContext(log, configuration);

            string[] valueOptions = selected.ValueOptions
                .Concat(CommandContext.CommonValueOptions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ArgSet parsed = ArgumentParser.Parse(args, valueOptions);
            return selected.Execute(context, parsed);
        }
        catch (CliException ex)
        {
            log.Error(ex.Message);
            return ex.ExitCode;
        }
        catch (OrcaException ex)
        {
            log.Error(ex.Message);
            return ExitCodes.Error;
        }
        catch (Exception ex)
        {
            log.Error(ex.Message);
            if (log.Verbosity == Verbosity.Verbose)
            {
                Console.Error.WriteLine(ex);
            }

            return ExitCodes.Error;
        }
    }

    private static Command? Find(string name) =>
        Registry.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static CommandHelp[] HelpList() =>
        Registry.Select(static c => c.Help).OrderBy(static h => h.Name, StringComparer.Ordinal).ToArray();

    private static bool Is(string value, string expected) =>
        value.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static bool Take(List<string> args, params string[] names)
    {
        bool found = false;
        foreach (string name in names)
        {
            found |= args.Remove(name);
        }

        return found;
    }

    private static string ReadVersion()
    {
        Version? version = typeof(Program).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
