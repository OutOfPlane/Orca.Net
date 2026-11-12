namespace Orca;

/// <summary>One entry (object) in a PowerBuilder library.</summary>
public sealed record OrcaEntry(
    string Name,
    OrcaEntryType Type,
    int SizeBytes,
    string Comments,
    DateTimeOffset CreatedUtc);

/// <summary>Directory listing of a PowerBuilder library.</summary>
public sealed record OrcaLibrary(
    string Path,
    string Comments,
    IReadOnlyList<OrcaEntry> Entries);

/// <summary>Metadata for a single entry: comments, sizes and creation time.</summary>
public sealed record OrcaEntryInfo(
    string Comments,
    DateTimeOffset CreatedUtc,
    int ObjectSizeBytes,
    int SourceSizeBytes);

/// <summary>An object referenced by another object.</summary>
public sealed record OrcaReference(
    string LibraryName,
    string EntryName,
    OrcaEntryType Type);

/// <summary>A compile or link message delivered by an ORCA callback.</summary>
public sealed record OrcaCompileError(
    int Level,
    string MessageNumber,
    string Message,
    int Line,
    int Column)
{
    /// <summary>True for level 4 (<c>CM_ERROR_LEVEL</c>) and above.</summary>
    public bool IsError => Level >= 4;
}

/// <summary>One object to import, for <see cref="OrcaSession.ImportEntries"/>.</summary>
public sealed record OrcaImportEntry(
    string Library,
    string EntryName,
    OrcaEntryType Type,
    string Comments,
    byte[] Source);

/// <summary>Version resource values applied to executables and machine-code DLLs.</summary>
public sealed class OrcaExecutableInfo
{
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public string? Copyright { get; set; }
    public string? FileVersion { get; set; }

    /// <summary>Four comma-separated integers, e.g. <c>"12,0,0,0001"</c>.</summary>
    public string? FileVersionNumber { get; set; }

    public string? ProductVersion { get; set; }

    /// <summary>Four comma-separated integers, e.g. <c>"12,0,0,0001"</c>.</summary>
    public string? ProductVersionNumber { get; set; }

    public string? ManifestInfo { get; set; }
}

/// <summary>Connection properties for a source-control project (<c>PBORCA_SCC</c>).</summary>
public sealed class OrcaSourceControlConfig
{
    public string ProviderName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string LocalProjectPath { get; set; } = string.Empty;
    public string AuxiliaryPath { get; set; } = string.Empty;
    public string LogFile { get; set; } = string.Empty;

    /// <summary>Append to the log file (true) or overwrite it (false).</summary>
    public bool AppendToLog { get; set; }

    /// <summary>Delete temporary files from the local project path when done.</summary>
    public bool DeleteTempFiles { get; set; }

    /// <summary>Delete the PBL files created for the local project path when done.</summary>
    public bool DeletePblFiles { get; set; }
}
