using Orca;

namespace OrcaCli;

/// <summary>
/// <c>orca-cli build</c> — rebuilds libraries and binaries from exported sources.
/// </summary>
/// <remarks>
/// The source tree may be the mirrored layout (one <c>&lt;pbl file name&gt;.src</c> folder per
/// library, which is what carries the object-to-library mapping) or a flat folder holding a
/// single library. The build runs the documented two-session bootstrap and re-imports the
/// Application object once its windows exist.
/// </remarks>
internal sealed class BuildCommand : Command
{
    public override string Name => "build";

    public override string Summary => "Build PBLs, PBDs and an EXE from exported sources.";

    public override IReadOnlyList<string> ValueOptions { get; } = new[]
    {
        "--src", "--out", "--pbl", "--app", "--rebuild", "--encoding",
        "--dll", "--runtime", "--icon", "--pbr", "--comment", "--exe",
    };

    public override IReadOnlyList<string> FlagOptions { get; } = new[]
    {
        "--pbd", "--no-rebuild", "--no-verify",
    };

    public override CommandHelp Help => new()
    {
        Name = Name,
        Summary = Summary,
        Usage =
        [
            "orca-cli build --src <dir> [--out <dir>] [--pbd] [--exe <file>] [options]",
            "orca-cli build [options]        (uses the configured default source folder)",
        ],
        Details =
            "Every *.pbl.src folder under --src becomes one PBL and its objects are imported into " +
            "that PBL; a flat folder builds a single library named by --pbl. The application's " +
            "library is placed first in the library list. The Application object is imported in a " +
            "bootstrap session, re-imported once the windows exist (its open/close events need " +
            "them), and the result is rebuilt. With --exe the application library goes into the " +
            "EXE and the other libraries become PBDs; --pbd alone builds a PBD for every library.",
        Options = WithCommon(
            new OptionHelp("--src <dir>", "folder containing *.pbl.src folders, or a flat .sr? folder"),
            new OptionHelp("--out <dir>", "output folder for the built libraries"),
            new OptionHelp("--pbl <file>", "flat layout: the library to create; also sets the output folder"),
            new OptionHelp("--app <name>", "Application object name (default: from the .sra file)"),
            new OptionHelp("--rebuild <type>", "full | incremental | migrate | 3pass (default: full)"),
            new OptionHelp("--no-rebuild", "skip PBORCA_ApplicationRebuild"),
            new OptionHelp("--pbd", "also build PBDs"),
            new OptionHelp("--exe <file>", "also build an executable"),
            new OptionHelp("--icon <file>", "icon for the executable (a placeholder is generated when omitted)"),
            new OptionHelp("--pbr <file>", "PowerBuilder resource file for the executable"),
            new OptionHelp("--comment <text>", "library comment for the created PBLs"),
            new OptionHelp("--no-verify", "skip the round-trip export comparison")),
        Examples =
        [
            "orca-cli build --src PB_DEMO_PROJ\\TARGET_OUTPUT --out PB_DEMO_PROJ\\BUILD_OUTPUT",
            "orca-cli build --src src --pbd --exe out\\app.exe --verbose",
            "orca-cli build --src src --no-verify --json"
        ],
        ExitCodeLines =
        [
            "0  built",
            "1  usage or ORCA error",
            "2  round-trip differences",
            "3  compile or link errors",
        ],
    };

