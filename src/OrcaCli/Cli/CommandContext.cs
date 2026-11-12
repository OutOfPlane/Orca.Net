using Orca;

namespace OrcaCli;

/// <summary>State shared by every command: logging, configuration, ORCA path overrides.</summary>
internal sealed class CommandContext
{
    public CommandContext(Log log, Configuration configuration)
    {
        Log = log;
        Config = configuration;
    }

    public Log Log { get; }

    public Configuration Config { get; }

    public string? DllOption { get; private set; }

    public string? RuntimeOption { get; private set; }

    public OrcaEncoding? EncodingOption { get; private set; }

    /// <summary>True when stdout must contain only a JSON document.</summary>
    public bool Json { get; private set; }

    public string? DllPath => DllOption ?? Config.DllPath;

    public string? RuntimePath => RuntimeOption ?? Config.RuntimePath;

    public OrcaEncoding Encoding => EncodingOption ?? Config.Encoding;

    /// <summary>Options every command accepts.</summary>
    public static readonly string[] CommonValueOptions = { "--dll", "--runtime", "--encoding" };

    /// <summary>Flags every command accepts.</summary>
    public static readonly string[] CommonFlags = { "--json" };

    public void ApplyCommonOptions(ArgSet args)
    {
        DllOption = args.Get("--dll");
        RuntimeOption = args.Get("--runtime");

        string? encoding = args.Get("--encoding");
        if (encoding is not null)
        {
            EncodingOption = Configuration.ParseEncoding(encoding);
        }

        Json = args.Has("--json");
        Log.MachineReadable = Json;
    }

    /// <summary>Points ORCA at the configured DLL and runtime folder.</summary>
    public void InitializeOrca()
    {
        Log.Detail($"pborc.dll : {DllPath ?? OrcaRuntime.DefaultDllPath}");
        Log.Detail($"runtime   : {RuntimePath ?? OrcaRuntime.DefaultRuntimePath}");
        OrcaRuntime.Initialize(DllPath, RuntimePath);
    }

    public string RequireInputPath(string? inline, string? configured, string optionName) =>
        Config.RequirePath(inline, configured, optionName);

    public static OptionHelp[] CommonHelp() =>
    [
        new("--dll <path>", "pborc.dll to load (default: configured, else the PowerBuilder 25.0 IDE folder)"),
        new("--runtime <dir>", "PowerBuilder runtime folder added to the DLL search path"),
        new("--encoding <enc>", "unicode | utf8 | ansi (default: unicode)"),
        new("--json", "write a JSON document to stdout, progress to stderr"),
        new("-v, --verbose", "show per-object detail"),
        new("-q, --quiet", "only warnings and errors"),
        new("-h, --help", "show help"),
    ];
}
