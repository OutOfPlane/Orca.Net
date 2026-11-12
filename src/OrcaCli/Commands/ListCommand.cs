using System.Text.Json.Serialization;
using Orca;

namespace OrcaCli;

/// <summary><c>orca-cli list</c> — lists a library's objects, or the contents of a source tree.</summary>
internal sealed class ListCommand : Command
{
    public override string Name => "list";

    public override string Summary => "List the objects in a library, or the files in a source tree.";

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
            "orca-cli list --pbl <file.pbl> [--json]",
            "orca-cli list --src <dir> [--json]",
            "orca-cli list [--json]            (uses the configured default library)",
        ],
        Details =
            "With --pbl the library is opened through ORCA and its directory is printed; no library " +
            "list or current application is needed for that. With --src the exported source tree is " +
            "read instead, so no PowerBuilder installation is involved.",
        Options = WithCommon(
            new OptionHelp("--pbl <file>", "library to inspect"),
            new OptionHelp("--src <dir>", "source tree to inspect (*.pbl.src folders or flat)")),
        Examples =
        [
            "orca-cli list --pbl PB_DEMO_PROJ\\APP_SRC\\tlfi_docuscan.pbl",
            "orca-cli list --src PB_DEMO_PROJ\\TARGET_OUTPUT --json"
        ],
        ExitCodeLines = ExitCodeHelp(),
    };

    public override int Execute(CommandContext context, ArgSet args)
    {
        context.ApplyCommonOptions(args);
        ValidateOptions(args);

        if (args.Has("--pbl") && args.Has("--src"))
        {
            throw new CliException("Use either --pbl or --src, not both.");
        }

        return args.Has("--src")
            ? ListSources(context, args)
            : ListLibrary(context, args);
    }

    private static int ListLibrary(CommandContext context, ArgSet args)
    {
        string pbl = context.RequireInputPath(args.Get("--pbl"), context.Config.ExportPblPath, "--pbl");
        if (!File.Exists(pbl))
        {
            throw new CliException($"Library not found: {pbl}");
        }

        context.InitializeOrca();

        using OrcaSession session = OrcaSession.Open();
        session.ConfigureSession(new OrcaSessionOptions
        {
            ExportEncoding = context.Encoding,
            ImportEncoding = context.Encoding,
            Debug = true,
        });

        OrcaLibrary library = session.GetLibrary(pbl);
        var entries = library.Entries
            .OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        context.Log.Info($"Library : {pbl}");
        context.Log.Info($"Comment : \"{library.Comments}\"");
        context.Log.Info($"Objects : {entries.Count}");
        context.Log.Info(string.Empty);
        context.Log.Info($"{Text.Pad("NAME", 28)} {Text.Pad("TYPE", 14)} {Text.Pad("SIZE", 9)} COMMENTS");

        foreach (OrcaEntry entry in entries)
        {
            context.Log.Info(
                $"{Text.Pad(entry.Name, 28)} {Text.Pad(entry.Type.ToString(), 14)} " +
                $"{Text.Pad(entry.SizeBytes.ToString(), 9)} {entry.Comments}");
        }

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                library = pbl,
                comment = library.Comments,
                count = entries.Count,
                entries = entries.Select(static e => new EntryRow(
                    e.Name, e.Type.ToString(), e.SizeBytes, e.Comments,
                    e.CreatedUtc == DateTimeOffset.UnixEpoch ? null : e.CreatedUtc.ToString("O"))),
            });
        }

        return ExitCodes.Ok;
    }

    private static int ListSources(CommandContext context, ArgSet args)
    {
        string sourceDirectory = context.RequireInputPath(
            args.Get("--src"), context.Config.BuildSourceDirectory, "--src");
        IReadOnlyList<SourceLibrary> libraries = SourceLibrary.Discover(sourceDirectory);

        if (libraries.Count == 0)
        {
            throw new CliException($"No PowerBuilder sources found in '{sourceDirectory}'.");
        }

        context.Log.Info($"Sources : {sourceDirectory}");

        foreach (SourceLibrary library in libraries)
        {
            string title = library.LibraryFile ?? "(flat folder, single library)";
            context.Log.Info(string.Empty);
            context.Log.Info($"{title} — {library.EntryCount} file(s)");

            foreach (SourceFile file in library.Files.OrderBy(static f => f.EntryName, StringComparer.OrdinalIgnoreCase))
            {
                context.Log.Info(
                    $"  {Text.Pad(file.EntryName, 26)} {Text.Pad(file.Type.ToString(), 14)} " +
                    $"{Text.Pad(file.Content.Length.ToString(), 9)} {Path.GetFileName(file.Path)}");
            }
        }

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                sources = sourceDirectory,
                libraries = libraries.Select(static l => new
                {
                    library = l.LibraryFile,
                    directory = l.Directory,
                    files = l.Files
                        .OrderBy(static f => f.EntryName, StringComparer.OrdinalIgnoreCase)
                        .Select(static f => new SourceRow(
                            f.EntryName, f.Type.ToString(), f.Content.Length, Path.GetFileName(f.Path),
                            f.Comments)),
                }),
            });
        }

        return ExitCodes.Ok;
    }

    private sealed record EntryRow(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("sizeBytes")] int SizeBytes,
        [property: JsonPropertyName("comments")] string Comments,
        [property: JsonPropertyName("createdUtc")] string? CreatedUtc);

    private sealed record SourceRow(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("bytes")] int Bytes,
        [property: JsonPropertyName("file")] string File,
        [property: JsonPropertyName("comments")] string Comments);
}