    public override int Execute(CommandContext context, ArgSet args)
    {
        context.ApplyCommonOptions(args);
        ValidateOptions(args);

        string sourceDirectory = context.RequireInputPath(
            args.Get("--src"), context.Config.BuildSourceDirectory, "--src");
        IReadOnlyList<SourceLibrary> libraries = SourceLibrary.Discover(sourceDirectory);

        if (libraries.Count == 0)
        {
            throw new CliException(
                $"No PowerBuilder sources found in '{sourceDirectory}'. Expected *.pbl.src folders " +
                "or .sra/.srd/.srw files.");
        }

        SourceLibrary applicationLibrary = libraries
            .FirstOrDefault(static l => l.Files.Any(static f => f.Type == OrcaEntryType.Application))
            ?? throw new CliException(
                $"No Application object (.sra) under '{sourceDirectory}'; it is required to set the " +
                "current application.");

        SourceFile application = applicationLibrary.Files.First(static f => f.Type == OrcaEntryType.Application);
        string applicationName = args.Get("--app") ?? application.EntryName;

        List<LibraryPlan> plans = Plan(context, args, libraries, applicationLibrary);
        LibraryPlan applicationPlan = plans.First(p => ReferenceEquals(p.Library, applicationLibrary));
        string outputDirectory = Path.GetDirectoryName(applicationPlan.Path)!;

        bool rebuildEnabled = !args.Has("--no-rebuild");
        OrcaRebuildType rebuildType = rebuildEnabled
            ? Configuration.ParseRebuild(args.Get("--rebuild") ?? "full")
            : OrcaRebuildType.Full;

        context.Log.Info($"Sources : {sourceDirectory} " +
                         $"({(libraries.Any(static l => l.HasLibraryFileName) ? $"{libraries.Count} libraries" : "flat")})");
        context.Log.Info($"Output  : {outputDirectory}");
        context.Log.Info($"App     : {applicationName} in {Path.GetFileName(applicationPlan.Path)}");
        context.Log.Info($"Objects : {libraries.Sum(static l => l.EntryCount)}");

        context.InitializeOrca();

        var options = new OrcaSessionOptions
        {
            ExportEncoding = context.Encoding,
            ImportEncoding = context.Encoding,
            Clobber = OrcaClobber.ClobberAlways,
            ExportHeaders = true,
            Debug = true,
        };

        Directory.CreateDirectory(outputDirectory);

        using (OrcaSession cleanup = OrcaSession.Open())
        {
            cleanup.ConfigureSession(options);
            foreach (LibraryPlan plan in plans.Where(static p => File.Exists(p.Path)))
            {
                cleanup.DeleteLibrary(plan.Path);
                context.Log.Detail($"removed existing library {plan.Path}");
            }
        }

        var messages = new List<OrcaCompileError>();
        var builtPbds = new List<string>();
        string? executablePath = null;
        string[] libraryList = plans.Select(static p => p.Path).ToArray();
        string comment = args.Get("--comment") ?? "Built by orca-cli from exported sources";

        // ---- session 1: create libraries, bootstrap the Application object ---------------
        using (OrcaSession bootstrap = OrcaSession.Open())
        {
            bootstrap.ConfigureSession(options);

            foreach (LibraryPlan plan in plans)
            {
                bootstrap.CreateLibrary(plan.Path, comment);
                context.Log.Detail($"created {plan.Path}");
            }

            bootstrap.SetLibraryList(libraryList);
            bootstrap.UseTemporaryApplication();

            // Compile errors are expected here: open/close events reference windows that do
            // not exist yet. The object is stored and re-imported in session 2.
            var deferred = new List<OrcaCompileError>();
            Import(bootstrap, applicationPlan.Path, application, deferred);

            context.Log.Info($"Import  : {application.EntryName} ({application.Type}) — bootstrap" +
                             (deferred.Any(static m => m.IsError) ? ", compile deferred" : string.Empty));
        }

        // ---- session 2: real application, remaining objects ------------------------------
        using (OrcaSession session = OrcaSession.Open())
        {
            session.ConfigureSession(options);
            session.SetLibraryList(libraryList);
            session.SetCurrentApplication(applicationPlan.Path, applicationName);

            foreach (LibraryPlan plan in plans)
            {
                List<SourceFile> files = Ordered(plan.Library.Files)
                    .Where(f => !ReferenceEquals(f, application))
                    .ToList();

                if (files.Count == 0)
                {
                    continue;
                }

                context.Log.Detail($"library {Path.GetFileName(plan.Path)} — {files.Count} object(s)");

                foreach (SourceFile file in files)
                {
                    Import(session, plan.Path, file, messages);
                    context.Log.Detail($"{Text.Pad(file.EntryName, 24)} {Text.Pad(file.Type.ToString(), 12)} " +
                                       $"{Text.Pad(file.Content.Length.ToString(), 8)} bytes imported");
                }
            }

            // The windows exist now, so the Application object compiles cleanly.
            Import(session, applicationPlan.Path, application, messages);
            context.Log.Info($"Import  : {application.EntryName} ({application.Type}) — application");

            if (rebuildEnabled)
            {
                session.Rebuild(rebuildType, message => messages.Add(message));
                context.Log.Info($"Rebuild : {rebuildType}");
            }

            if (messages.Any(static m => m.IsError))
            {
                Text.CompilerMessages(context.Log, messages);
                context.Log.Error("build failed: the libraries contain compile or link errors.");
                return ExitCodes.BuildFailed;
            }

            Text.CompilerMessages(context.Log, messages);

            foreach (LibraryPlan plan in plans)
            {
                context.Log.Info($"Library : {plan.Path} ({new FileInfo(plan.Path).Length} bytes)");
            }

            bool executableRequested = args.Has("--exe");
            bool pbdRequested = args.Has("--pbd");

            // --pbd builds a PBD for every library. An EXE additionally needs PBDs for the
            // non-application libraries it links instead of embedding.
            IEnumerable<LibraryPlan> pbdTargets = pbdRequested
                ? plans
                : executableRequested
                    ? plans.Where(p => !ReferenceEquals(p.Library, applicationLibrary))
                    : Enumerable.Empty<LibraryPlan>();

            foreach (LibraryPlan plan in pbdTargets.ToList())
            {
                session.CreateDynamicLibrary(plan.Path);
                string pbd = Path.ChangeExtension(plan.Path, ".pbd");
                if (File.Exists(pbd))
                {
                    builtPbds.Add(pbd);
                    context.Log.Info($"PBD     : {pbd} ({new FileInfo(pbd).Length} bytes)");
                }
                else
                {
                    context.Log.Info($"PBD     : expected '{pbd}' but ORCA did not create it.");
                }
            }

            if (executableRequested)
            {
                executablePath = BuildExecutable(context, args, session, plans, applicationPlan, messages);
            }
        }

        VerifySummary? summary = args.Has("--no-verify") ? null : Verify(context, plans, messages);

        if (context.Json)
        {
            JsonOutput.Write(new
            {
                sources = sourceDirectory,
                output = outputDirectory,
                application = applicationName,
                libraries = plans.Select(p => new { path = p.Path, bytes = new FileInfo(p.Path).Length }),
                pbds = builtPbds,
                executable = executablePath,
                verify = summary,
            });
        }

        return summary is { } result && (result.Different > 0 || result.Missing > 0)
            ? ExitCodes.Differences
            : ExitCodes.Ok;
    }

