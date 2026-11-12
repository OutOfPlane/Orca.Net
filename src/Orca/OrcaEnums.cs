namespace Orca;

/// <summary>
/// Object / entry type. Numeric values match <c>PBORCA_TYPE</c> in pborca.h, which is
/// declared in a fixed order — do not reorder.
/// </summary>
public enum OrcaEntryType
{
    Application = 0,
    DataWindow = 1,
    Function = 2,
    Menu = 3,
    Query = 4,
    Structure = 5,
    UserObject = 6,
    Window = 7,
    Pipeline = 8,
    Project = 9,
    ProxyObject = 10,

    /// <summary>Binary component of another entry (embedded OLE etc.), not a standalone object.</summary>
    Binary = 11,
}

/// <summary>Source encoding for import and export (<c>PBORCA_ENCODING</c>).</summary>
public enum OrcaEncoding
{
    /// <summary>UTF-16LE, the native PowerBuilder source representation.</summary>
    Unicode = 0,

    Utf8 = 1,

    /// <summary>Printable hex form used for embedded binary sections.</summary>
    HexAscii = 2,

    AnsiDbcs = 3,
}

/// <summary>Overwrite policy for files produced by ORCA (<c>PBORCA_ENUM_FILEWRITE_OPTION</c>).</summary>
public enum OrcaClobber
{
    NoClobber = 0,
    Clobber = 1,
    ClobberAlways = 2,

    /// <summary>Legacy pre-PowerBuilder 10 behaviour.</summary>
    DecidedBySystem = 3,
}

/// <summary>Rebuild flavour (<c>PBORCA_REBLD_TYPE</c>).</summary>
public enum OrcaRebuildType
{
    Full = 0,
    Incremental = 1,
    Migrate = 2,
    ThreePass = 3,
}

/// <summary>Code generation options for <c>DynamicLibraryCreate</c> / <c>ExecutableCreate</c>.</summary>
[Flags]
public enum OrcaBuildFlags
{
    /// <summary>Native Pcode — the default.</summary>
    PCode = 0x00000000,

    MachineCode = 0x00000001,

    NativeMachineCode = 0x00000001,

    /// <summary>Include trace information.</summary>
    TraceInfo = 0x00000010,

    /// <summary>Include script name and line number in runtime errors.</summary>
    ErrorContext = 0x00000020,

    Optimize = 0x00000100,

    OptimizeSpeed = 0x00000100,

    OptimizeSpace = 0x00000200,

    /// <summary>Build a 64-bit executable.</summary>
    X64 = 0x00000800,
}

/// <summary>Flags for <c>SccSetTarget</c>.</summary>
[Flags]
public enum OrcaSourceControlTargetFlags
{
    None = 0x00000000,
    RefreshAll = 0x00000001,
    OutOfDate = 0x00000002,
    ImportOnly = 0x00000004,
    ExcludeCheckout = 0x00000008,
}

/// <summary>File name extensions PowerBuilder uses for exported source.</summary>
public static class OrcaEntryExtensions
{
    /// <summary>Returns the conventional <c>.sr?</c> extension for an entry type.</summary>
    public static string GetSourceExtension(this OrcaEntryType type) => type switch
    {
        OrcaEntryType.Application => ".sra",
        OrcaEntryType.DataWindow => ".srd",
        OrcaEntryType.Function => ".srf",
        OrcaEntryType.Menu => ".srm",
        OrcaEntryType.Query => ".srq",
        OrcaEntryType.Structure => ".srs",
        OrcaEntryType.UserObject => ".sru",
        OrcaEntryType.Window => ".srw",
        OrcaEntryType.Pipeline => ".srp",
        OrcaEntryType.Project => ".srj",
        OrcaEntryType.ProxyObject => ".srx",
        OrcaEntryType.Binary => ".bin",
        _ => ".sr?",
    };
}
