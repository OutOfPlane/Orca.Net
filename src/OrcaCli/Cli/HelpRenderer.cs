namespace OrcaCli;

internal sealed record OptionHelp(string Names, string Description);

/// <summary>Everything needed to render help for one command.</summary>
internal sealed class CommandHelp
{
    public required string Name { get; init; }

    public required string Summary { get; init; }

    public string[] Usage { get; init; } = [];

    public string? Details { get; init; }

    public OptionHelp[] Options { get; init; } = [];

    public string[] Examples { get; init; } = [];

    public string[] ExitCodeLines { get; init; } = [];

    public string? SeeAlso { get; init; }
}

/// <summary>Renders command help and the command overview.</summary>
internal static class HelpRenderer
{
    private const string ToolName = "orca-cli";

    public static void Command(CommandHelp help)
    {
        Console.WriteLine($"{ToolName} {help.Name} — {help.Summary}");
        Console.WriteLine();
        Console.WriteLine("Usage:");

        foreach (string usage in help.Usage)
        {
            Console.WriteLine($"  {usage}");
        }

        if (!string.IsNullOrWhiteSpace(help.Details))
        {
            Console.WriteLine();
            foreach (string line in Wrap(help.Details!))
            {
                Console.WriteLine($"  {line}");
            }
        }

        if (help.Options.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            WriteOptions(help.Options);
        }

        if (help.Examples.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Examples:");
            foreach (string example in help.Examples)
            {
                Console.WriteLine($"  {example}");
            }
        }

        if (help.ExitCodeLines.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Exit codes:");
            foreach (string line in help.ExitCodeLines)
            {
                Console.WriteLine($"  {line}");
            }
        }

        if (help.SeeAlso is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"See also: {help.SeeAlso}");
        }
    }

    public static void Overview(IReadOnlyList<CommandHelp> commands, string version)
    {
        Console.WriteLine($"{ToolName} {version} — ORCA command line front end for PowerBuilder libraries");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine($"  {ToolName} <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        int width = commands.Max(static c => c.Name.Length);
        foreach (CommandHelp command in commands)
        {
            Console.WriteLine($"  {Text.Pad(command.Name, width)}  {command.Summary}");
        }

        Console.WriteLine();
        Console.WriteLine("Global options:");
        WriteOptions(new[]
        {
            new OptionHelp("-h, --help", "show this help, or 'help <command>' for one command"),
            new OptionHelp("-V, --version", "print the tool version"),
            new OptionHelp("-v, --verbose", "show per-object detail"),
            new OptionHelp("-q, --quiet", "only warnings and errors"),
        });

        Console.WriteLine();
        Console.WriteLine($"Run '{ToolName} help <command>' for details, e.g. '{ToolName} help export'.");
    }

    private static void WriteOptions(IReadOnlyList<OptionHelp> options)
    {
        int width = options.Max(static o => o.Names.Length);
        width = Math.Clamp(width, 14, 30);

        foreach (OptionHelp option in options)
        {
            string[] lines = Wrap(option.Description, 100 - width - 4);
            Console.WriteLine($"  {Text.Pad(option.Names, width)}  {lines[0]}");

            foreach (string extra in lines.Skip(1))
            {
                Console.WriteLine($"  {new string(' ', width)}  {extra}");
            }
        }
    }

    private static string[] Wrap(string text, int width = 96)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string current = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length == 0)
                {
                    current = word;
                }
                else if (current.Length + 1 + word.Length <= width)
                {
                    current += " " + word;
                }
                else
                {
                    lines.Add(current);
                    current = word;
                }
            }

            lines.Add(current);
        }

        return lines.ToArray();
    }
}
