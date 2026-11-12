using System.Text.Json.Serialization;
using Orca;

namespace OrcaCli;

/// <summary>
/// <c>orca-cli export</c> — reads a compiled PowerBuilder library and writes every object's
/// source into the Library painter's mirrored layout (<c>&lt;pbl file name&gt;.src</c>).
/// </summary>
internal sealed class ExportCommand : Command
{
    public override string Name => "export";

    public override string Summary => "Export a PowerBuilder library (PBL) to source files.";

    public override IReadOnlyList<string> ValueOptions { get; } = new[]
    {
        "--pbl", "--out", "--layout", "--encoding", "--dll", "--runtime",
    };

    public override IReadOnlyList<string> FlagOptions { get; } = new[]
    {
        "--flat", "--no-headers", "--binary", "--no-bom", "--dry-run",
    };

    public override CommandHelp Help => new()
    {
        Name = Name,
        Summary = Summary,
        Usage =
        [
            "orca-cli export --pbl <file.pbl> [--out <dir>] [options]",
            "orca-cli export [options]          (uses the configured default library)",
        ],
        Details =
            "Every object is exported with PBORCA_LibraryEntryExportEx and written as <entry>.sr? " +
            "(application .sra, window .srw, datawindow .srd, function .srf, menu .srm, structure .srs, " +
            "user object .sru, query .srq, pipeline .srp, project .srj). By default the files go into a " +
            "folder named after the library file plus '.src', which is how the Library painter records " +
            "which objects belong to which PBL; use --layout flat to write them directly into --out.",
        Options = WithCommon(
            new OptionHelp("--pbl <file>", "library to export (default: PblPath from appsettings.json)"),
            new OptionHelp("--out <dir>", "parent output folder (default: OutputDirectory, or the library's folder)"),
            new OptionHelp("--layout <kind>", "pbl = one folder per library (default); flat = files directly in --out"),
            new OptionHelp("--flat", "shorthand for --layout flat"),
            new OptionHelp("--no-headers", "omit the $PBExportHeader$ / $PBExportComments$ lines"),
            new OptionHelp("--binary", "include embedded binary components (OLE and similar)"),
            new OptionHelp("--no-bom", "do not prepend a UTF-16 BOM to Unicode files"),
            new OptionHelp("--dry-run", "export and report, but write nothing")),
        Examples =
        [
            "orca-cli export --pbl PB_DEMO_PROJ\\APP_SRC\\tlfi_docuscan.pbl",
            "orca-cli export --pbl app.pbl --out src --layout flat --verbose",
            "orca-cli export --pbl app.pbl --json"
        ],
        ExitCodeLines = ExitCodeHelp(),
    };

    public override int Execute(CommandContext context, ArgSet args)
    {
        context.ApplyCommonOptions(args);
        ValidateOptions(args);

        string pbl = context.RequireInputPath(args.Get("--pbl"), context.Config.ExportPblPath, "--pbl");
        if (!File.Exists(pbl))
        {
            throw new CliException($"Library not found: {pbl}");
        }

        bool flat = args.Has("--flat") || ParseLayout(args.Get("--layout"));
        string outputBase =
            args.Get("--out") is { } inline ? context.Config.Resolve(inline)
            : context.Config.ExportDirectory is { } configured ? context.Config.Resolve(configured)
            : Path.GetDirectoryName(pbl)!;
        string target = flat ? outputBase : Path.Combine(outputBase, SourceLibrary.FolderNameFor(pbl));

        bool headers = !args.Has("--no-headers");
        bool includeBinary = args.Has("--binary");
        bool writeBom = !args.Has("--no-bom") && context.Encoding == OrcaEncoding.Unicode;
        bool dryRun = args.Has("--dry-run");

        context.Log.Info($"Library : {pbl}");
        context.Log.Info($"Output  : {target}{(dryRun ? "  (dry run)" : string.Empty)}");
        context.Log.Info($"Encoding: {context.Encoding}{(headers ? ", headers" : string.Empty)}" +
                         $"{(includeBinary ? ", binary" : string.Empty)}");

        context.InitializeOrca();

        using OrcaSession session = OrcaSession.Open();
        session.ConfigureSession(new OrcaSessionOptions
        {
            ExportEncoding = context.Encoding,
            ImportEncoding = context.Encoding,
            Clobber = OrcaClobber.Clobber,
            ExportHeaders = headers,
            ExportIncludeBinary = includeBinary,
            ExportToFile = false,
            Debug = true,
        });

        OrcaLibrary library = session.GetLibrary(pbl);
        context.Log.Info($"Entries : {library.Entries.Count}");

        Directory.CreateDirectory(target);

        var written = new List<ExportedEntry>();
        long totalBytes = 0;

        foreach (OrcaEntry entry in library.Entries.OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            byte[] data = session.ExportEntryBytes(pbl, entry.Name, entry.Type);
            byte[] fileBytes = writeBom ? Text.AddUnicodeBom(data) : data;

            string fileName = Text.SafeFileName(entry.Name) + entry.Type.GetSourceExtension();
            string path = Path.Combine(target, fileName);

            if (!dryRun)
            {
                File.WriteAllBytes(path, fileBytes);
            }

            totalBytes += fileBytes.Length;
            context.Log.Detail($"{Text.Pad(entry.Name, 24)} {Text.Pad(entry.Type.ToString(), 12)} " +
                               $"{Text.Pad(fileBytes.Length.ToString(), 8)} B  {fileName}");
            written.Add(new ExportedEntry(entry.Name, entry.Type.ToString(), fileBytes.Length, fileName));
        }

        context.Log.Info($"{(dryRun ? "Would export" : "Exported")} {written.Count} object(s), " +
                         $"{totalBytes} bytes{(dryRun ? " (nothing written)" : string.Empty)}");

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                library = pbl,
                output = target,
                encoding = context.Encoding.ToString(),
                layout = flat ? "flat" : "pbl",
                dryRun,
                count = written.Count,
                bytes = totalBytes,
                entries = written,
            });
        }

        return ExitCodes.Ok;
    }

    private static bool ParseLayout(string? value) => value?.ToLowerInvariant() switch
    {
        null or "pbl" or "library" => false,
        "flat" => true,
        _ => throw new CliException($"Unknown layout '{value}'. Use 'pbl' or 'flat'."),
    };

    private sealed record ExportedEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("bytes")] long Bytes,
        [property: JsonPropertyName("file")] string File);
}
