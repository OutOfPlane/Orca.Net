using System.Text.Json;
using Orca;

namespace OrcaCli;

/// <summary>
/// Resolved defaults for the tool: ORCA paths, demo/project paths and encoding.
/// </summary>
/// <remarks>
/// Precedence: command line &gt; environment (<c>ORCA_DLL_PATH</c>, <c>ORCA_RUNTIME_PATH</c>)
/// &gt; <c>appsettings.json</c> next to the executable &gt; <c>appsettings.json</c> in the
/// current directory.
/// </remarks>
internal sealed class Configuration
{
    private Configuration()
    {
    }

    /// <summary>Directory against which relative paths are resolved.</summary>
    public required string Root { get; init; }

    public string? DllPath { get; init; }

    public string? RuntimePath { get; init; }

    /// <summary>Default <c>--pbl</c> for export/list/verify.</summary>
    public string? ExportPblPath { get; init; }

    /// <summary>Default <c>--out</c> for export.</summary>
    public string? ExportDirectory { get; init; }

    /// <summary>Default <c>--src</c> for build/verify/list.</summary>
    public string? BuildSourceDirectory { get; init; }

    /// <summary>Default library location for build (its directory is the output directory).</summary>
    public string? BuildPblPath { get; init; }

    public OrcaEncoding Encoding { get; init; } = OrcaEncoding.Unicode;

    /// <summary>Configuration files that were actually read, in application order.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    public static Configuration Load()
    {
        string? dll = null;
        string? runtime = null;
        string? exportPbl = null;
        string? exportDirectory = null;
        string? buildSource = null;
        string? buildPbl = null;
        OrcaEncoding encoding = OrcaEncoding.Unicode;
        var files = new List<string>();

        foreach (string file in CandidateFiles())
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("Orca", out JsonElement orca))
            {
                dll = ReadString(orca, "DllPath") ?? dll;
                runtime = ReadString(orca, "RuntimePath") ?? runtime;
            }

            if (root.TryGetProperty("Demo", out JsonElement demo) ||
                root.TryGetProperty("Project", out demo))
            {
                exportPbl = ReadString(demo, "PblPath") ?? exportPbl;
                exportDirectory = ReadString(demo, "OutputDirectory") ?? exportDirectory;
                buildSource = ReadString(demo, "BuildSourceDirectory") ?? buildSource;
                buildPbl = ReadString(demo, "BuildPblPath") ?? buildPbl;

                string? configured = ReadString(demo, "Encoding");
                if (configured is not null)
                {
                    encoding = ParseEncoding(configured);
                }
            }

            files.Add(file);
        }

        return new Configuration
        {
            Root = FindRoot(),
            DllPath = Environment.GetEnvironmentVariable("ORCA_DLL_PATH") ?? dll,
            RuntimePath = Environment.GetEnvironmentVariable("ORCA_RUNTIME_PATH") ?? runtime,
            ExportPblPath = exportPbl,
            ExportDirectory = exportDirectory,
            BuildSourceDirectory = buildSource,
            BuildPblPath = buildPbl,
            Encoding = encoding,
            Files = files,
        };
    }

    /// <summary>Resolves a path against <see cref="Root"/>.</summary>
    public string Resolve(string path) =>
        Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(Path.Combine(Root, path));

    /// <summary>Precedence: command line, then configured default, then an error.</summary>
    public string RequirePath(string? inline, string? configured, string optionName)
    {
        string? value = inline ?? configured;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliException(
                $"Missing {optionName} and no default is configured. Pass {optionName} or add it to appsettings.json.");
        }

        return Resolve(value);
    }

    public static OrcaEncoding ParseEncoding(string value) => value.ToLowerInvariant() switch
    {
        "unicode" or "utf16" or "utf-16" => OrcaEncoding.Unicode,
        "utf8" or "utf-8" => OrcaEncoding.Utf8,
        "ansi" or "ansidbcs" or "ansi-dbcs" => OrcaEncoding.AnsiDbcs,
        "hex" or "hexascii" => OrcaEncoding.HexAscii,
        _ => throw new CliException($"Unknown encoding '{value}'. Use unicode, utf8 or ansi."),
    };

    public static OrcaRebuildType ParseRebuild(string value) => value.ToLowerInvariant() switch
    {
        "full" => OrcaRebuildType.Full,
        "incremental" => OrcaRebuildType.Incremental,
        "migrate" => OrcaRebuildType.Migrate,
        "3pass" or "three-pass" => OrcaRebuildType.ThreePass,
        _ => throw new CliException(
            $"Unknown rebuild type '{value}'. Use full, incremental, migrate or 3pass."),
    };

    /// <summary>Project root: the nearest ancestor holding <c>PB_DEMO_PROJ</c>, else the working directory.</summary>
    private static string FindRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "PB_DEMO_PROJ")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static IEnumerable<string> CandidateFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string beside = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(beside) && seen.Add(beside))
        {
            yield return beside;
        }

        string current = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(current) && seen.Add(current))
        {
            yield return current;
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