    private static string? BuildExecutable(
        CommandContext context,
        ArgSet args,
        OrcaSession session,
        List<LibraryPlan> plans,
        LibraryPlan applicationPlan,
        List<OrcaCompileError> messages)
    {
        string executable = context.Config.Resolve(args.Require("--exe"));
        string? resource = args.Get("--pbr") is { } pbr ? context.Config.Resolve(pbr) : null;

        string? icon;
        if (args.Get("--icon") is { } configuredIcon)
        {
            icon = context.Config.Resolve(configuredIcon);
        }
        else
        {
            // ExecutableCreate rejects a NULL icon, so generate a placeholder.
            icon = Path.ChangeExtension(executable, ".ico");
            IconFile.WritePlaceholder(icon);
            context.Log.Detail($"generated placeholder icon {icon}");
        }

        bool[] pbdFlags = plans
            .Select(p => !ReferenceEquals(p.Library, applicationPlan.Library))
            .ToArray();

        session.CreateExecutable(
            executable, icon, resource, pbdFlags, OrcaBuildFlags.PCode,
            message => messages.Add(message));

        if (File.Exists(executable))
        {
            context.Log.Info($"EXE     : {executable} ({new FileInfo(executable).Length} bytes)");
            return executable;
        }

        context.Log.Info($"EXE     : expected '{executable}' but ORCA did not create it.");
        return null;
    }

    /// <summary>Re-exports every rebuilt library and compares it with the sources.</summary>
    private static VerifySummary Verify(
        CommandContext context, List<LibraryPlan> plans, List<OrcaCompileError> messages)
    {
        using OrcaSession session = OrcaSession.Open();
        session.ConfigureSession(new OrcaSessionOptions
        {
            ExportEncoding = context.Encoding,
            ImportEncoding = context.Encoding,
            Clobber = OrcaClobber.Clobber,
            ExportHeaders = true,
            Debug = true,
        });

        int identical = 0;
        int different = 0;
        int missing = 0;
        int total = 0;
        var problems = new List<object>();

        foreach (LibraryPlan plan in plans)
        {
            foreach (SourceFile file in Ordered(plan.Library.Files))
            {
                total++;
                byte[] exported;
                try
                {
                    exported = session.ExportEntryBytes(plan.Path, file.EntryName, file.Type);
                }
                catch (OrcaException ex) when (ex.Code == OrcaResult.ObjectNotFound)
                {
                    missing++;
                    problems.Add(new { entry = file.EntryName, library = plan.Path, status = "missing" });
                    context.Log.Detail($"{Text.Pad(file.EntryName, 24)} missing from {Path.GetFileName(plan.Path)}");
                    continue;
                }

                byte[] compared = context.Encoding == OrcaEncoding.Unicode && !Text.HasUnicodeBom(exported)
                    ? Text.AddUnicodeBom(exported)
                    : exported;

                if (compared.AsSpan().SequenceEqual(file.Content))
                {
                    identical++;
                    context.Log.Detail($"{Text.Pad(file.EntryName, 24)} identical");
                }
                else
                {
                    different++;
                    string detail = Text.DescribeDifference(file.Content, compared);
                    problems.Add(new { entry = file.EntryName, library = plan.Path, status = "differs", detail });
                    context.Log.Detail($"{Text.Pad(file.EntryName, 24)} differs ({detail})");
                }
            }
        }

        Text.CompilerMessages(context.Log, messages);
        context.Log.Info($"Verify  : {total} object(s) — {identical} identical, {different} different, {missing} missing");

        return new VerifySummary(total, identical, different, missing, problems);
    }

