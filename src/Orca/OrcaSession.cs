using System.Runtime.InteropServices;
using System.Text;
using Orca.Interop;

namespace Orca;

/// <summary>
/// A single ORCA session: the entry point for every library, compile, query, build and
/// source-control operation.
/// </summary>
/// <remarks>
/// <para>Typical use:</para>
/// <code>
/// OrcaRuntime.Initialize(dllPath, runtimePath);
/// using var session = OrcaSession.Open();
/// session.ConfigureSession(new OrcaSessionOptions());
/// var library = session.GetLibrary(pbl);
/// foreach (var entry in library.Entries)
///     File.WriteAllBytes(entry.Name + entry.Type.GetSourceExtension(),
///         session.ExportEntryBytes(pbl, entry.Name, entry.Type));
/// </code>
/// <para>
/// Only one library list and current application can be set per session; close and reopen to
/// switch to another application, exactly as documented for the native API.
/// </para>
/// </remarks>
public sealed class OrcaSession : IDisposable
{
    private const int CommentBufferChars = 512;

    private readonly IntPtr _handle;
    private readonly List<OrcaCompileError> _compileErrors = new();
    private bool _disposed;

    private OrcaSession(IntPtr handle) => _handle = handle;

    /// <summary>Native session handle (<c>HPBORCA</c>).</summary>
    public IntPtr Handle => _handle;

    /// <summary>Messages reported by the most recent compile/link operation.</summary>
    public IReadOnlyList<OrcaCompileError> LastCompileErrors => _compileErrors;

    /// <summary>Opens a session. Loads <c>pborc.dll</c> through <see cref="OrcaRuntime"/> if needed.</summary>
    public static OrcaSession Open()
    {
        OrcaRuntime.EnsureInitialized();
        InteropLayoutCheck.Verify();

        IntPtr handle = NativeMethods.PBORCA_SessionOpen();
        if (handle == IntPtr.Zero)
        {
            throw new OrcaException(
                OrcaResult.InvalidParameters, "SessionOpen", "ORCA returned no session handle (out of memory).");
        }

        return new OrcaSession(handle);
    }

