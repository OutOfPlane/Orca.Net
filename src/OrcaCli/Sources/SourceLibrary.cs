using Orca;

namespace OrcaCli;

/// <summary>
/// The contents of one PowerBuilder library, as found on disk. The Library painter keeps the
/// library-list membership on export by writing each PBL's objects into a folder named
/// <c>&lt;pbl file name&gt;.src</c> — for example <c>tlfi_docuscan.pbl.src</c> — so the
/// mapping survives without the PBL itself.
/// </summary>
internal sealed record SourceLibrary(string? LibraryFile, string Directory, IReadOnlyList<SourceFile> Files)
{
    /// <summary>Suffix appended to a PBL file name to name its source folder.</summary>
    public const string FolderSuffix = ".src";

    /// <summary>True when the library was discovered from a <c>*.pbl.src</c> folder.</summary>
    public bool HasLibraryFileName => LibraryFile is not null;

    public int EntryCount => Files.Count;

    /// <summary>Returns the folder name a PBL's sources belong in, e.g. <c>tlfi_docuscan.pbl.src</c>.</summary>
    public static string FolderNameFor(string libraryPath) =>
        Path.GetFileName(libraryPath) + FolderSuffix;

    /// <summary>
    /// Reads a source tree: either the mirrored layout (one <c>*.pbl.src</c> folder per
    /// library) or a flat directory holding a single library's files.
    /// </summary>
    public static IReadOnlyList<SourceLibrary> Discover(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {directory}");
        }

        var libraries = new List<SourceLibrary>();
        foreach (string folder in System.IO.Directory
                     .EnumerateDirectories(directory)
                     .Where(IsLibraryFolder)
                     .OrderBy(static d => d, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(folder);
            string libraryFile = name[..^FolderSuffix.Length]; // strip ".src"
            libraries.Add(new SourceLibrary(libraryFile, folder, SourceFile.LoadDirectory(folder)));
        }

        if (libraries.Count > 0)
        {
            return libraries;
        }

        IReadOnlyList<SourceFile> flat = SourceFile.LoadDirectory(directory);
        return flat.Count == 0
            ? Array.Empty<SourceLibrary>()
            : new[] { new SourceLibrary(null, directory, flat) };
    }

    /// <summary>True for folder names like <c>tlfi_docuscan.pbl.src</c> or <c>lib.pkl.src</c>.</summary>
    private static bool IsLibraryFolder(string folder)
    {
        string name = Path.GetFileName(folder);
        if (!name.EndsWith(FolderSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string baseName = name[..^FolderSuffix.Length];
        return baseName.EndsWith(".pbl", StringComparison.OrdinalIgnoreCase) ||
               baseName.EndsWith(".pkl", StringComparison.OrdinalIgnoreCase);
    }
}
