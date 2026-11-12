namespace OrcaCli;

/// <summary>Parsed options and positionals of one command invocation.</summary>
internal sealed class ArgSet
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positionals { get; } = new();

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out string? value) ? value : null;

    public string Require(string name) =>
        Get(name) ?? throw new CliException($"Option '{name}' is required.");

    public int? GetInt(string name)
    {
        string? raw = Get(name);
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, out int value)
            ? value
            : throw new CliException($"Option '{name}' expects an integer, got '{raw}'.");
    }

    /// <summary>Rejects anything the command does not understand, so typos fail loudly.</summary>
    public void EnsureOnly(string command, params string[] allowed)
    {
        var known = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        foreach (string name in _options.Keys)
        {
            if (!known.Contains(name))
            {
                throw new CliException(
                    $"Unknown option '{name}' for '{command}'. Run 'orca-cli help {command}' for the options it accepts.");
            }
        }
    }

    internal void Set(string name, string? value) => _options[name] = value;
}

/// <summary>
/// Minimal, strict option parser: <c>--name value</c>, <c>--name=value</c>, single-letter
/// aliases, <c>--</c> to end option parsing. Unknown options are rejected by the command.
/// </summary>
internal static class ArgumentParser
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["-p"] = "--pbl",
        ["-s"] = "--src",
        ["-o"] = "--out",
    };

    public static ArgSet Parse(IReadOnlyList<string> tokens, IReadOnlyCollection<string> valueOptions)
    {
        var set = new ArgSet();
        var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];

            if (token == "--")
            {
                for (int rest = i + 1; rest < tokens.Count; rest++)
                {
                    set.Positionals.Add(tokens[rest]);
                }

                break;
            }

            if (!token.StartsWith('-') || token == "-")
            {
                set.Positionals.Add(token);
                continue;
            }

            string name = token;
            string? value = null;

            int equals = token.IndexOf('=');
            if (equals > 0)
            {
                name = token[..equals];
                value = token[(equals + 1)..];
            }

            if (Aliases.TryGetValue(name, out string? alias))
            {
                name = alias;
            }

            if (values.Contains(name))
            {
                if (value is null)
                {
                    if (i + 1 >= tokens.Count)
                    {
                        throw new CliException($"Option '{name}' expects a value.");
                    }

                    value = tokens[++i];
                }

                set.Set(name, value);
            }
            else
            {
                if (value is not null)
                {
                    throw new CliException($"Option '{name}' does not take a value.");
                }

                set.Set(name, null);
            }
        }

        return set;
    }
}
