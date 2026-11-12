namespace OrcaCli;

/// <summary>Base class for one CLI command.</summary>
internal abstract class Command
{
    public abstract string Name { get; }

    public abstract string Summary { get; }

    /// <summary>Options that consume a value, used to configure the parser.</summary>
    public abstract IReadOnlyList<string> ValueOptions { get; }

    /// <summary>Options that are plain switches.</summary>
    public virtual IReadOnlyList<string> FlagOptions => Array.Empty<string>();

    public abstract CommandHelp Help { get; }

    public abstract int Execute(CommandContext context, ArgSet args);

    /// <summary>Appends the options every command shares.</summary>
    protected static OptionHelp[] WithCommon(params OptionHelp[] specific) =>
        specific.Concat(CommandContext.CommonHelp()).ToArray();

    protected static string[] ExitCodeHelp(params string[] specific) =>
        specific.Concat(new[]
        {
            "0  success",
            "1  usage or ORCA error",
        }).ToArray();

    /// <summary>Rejects unknown options; <c>--json</c>/<c>--dll</c>… are always allowed.</summary>
    protected void ValidateOptions(ArgSet args)
    {
        string[] allowed = ValueOptions
            .Concat(FlagOptions)
            .Concat(CommandContext.CommonValueOptions)
            .Concat(CommandContext.CommonFlags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        args.EnsureOnly(Name, allowed);
    }
}