    private static void Import(
        OrcaSession session, string library, SourceFile file, List<OrcaCompileError> messages)
    {
        (byte[] source, byte[]? binary) = file.SplitBinary();

        try
        {
            session.ImportEntry(
                library, file.EntryName, file.Type, file.Comments, source, message => messages.Add(message));

            if (binary is not null)
            {
                // Objects with embedded binary data need a second import with PBORCA_BINARY.
                session.ImportEntry(
                    library, file.EntryName, OrcaEntryType.Binary, string.Empty, binary,
                    message => messages.Add(message));
            }
        }
        catch (OrcaException ex) when (ex.Code == OrcaResult.CompileError)
        {
            messages.Add(new OrcaCompileError(4, string.Empty, $"{file.EntryName}: {ex.Detail}", 0, 0));
        }
    }

    /// <summary>Maps each discovered library to the PBL file it will produce.</summary>
    private static List<LibraryPlan> Plan(
        CommandContext context,
        ArgSet args,
        IReadOnlyList<SourceLibrary> libraries,
        SourceLibrary applicationLibrary)
    {
        string? explicitPbl = args.Get("--pbl");
        string? configuredPbl = context.Config.BuildPblPath;

        string? anchor = explicitPbl is null && configuredPbl is null
            ? null
            : context.Config.Resolve(explicitPbl ?? configuredPbl!);

        string outputDirectory = args.Get("--out") is { } outDir
            ? context.Config.Resolve(outDir)
            : anchor is not null
                ? Path.GetDirectoryName(anchor)!
                : Path.Combine(context.Config.Root, "PB_DEMO_PROJ", "BUILD_OUTPUT");

        var plans = new List<LibraryPlan>();
        if (libraries.Count == 1 && !libraries[0].HasLibraryFileName)
        {
            string pbl = anchor ?? throw new CliException(
                "Flat source folder: pass --pbl <file> (or configure BuildPblPath) to name the library.");
            plans.Add(new LibraryPlan(pbl, libraries[0]));
            return plans;
        }

        foreach (SourceLibrary library in libraries)
        {
            string fileName = library.LibraryFile ?? Path.GetFileName(anchor ?? "application.pbl");
            plans.Add(new LibraryPlan(Path.Combine(outputDirectory, fileName), library));
        }

        // The application's library goes first in the library list (normal search order).
        return plans
            .OrderBy(p => ReferenceEquals(p.Library, applicationLibrary) ? 0 : 1)
            .ToList();
    }

    private static IEnumerable<SourceFile> Ordered(IReadOnlyList<SourceFile> files) =>
        files.OrderBy(static f => ImportRank(f.Type))
             .ThenBy(static f => f.EntryName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Import order: definitions before the objects that use them.</summary>
    private static int ImportRank(OrcaEntryType type) => type switch
    {
        OrcaEntryType.Structure => 1,
        OrcaEntryType.Function => 2,
        OrcaEntryType.Query => 3,
        OrcaEntryType.DataWindow => 4,
        OrcaEntryType.UserObject => 5,
        OrcaEntryType.Menu => 6,
        OrcaEntryType.Window => 7,
        OrcaEntryType.Pipeline => 8,
        OrcaEntryType.Project => 9,
        OrcaEntryType.ProxyObject => 10,
        _ => 11,
    };

    private sealed record VerifySummary(
        int Objects, int Identical, int Different, int Missing, List<object> Problems);

    private sealed record LibraryPlan(string Path, SourceLibrary Library);
}
