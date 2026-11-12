using System.Text.Json.Serialization;
using Orca;

namespace OrcaCli;

/// <summary>
/// <c>orca-cli verify</c> — checks that a library's objects are byte-identical to the source
/// files exported from it.
/// </summary>
internal sealed class VerifyCommand : Command
{
    public override string Name => "verify";

    public override string Summary => "Compare a library with its exported sources (CI check).";

    public override IReadOnlyList<string> ValueOptions { get; } = new[]
    {
        "--pbl", "--src", "--encoding", "--dll", "--runtime",
    };

    public override CommandHelp Help => new()
    {
        Name = Name,
        Summary = Summary,
        Usage =
        [
            "orca-cli verify --pbl <file.pbl> --src <dir> [--json]",
            "orca-cli verify --pbl <file.pbl> [--json]    (uses the sibling <pbl>.src folder)",
        ],
        Details =
            "Each exported source file is re-exported from the library and compared byte for byte. " +
            "Objects present in the library but missing from the source tree are reported as extra. " +
            "The command needs neither a library list nor a current application.",
        Options = WithCommon(
            new OptionHelp("--pbl <file>", "library to verify"),
            new OptionHelp("--src <dir>", "source folder, or its parent when it holds <pbl file name>.src")),
        Examples =
        [
            "orca-cli verify --pbl PB_DEMO_PROJ\\APP_SRC\\tlfi_docuscan.pbl --src PB_DEMO_PROJ\\TARGET_OUTPUT",
            "orca-cli verify --pbl app.pbl --json"
        ],
        ExitCodeLines =
        [
            "0  sources and library agree",
            "1  usage or ORCA error",
            "2  differences found",
        ],
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

        string sourceDirectory = ResolveSourceDirectory(context, args, pbl);
        IReadOnlyList<SourceFile> files = SourceFile.LoadDirectory(sourceDirectory);
        if (files.Count == 0)
        {
            throw new CliException($"No PowerBuilder source files found in '{sourceDirectory}'.");
        }

        context.Log.Info($"Library : {pbl}");
        context.Log.Info($"Sources : {sourceDirectory}");

        context.InitializeOrca();

        using OrcaSession session = OrcaSession.Open();
        session.ConfigureSession(new OrcaSessionOptions
        {
            ExportEncoding = context.Encoding,
            ImportEncoding = context.Encoding,
            Clobber = OrcaClobber.Clobber,
            ExportHeaders = true,
            Debug = true,
        });

        OrcaLibrary library = session.GetLibrary(pbl);

        int identical = 0;
        int different = 0;
        int missing = 0;
        var problems = new List<Problem>();

        foreach (SourceFile file in files.OrderBy(static f => f.EntryName, StringComparer.OrdinalIgnoreCase))
        {
            byte[] exported;
            try
            {
                exported = session.ExportEntryBytes(pbl, file.EntryName, file.Type);
            }
            catch (OrcaException ex) when (ex.Code == OrcaResult.ObjectNotFound)
            {
                missing++;
                problems.Add(new Problem(file.EntryName, file.Type.ToString(), "missing", "not in library"));
                context.Log.Info($"{Text.Pad(file.EntryName, 24)} {Text.Pad(file.Type.ToString(), 12)} missing");
                continue;
            }

            byte[] compared = context.Encoding == OrcaEncoding.Unicode && !Text.HasUnicodeBom(exported)
                ? Text.AddUnicodeBom(exported)
                : exported;

            if (compared.AsSpan().SequenceEqual(file.Content))
            {
                identical++;
                context.Log.Detail($"{Text.Pad(file.EntryName, 24)} {Text.Pad(file.Type.ToString(), 12)} identical");
            }
            else
            {
                different++;
                string detail = Text.DescribeDifference(file.Content, compared);
                problems.Add(new Problem(file.EntryName, file.Type.ToString(), "differs", detail));
                context.Log.Info($"{Text.Pad(file.EntryName, 24)} {Text.Pad(file.Type.ToString(), 12)} differs " +
                                 $"({detail})");
            }
        }

        // Objects in the library that have no source file at all.
        var known = files
            .Select(static f => (f.EntryName.ToLowerInvariant(), f.Type))
            .ToHashSet();
        var extra = library.Entries
            .Where(e => !known.Contains((e.Name.ToLowerInvariant(), e.Type)))
            .OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (OrcaEntry entry in extra)
        {
            problems.Add(new Problem(entry.Name, entry.Type.ToString(), "extra", "no source file"));
            context.Log.Info($"{Text.Pad(entry.Name, 24)} {Text.Pad(entry.Type.ToString(), 12)} extra " +
                             "(no source file)");
        }

        context.Log.Info($"Result  : {files.Count} source file(s) — {identical} identical, {different} different, " +
                         $"{missing} missing, {extra.Count} extra");

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                library = pbl,
                source = sourceDirectory,
                sources = files.Count,
                identical,
                different,
                missing,
                extra = extra.Count,
                differences = problems,
            });
        }

        return different > 0 || missing > 0 || extra.Count > 0
            ? ExitCodes.Differences
            : ExitCodes.Ok;
    }

    private static string ResolveSourceDirectory(CommandContext context, ArgSet args, string pbl)
    {
        string folderName = SourceLibrary.FolderNameFor(pbl);
        string sibling = Path.Combine(Path.GetDirectoryName(pbl)!, folderName);

        if (args.Get("--src") is { } configured)
        {
            string resolved = context.Config.Resolve(configured);
            string nested = Path.Combine(resolved, folderName);
            return Directory.Exists(nested) ? nested : resolved;
        }

        if (Directory.Exists(sibling))
        {
            return sibling;
        }

        if (context.Config.ExportDirectory is { } exportDirectory)
        {
            string resolved = context.Config.Resolve(exportDirectory);
            string nested = Path.Combine(resolved, folderName);
            return Directory.Exists(nested) ? nested : resolved;
        }

        throw new CliException(
            $"No source folder found for '{Path.GetFileName(pbl)}'. Pass --src, or export the library " +
            $"first (expected '{sibling}').");
    }

    private sealed record Problem(
        [property: JsonPropertyName("entry")] string Entry,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("detail")] string Detail);
}