    /// <summary>Closes the session and frees everything ORCA allocated for it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.PBORCA_SessionClose(_handle);
    }

    // ------------------------------------------------------------------ session

    /// <summary>Applies session-wide behaviour (encoding, clobber policy, headers, DEBUG).</summary>
    public void ConfigureSession(OrcaSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IntPtr directory = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(options.ExportDirectory))
            {
                directory = Marshal.StringToHGlobalUni(options.ExportDirectory);
            }

            var config = new PBORCA_CONFIG_SESSION
            {
                eClobber = (int)options.Clobber,
                eExportEncoding = (int)options.ExportEncoding,
                bExportHeaders = options.ExportHeaders ? 1 : 0,
                bExportIncludeBinary = options.ExportIncludeBinary ? 1 : 0,
                bExportCreateFile = options.ExportToFile ? 1 : 0,
                pExportDirectory = directory,
                eImportEncoding = (int)options.ImportEncoding,
                bDebug = options.Debug ? 1 : 0,
            };

            int rc = NativeMethods.PBORCA_ConfigureSession(_handle, ref config);
            ThrowIfFailed(rc, "ConfigureSession");
        }
        finally
        {
            if (directory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(directory);
            }
        }
    }

    /// <summary>Sets the DEBUG conditional-compilation directive for later compiler calls.</summary>
    public void SetDebug(bool enabled) =>
        ThrowIfFailed(NativeMethods.PBORCA_SetDebug(_handle, enabled ? 1 : 0), "SetDebug");

    /// <summary>Sets the library search path (call before <see cref="SetCurrentApplication"/>).</summary>
    public void SetLibraryList(params string[] libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);
        if (libraries.Length == 0)
        {
            throw new ArgumentException("At least one library is required.", nameof(libraries));
        }

        ThrowIfFailed(
            NativeMethods.PBORCA_SessionSetLibraryList(_handle, libraries, libraries.Length),
            "SessionSetLibraryList");
    }

    /// <summary>Sets the current Application object. The library must be on the library list.</summary>
    public void SetCurrentApplication(string applicationLibrary, string applicationName)
    {
        ThrowIfFailed(
            NativeMethods.PBORCA_SessionSetCurrentAppl(_handle, applicationLibrary, applicationName),
            "SessionSetCurrentAppl");
    }

    /// <summary>
    /// Bootstrapping mode: ORCA installs a temporary Application object so that a new
    /// Application object can be imported into an otherwise empty library.
    /// </summary>
    public void UseTemporaryApplication() =>
        ThrowIfFailed(
            NativeMethods.PBORCA_SessionSetCurrentAppl(_handle, null, null),
            "SessionSetCurrentAppl(NULL)");

    /// <summary>Returns ORCA's text for the current error, or an empty string.</summary>
    public string GetErrorMessage()
    {
        var buffer = new StringBuilder(CommentBufferChars);
        NativeMethods.PBORCA_SessionGetError(_handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ------------------------------------------------------------------ library

    /// <summary>Creates an empty PBL on disk. No library list required.</summary>
    public void CreateLibrary(string libraryPath, string comments = "") =>
        ThrowIfFailed(NativeMethods.PBORCA_LibraryCreate(_handle, libraryPath, comments), "LibraryCreate");

    /// <summary>Deletes a PBL file. A read-only file needs <see cref="OrcaClobber.ClobberAlways"/>.</summary>
    public void DeleteLibrary(string libraryPath) =>
        ThrowIfFailed(NativeMethods.PBORCA_LibraryDelete(_handle, libraryPath), "LibraryDelete");

    /// <summary>Replaces a library's comment.</summary>
    public void SetLibraryComment(string libraryPath, string comments) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_LibraryCommentModify(_handle, libraryPath, comments),
            "LibraryCommentModify");

    /// <summary>Reads a library's comment and full entry list.</summary>
    public OrcaLibrary GetLibrary(string libraryPath)
    {
        var entries = new List<OrcaEntry>();
        var comments = new StringBuilder(CommentBufferChars);

        ListProc callback = (dirEntry, _) =>
        {
            PBORCA_DIRENTRY native = Marshal.PtrToStructure<PBORCA_DIRENTRY>(dirEntry);
            string name = native.lpszEntryName == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(native.lpszEntryName) ?? string.Empty;

            entries.Add(new OrcaEntry(
                name,
                (OrcaEntryType)native.otEntryType,
                native.lEntrySize,
                native.szComments ?? string.Empty,
                ToTime(native.lCreateTime)));
        };

        int rc = NativeMethods.PBORCA_LibraryDirectory(
            _handle, libraryPath, comments, comments.Capacity, callback, IntPtr.Zero);
        ThrowIfFailed(rc, "LibraryDirectory");

        return new OrcaLibrary(libraryPath, comments.ToString(), entries);
    }

    /// <summary>Returns comments, sizes and creation time for one entry.</summary>
    public OrcaEntryInfo GetEntryInfo(string libraryPath, string entryName, OrcaEntryType type)
    {
        int rc = NativeMethods.PBORCA_LibraryEntryInformation(
            _handle, libraryPath, entryName, (int)type, out PBORCA_ENTRYINFO native);
        ThrowIfFailed(rc, "LibraryEntryInformation");

        return new OrcaEntryInfo(
            native.szComments ?? string.Empty,
            ToTime(native.lCreateTime),
            native.lObjectSize,
            native.lSourceSize);
    }

    /// <summary>Copies an entry, including its binary component, to another library.</summary>
    public void CopyEntry(string sourceLibrary, string destinationLibrary, string entryName, OrcaEntryType type) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_LibraryEntryCopy(
                _handle, sourceLibrary, destinationLibrary, entryName, (int)type),
            "LibraryEntryCopy");

    /// <summary>Moves an entry, including its binary component, to another library.</summary>
    public void MoveEntry(string sourceLibrary, string destinationLibrary, string entryName, OrcaEntryType type) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_LibraryEntryMove(
                _handle, sourceLibrary, destinationLibrary, entryName, (int)type),
            "LibraryEntryMove");

    /// <summary>Deletes one entry from a library.</summary>
    public void DeleteEntry(string libraryPath, string entryName, OrcaEntryType type) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_LibraryEntryDelete(_handle, libraryPath, entryName, (int)type),
            "LibraryEntryDelete");

    /// <summary>
    /// Exports an entry's source as raw bytes, exactly as ORCA produced it (no BOM added,
    /// no text decoding). Grows the buffer and retries on <c>PBORCA_BUFFERTOOSMALL</c>.
    /// </summary>
    public byte[] ExportEntryBytes(string libraryPath, string entryName, OrcaEntryType type)
    {
        int capacity = EstimateExportBuffer(libraryPath, entryName, type);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            byte[] buffer = new byte[capacity];
            GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                int rc = NativeMethods.PBORCA_LibraryEntryExportEx(
                    _handle,
                    libraryPath,
                    entryName,
                    (int)type,
                    pin.AddrOfPinnedObject(),
                    capacity,
                    out int written);

                if (rc == (int)OrcaResult.Ok)
                {
                    if (written < 0 || written > capacity)
                    {
                        throw new InvalidOperationException(
                            $"ORCA reported {written} bytes written into a {capacity}-byte buffer.");
                    }

                    byte[] result = new byte[written];
                    Buffer.BlockCopy(buffer, 0, result, 0, written);
                    return result;
                }

                if (rc == (int)OrcaResult.BufferTooSmall)
                {
                    capacity = checked(capacity * 4);
                    continue;
                }

                ThrowIfFailed(rc, "LibraryEntryExport");
            }
            finally
            {
                pin.Free();
            }
        }

        throw new InvalidOperationException(
            $"Export of '{entryName}' did not fit in a {capacity}-byte buffer after 8 attempts.");
    }

    /// <summary>
    /// Exports an entry and decodes it as text using the session's export encoding
    /// (Unicode by default). A leading byte-order mark is removed.
    /// </summary>
    public string ExportEntryText(
        string libraryPath, string entryName, OrcaEntryType type, OrcaEncoding encoding = OrcaEncoding.Unicode)
    {
        byte[] bytes = ExportEntryBytes(libraryPath, entryName, type);
        string text = encoding switch
        {
            OrcaEncoding.Utf8 => new UTF8Encoding(false).GetString(bytes),
            OrcaEncoding.AnsiDbcs => Encoding.Latin1.GetString(bytes),
            OrcaEncoding.HexAscii => Encoding.ASCII.GetString(bytes),
            _ => Encoding.Unicode.GetString(bytes),
        };

        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    /// <summary>
    /// Exports an entry to <paramref name="directory"/> as <c>&lt;entry&gt;.sr?</c>.
    /// Returns the full path written.
    /// </summary>
    public string ExportEntryToFile(
        string libraryPath, string entryName, OrcaEntryType type, string directory,
        bool prependByteOrderMark = true, OrcaEncoding encoding = OrcaEncoding.Unicode)
    {
        byte[] bytes = ExportEntryBytes(libraryPath, entryName, type);
        Directory.CreateDirectory(directory);

        string fileName = entryName + type.GetSourceExtension();
        string fullPath = Path.Combine(directory, fileName);

        if (prependByteOrderMark && encoding == OrcaEncoding.Unicode && !HasUnicodeBom(bytes))
        {
            byte[] withBom = new byte[bytes.Length + 2];
            withBom[0] = 0xFF;
            withBom[1] = 0xFE;
            Buffer.BlockCopy(bytes, 0, withBom, 2, bytes.Length);
            bytes = withBom;
        }

        File.WriteAllBytes(fullPath, bytes);
        return fullPath;
    }

    // ------------------------------------------------------------------ compile

    /// <summary>
    /// Imports one object's source and compiles it. Requires a library list and a current
    /// application. A failed import still leaves the object in the library, and any existing
    /// entry of the same name is deleted first.
    /// </summary>
    public void ImportEntry(
        string libraryPath,
        string entryName,
        OrcaEntryType type,
        string comments,
        byte[] source,
        Action<OrcaCompileError>? onMessage = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length == 0)
        {
            throw new ArgumentException("Source buffer is empty.", nameof(source));
        }

        _compileErrors.Clear();
        GCHandle pin = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            IntPtr callback = IntPtr.Zero;
            ErrProc? proc = null;
            if (onMessage is not null)
            {
                proc = CreateErrorProc(onMessage);
                callback = Marshal.GetFunctionPointerForDelegate(proc);
            }

            int rc = NativeMethods.PBORCA_CompileEntryImport(
                _handle,
                libraryPath,
                entryName,
                (int)type,
                comments,
                pin.AddrOfPinnedObject(),
                source.Length,
                callback,
                IntPtr.Zero);

            ThrowIfFailed(rc, "CompileEntryImport");
            GC.KeepAlive(proc);
        }
        finally
        {
            pin.Free();
        }
    }

    /// <summary>
    /// Imports several objects in one pass. Put ancestors before descendants and user
    /// objects before the objects that reference them.
    /// </summary>
    public void ImportEntries(
        IReadOnlyList<OrcaImportEntry> entries, Action<OrcaCompileError>? onMessage = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        int count = entries.Count;
        var libraries = new string[count];
        var names = new string[count];
        var types = new int[count];
        var comments = new string[count];
        var sizes = new int[count];
        var pointers = new IntPtr[count];
        var pins = new GCHandle[count];

        _compileErrors.Clear();
        try
        {
            for (int i = 0; i < count; i++)
            {
                OrcaImportEntry entry = entries[i];
                libraries[i] = entry.Library;
                names[i] = entry.EntryName;
                types[i] = (int)entry.Type;
                comments[i] = entry.Comments;
                sizes[i] = entry.Source.Length;
                pins[i] = GCHandle.Alloc(entry.Source, GCHandleType.Pinned);
                pointers[i] = pins[i].AddrOfPinnedObject();
            }

            IntPtr callback = IntPtr.Zero;
            ErrProc? proc = null;
            if (onMessage is not null)
            {
                proc = CreateErrorProc(onMessage);
                callback = Marshal.GetFunctionPointerForDelegate(proc);
            }

            int rc = NativeMethods.PBORCA_CompileEntryImportList(
                _handle, libraries, names, types, comments, pointers, sizes, count, callback, IntPtr.Zero);

            ThrowIfFailed(rc, "CompileEntryImportList");
            GC.KeepAlive(proc);
        }
        finally
        {
            foreach (GCHandle pin in pins)
            {
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }
        }
    }

    /// <summary>Recompiles one object already in a library.</summary>
    public void RegenerateEntry(
        string libraryPath, string entryName, OrcaEntryType type, Action<OrcaCompileError>? onMessage = null)
    {
        _compileErrors.Clear();
        IntPtr callback = IntPtr.Zero;
        ErrProc? proc = null;
        if (onMessage is not null)
        {
            proc = CreateErrorProc(onMessage);
            callback = Marshal.GetFunctionPointerForDelegate(proc);
        }

        int rc = NativeMethods.PBORCA_CompileEntryRegenerate(
            _handle, libraryPath, entryName, (int)type, callback, IntPtr.Zero);

        ThrowIfFailed(rc, "CompileEntryRegenerate");
        GC.KeepAlive(proc);
    }

    /// <summary>
    /// Rebuilds every object in every library on the list. Use after a batch import to
    /// resolve dependencies and circular references.
    /// </summary>
    public void Rebuild(OrcaRebuildType type = OrcaRebuildType.Full, Action<OrcaCompileError>? onMessage = null)
    {
        _compileErrors.Clear();
        IntPtr callback = IntPtr.Zero;
        ErrProc? proc = null;
        if (onMessage is not null)
        {
            proc = CreateErrorProc(onMessage);
            callback = Marshal.GetFunctionPointerForDelegate(proc);
        }

        int rc = NativeMethods.PBORCA_ApplicationRebuild(_handle, (int)type, callback, IntPtr.Zero);

        ThrowIfFailed(rc, "ApplicationRebuild");
        GC.KeepAlive(proc);
    }

    // ------------------------------------------------------------------ query

    /// <summary>
    /// Returns the ancestor chain of a window, menu or user object. Empty when the object has
    /// no ancestors (not an error).
    /// </summary>
    public IReadOnlyList<string> QueryAncestors(string libraryPath, string entryName, OrcaEntryType type)
    {
        var ancestors = new List<string>();

        HierProc callback = (hierarchy, _) =>
        {
            PBORCA_HIERARCHY native = Marshal.PtrToStructure<PBORCA_HIERARCHY>(hierarchy);
            ancestors.Add(ToManagedString(native.lpszAncestorName));
        };

        int rc = NativeMethods.PBORCA_ObjectQueryHierarchy(
            _handle, libraryPath, entryName, (int)type, callback, IntPtr.Zero);

        if (rc == (int)OrcaResult.ObjectHasNoAncestors)
        {
            return ancestors;
        }

        ThrowIfFailed(rc, "ObjectQueryHierarchy");
        return ancestors;
    }

    /// <summary>Returns the objects an entry references. Empty when it has none.</summary>
    public IReadOnlyList<OrcaReference> QueryReferences(string libraryPath, string entryName, OrcaEntryType type)
    {
        var references = new List<OrcaReference>();

        RefProc callback = (reference, _) =>
        {
            PBORCA_REFERENCE native = Marshal.PtrToStructure<PBORCA_REFERENCE>(reference);
            references.Add(new OrcaReference(
                ToManagedString(native.lpszLibraryName),
                ToManagedString(native.lpszEntryName),
                (OrcaEntryType)native.otEntryType));
        };

        int rc = NativeMethods.PBORCA_ObjectQueryReference(
            _handle, libraryPath, entryName, (int)type, callback, IntPtr.Zero);

        if (rc == (int)OrcaResult.ObjectHasNoReferences)
        {
            return references;
        }

        ThrowIfFailed(rc, "ObjectQueryReference");
        return references;
    }

    // ------------------------------------------------------------------ build

    /// <summary>Sets version-resource values for executables and machine-code DLLs built later.</summary>
    public void SetExecutableInfo(OrcaExecutableInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var text = new List<IntPtr>();
        try
        {
            PBORCA_EXEINFO native = new()
            {
                lpszCompanyName = Alloc(info.CompanyName, text),
                lpszProductName = Alloc(info.ProductName, text),
                lpszDescription = Alloc(info.Description, text),
                lpszCopyright = Alloc(info.Copyright, text),
                lpszFileVersion = Alloc(info.FileVersion, text),
                lpszFileVersionNum = Alloc(info.FileVersionNumber, text),
                lpszProductVersion = Alloc(info.ProductVersion, text),
                lpszProductVersionNum = Alloc(info.ProductVersionNumber, text),
                lpszManifestInfo = Alloc(info.ManifestInfo, text),
            };

            ThrowIfFailed(NativeMethods.PBORCA_SetExeInfo(_handle, ref native), "SetExeInfo");
        }
        finally
        {
            foreach (IntPtr pointer in text)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    /// <summary>
    /// Builds a PBD (Pcode) or PowerBuilder DLL (machine code) from one library. Build these
    /// before the executable that links them.
    /// </summary>
    public void CreateDynamicLibrary(
        string libraryPath, string? resourceFile = null, OrcaBuildFlags flags = OrcaBuildFlags.PCode) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_DynamicLibraryCreate(_handle, libraryPath, resourceFile, (int)flags, IntPtr.Zero),
            "DynamicLibraryCreate");

    /// <summary>Builds an executable from the session's library list.</summary>
    /// <param name="executablePath">Full path of the EXE to create.</param>
    /// <param name="iconFile">Icon file to embed; it must already exist. Null lets ORCA use its default.</param>
    /// <param name="resourceFile">PowerBuilder resource file (<c>.pbr</c>), or <c>null</c>.</param>
    /// <param name="pbdFlags">
    /// One flag per library, in library-list order: <c>true</c> = the library is already a
    /// PBD/DLL and must be linked, <c>false</c> = its objects go into the EXE.
    /// </param>
    /// <param name="flags">Pcode or machine-code options.</param>
    /// <param name="onLinkError">Optional callback for each link error.</param>
    public void CreateExecutable(
        string executablePath,
        string? iconFile,
        string? resourceFile,
        IReadOnlyList<bool> pbdFlags,
        OrcaBuildFlags flags = OrcaBuildFlags.PCode,
        Action<OrcaCompileError>? onLinkError = null)
    {
        ArgumentNullException.ThrowIfNull(pbdFlags);

        _compileErrors.Clear();
        int[] nativeFlags = pbdFlags.Select(static f => f ? 1 : 0).ToArray();

        IntPtr callback = IntPtr.Zero;
        LnkProc? proc = null;
        if (onLinkError is not null)
        {
            proc = (IntPtr linkErr, IntPtr _) =>
            {
                PBORCA_LINKERR native = Marshal.PtrToStructure<PBORCA_LINKERR>(linkErr);
                var error = new OrcaCompileError(4, string.Empty, ToManagedString(native.lpszMessageText), 0, 0);
                _compileErrors.Add(error);
                onLinkError(error);
            };
            callback = Marshal.GetFunctionPointerForDelegate(proc);
        }

        int rc = NativeMethods.PBORCA_ExecutableCreate(
            _handle,
            executablePath,
            iconFile,
            resourceFile,
            callback,
            IntPtr.Zero,
            nativeFlags,
            nativeFlags.Length,
            (int)flags,
            IntPtr.Zero);

        ThrowIfFailed(rc, "ExecutableCreate");
        GC.KeepAlive(proc);
    }

    /// <summary>Deploys a Windows Forms project (assemblies generated per the project object).</summary>
    public void DeployWinFormsProject(
        string libraryPath, string projectName, string iconFile, Action<OrcaCompileError>? onMessage = null)
    {
        _compileErrors.Clear();
        DotNetProc? proc = null;
        if (onMessage is not null)
        {
            proc = (message, _) =>
            {
                PBORCA_DOTNET_MESSAGE native = Marshal.PtrToStructure<PBORCA_DOTNET_MESSAGE>(message);
                var converted = new OrcaCompileError(
                    native.eMessageType, string.Empty, ToManagedString(native.lpszMessageText), 0, 0);
                _compileErrors.Add(converted);
                onMessage(converted);
            };
        }

        int rc = NativeMethods.PBORCA_DeployWinFormProject(
            _handle, libraryPath, projectName, iconFile, proc, IntPtr.Zero);

        ThrowIfFailed(rc, "DeployWinFormProject");
        GC.KeepAlive(proc);
    }

    // ------------------------------------------------------------------ source control

    /// <summary>Reads SCC connection properties stored in a PowerBuilder workspace (<c>.pbw</c>).</summary>
    public OrcaSourceControlConfig GetSourceControlProperties(string workspaceFile)
    {
        PBORCA_SCC native = NewSccBlock();
        ThrowIfFailed(
            NativeMethods.PBORCA_SccGetConnectProperties(_handle, workspaceFile, ref native),
            "SccGetConnectProperties");

        return FromNative(native);
    }

    /// <summary>Connects to the source-control provider and opens the project.</summary>
    public void ConnectSourceControl(OrcaSourceControlConfig config)
    {
        PBORCA_SCC native = ToNative(config);
        ThrowIfFailed(NativeMethods.PBORCA_SccConnect(_handle, ref native), "SccConnect");
    }

    /// <summary>
    /// Simulates a connection for import-only refreshes. Requires a prior
    /// <see cref="GetSourceControlProperties"/> call and
    /// <see cref="OrcaSourceControlTargetFlags.ImportOnly"/> on the next
    /// <see cref="SetSourceControlTarget"/>.
    /// </summary>
    public void ConnectSourceControlOffline(OrcaSourceControlConfig config)
    {
        PBORCA_SCC native = ToNative(config);
        ThrowIfFailed(NativeMethods.PBORCA_SccConnectOffline(_handle, ref native), "SccConnectOffline");
    }

    /// <summary>Sets the SCC password before connecting.</summary>
    public void SetSourceControlPassword(string password) =>
        ThrowIfFailed(NativeMethods.PBORCA_SccSetPassword(_handle, password), "SccSetPassword");

    /// <summary>
    /// Retrieves the target from source control and performs an implicit
    /// <see cref="SetLibraryList"/> + <see cref="SetCurrentApplication"/>.
    /// </summary>
    public void SetSourceControlTarget(
        string targetFile,
        OrcaSourceControlTargetFlags flags,
        Action<string>? onLibrary = null)
    {
        SetTgtProc? proc = null;
        if (onLibrary is not null)
        {
            proc = (setTarget, _) =>
            {
                PBORCA_SETTARGET native = Marshal.PtrToStructure<PBORCA_SETTARGET>(setTarget);
                onLibrary(ToManagedString(native.lpszLibraryName));
            };
        }

        int rc = NativeMethods.PBORCA_SccSetTarget(_handle, targetFile, (int)flags, proc, IntPtr.Zero);
        ThrowIfFailed(rc, "SccSetTarget");
        GC.KeepAlive(proc);
    }

    /// <summary>Libraries to skip during the next <see cref="RefreshSourceControlTarget"/>.</summary>
    public void ExcludeLibrariesFromRefresh(params string[] libraries) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_SccExcludeLibraryList(_handle, libraries, libraries.Length),
            "SccExcludeLibraryList");

    /// <summary>Refreshes and recompiles the target from source control.</summary>
    public void RefreshSourceControlTarget(OrcaRebuildType type = OrcaRebuildType.Full) =>
        ThrowIfFailed(NativeMethods.PBORCA_SccRefreshTarget(_handle, (int)type), "SccRefreshTarget");

    /// <summary>Retrieves loose files (images, .pbr, help files) that are not PBL entries.</summary>
    public void GetLatestVersion(params string[] fileNames) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_SccGetLatestVer(_handle, fileNames.Length, fileNames),
            "SccGetLatestVer");

    /// <summary>
    /// Rewrites the revision number stored as metadata in the local PBL (<c>null</c> clears it).
    /// </summary>
    public void ResetRevisionNumber(string libraryPath, string entryName, OrcaEntryType type, string? revision) =>
        ThrowIfFailed(
            NativeMethods.PBORCA_SccResetRevisionNumber(_handle, libraryPath, entryName, (int)type, revision),
            "SccResetRevisionNumber");

    /// <summary>Disconnects from source control. Call before <see cref="Dispose"/>.</summary>
    public void CloseSourceControl() =>
        ThrowIfFailed(NativeMethods.PBORCA_SccClose(_handle), "SccClose");

    // ------------------------------------------------------------------ helpers

    private int EstimateExportBuffer(string libraryPath, string entryName, OrcaEntryType type)
    {
        try
        {
            OrcaEntryInfo info = GetEntryInfo(libraryPath, entryName, type);

            // lSourceSize is always a Unicode byte count, plus headers and binary payload
            // when those are enabled. Leave generous slack for encoding conversion.
            long estimate = (long)info.SourceSizeBytes * 2 + 16384;
            return (int)Math.Min(Math.Max(estimate, 16384), int.MaxValue / 4);
        }
        catch (OrcaException)
        {
            return 65536;
        }
    }

    private ErrProc CreateErrorProc(Action<OrcaCompileError> onMessage) =>
        (compErr, _) =>
        {
            PBORCA_COMPERR native = Marshal.PtrToStructure<PBORCA_COMPERR>(compErr);
            var error = new OrcaCompileError(
                native.iLevel,
                ToManagedString(native.lpszMessageNumber),
                ToManagedString(native.lpszMessageText),
                (int)native.iLineNumber,
                (int)native.iColumnNumber);

            _compileErrors.Add(error);
            onMessage(error);
        };

    private static IntPtr Alloc(string? value, List<IntPtr> allocated)
    {
        if (value is null)
        {
            return IntPtr.Zero;
        }

        IntPtr pointer = Marshal.StringToHGlobalUni(value);
        allocated.Add(pointer);
        return pointer;
    }

    private void ThrowIfFailed(int code, string operation)
    {
        if (code == (int)OrcaResult.Ok)
        {
            return;
        }

        string detail = GetErrorMessage();
        if (string.IsNullOrWhiteSpace(detail) && _compileErrors.Count > 0)
        {
            detail = string.Join("; ", _compileErrors.Take(5).Select(static e => e.Message));
        }

        throw new OrcaException((OrcaResult)code, operation, detail);
    }

    private static string ToManagedString(IntPtr value) =>
        value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(value) ?? string.Empty;

    private static DateTimeOffset ToTime(int value)
    {
        // PowerBuilder stores seconds since 1970 in a LONG.
        if (value <= 0)
        {
            return DateTimeOffset.UnixEpoch;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    private static bool HasUnicodeBom(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;

    private static PBORCA_SCC NewSccBlock() => new()
    {
        szProviderName = string.Empty,
        szUserID = string.Empty,
        szProject = string.Empty,
        szLocalProjPath = string.Empty,
        szAuxPath = string.Empty,
        szLogFile = string.Empty,
    };

    private static PBORCA_SCC ToNative(OrcaSourceControlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        PBORCA_SCC native = NewSccBlock();
        native.szProviderName = Fit(config.ProviderName, 31, nameof(config.ProviderName));
        native.szUserID = Fit(config.UserId, 31, nameof(config.UserId));
        native.szProject = Fit(config.Project, 300, nameof(config.Project));
        native.szLocalProjPath = Fit(config.LocalProjectPath, 300, nameof(config.LocalProjectPath));
        native.szAuxPath = Fit(config.AuxiliaryPath, 300, nameof(config.AuxiliaryPath));
        native.szLogFile = Fit(config.LogFile, 300, nameof(config.LogFile));
        native.lAppend = config.AppendToLog ? 1 : 0;
        native.lDeleteTempFiles = config.DeleteTempFiles ? 1 : 0;
        native.bDeletePblFlag = config.DeletePblFiles ? 1 : 0;
        return native;
    }

    private static OrcaSourceControlConfig FromNative(PBORCA_SCC native) => new()
    {
        ProviderName = native.szProviderName ?? string.Empty,
        UserId = native.szUserID ?? string.Empty,
        Project = native.szProject ?? string.Empty,
        LocalProjectPath = native.szLocalProjPath ?? string.Empty,
        AuxiliaryPath = native.szAuxPath ?? string.Empty,
        LogFile = native.szLogFile ?? string.Empty,
        AppendToLog = native.lAppend != 0,
        DeleteTempFiles = native.lDeleteTempFiles != 0,
        DeletePblFiles = native.bDeletePblFlag != 0,
    };

    private static string Fit(string? value, int maxLength, string name)
    {
        value ??= string.Empty;
        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value is {value.Length} characters; the ORCA structure allows {maxLength}.", name);
        }

        return value;
    }
}
