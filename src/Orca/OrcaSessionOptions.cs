namespace Orca;

/// <summary>
/// Session-wide behaviour applied by <see cref="OrcaSession.ConfigureSession"/>.
/// </summary>
/// <remarks>
/// ORCA applies the whole configuration block on every call — there is no partial update.
/// The wrapper always populates every field from this object, so the defaults below are the
/// complete session state.
/// </remarks>
public sealed class OrcaSessionOptions
{
    /// <summary>Encoding ORCA writes exported source in. Default: Unicode (UTF-16LE).</summary>
    public OrcaEncoding ExportEncoding { get; set; } = OrcaEncoding.Unicode;

    /// <summary>Encoding ORCA expects in imported source buffers. Default: Unicode.</summary>
    public OrcaEncoding ImportEncoding { get; set; } = OrcaEncoding.Unicode;

    /// <summary>Overwrite policy for export files, PBDs/DLLs, EXEs and libraries.</summary>
    public OrcaClobber Clobber { get; set; } = OrcaClobber.Clobber;

    /// <summary>Write the two <c>$PBExport…$</c> header lines on export.</summary>
    public bool ExportHeaders { get; set; } = true;

    /// <summary>Append the binary component (OLE and similar) to the exported source.</summary>
    public bool ExportIncludeBinary { get; set; }

    /// <summary>Export to a file in <see cref="ExportDirectory"/> instead of to a buffer.</summary>
    public bool ExportToFile { get; set; }

    /// <summary>Directory used when <see cref="ExportToFile"/> is true.</summary>
    public string? ExportDirectory { get; set; }

    /// <summary>Value of the DEBUG conditional-compilation directive.</summary>
    public bool Debug { get; set; } = true;
}
