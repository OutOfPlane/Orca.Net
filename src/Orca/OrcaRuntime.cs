using System.Reflection;
using System.Runtime.InteropServices;

namespace Orca;

/// <summary>
/// Locates and loads <c>pborc.dll</c> and makes the PowerBuilder runtime folder usable as a
/// DLL search directory.
/// </summary>
/// <remarks>
/// <para>
/// ORCA needs the PowerBuilder runtime DLLs beside it: <c>pborc.dll</c> lives in the IDE
/// folder, while its dependencies (pbvm, pbshr, pbdwe, ...) live in the runtime folder.
/// Windows resolves those dependencies through the process DLL search path, so the runtime
/// directory must be added to it <b>before</b> the first ORCA call. This class does that three
/// ways, in increasing order of precision:
/// </para>
/// <list type="number">
///   <item>prepends the runtime folder to the process <c>PATH</c>;</item>
///   <item>calls <c>SetDefaultDllDirectories</c> + <c>AddDllDirectory</c> + <c>SetDllDirectory</c>;</item>
///   <item>installs a <see cref="NativeLibrary"/> import resolver that loads <c>pborc.dll</c>
///         from the configured absolute path instead of relying on the search order.</item>
/// </list>
/// <para>Both paths are configurable through <see cref="Initialize"/>.</para>
/// </remarks>
public static class OrcaRuntime
{
    /// <summary>Default <c>pborc.dll</c> location for PowerBuilder 25.0.</summary>
    public const string DefaultDllPath =
        @"C:\Program Files (x86)\Appeon\PowerBuilder 25.0\IDE\pborc.dll";

    /// <summary>Default PowerBuilder 25.0 runtime folder.</summary>
    public const string DefaultRuntimePath =
        @"C:\Program Files (x86)\Appeon\Common\PowerBuilder\Runtime 25.0.0.3711";

    private const string NativeDllName = "pborc.dll";

    // LOAD_LIBRARY_SEARCH_DEFAULT_DIRS — enables the directories added via AddDllDirectory.
    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    private static readonly object Gate = new();

    private static bool _initialized;
    private static bool _resolverInstalled;
    private static IntPtr _dllDirectoryCookie = IntPtr.Zero;
    private static IntPtr _module;
    private static string _dllPath = DefaultDllPath;
    private static string _runtimePath = DefaultRuntimePath;

    /// <summary>True once <see cref="Initialize"/> completed successfully.</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>Absolute path of the <c>pborc.dll</c> in use.</summary>
    public static string DllPath => _dllPath;

    /// <summary>Absolute path of the PowerBuilder runtime folder in use.</summary>
    public static string RuntimePath => _runtimePath;

    /// <summary>
    /// Prepares the process for ORCA and loads <c>pborc.dll</c>.
    /// Call this once, before the first <see cref="OrcaSession.Open"/>.
    /// </summary>
    /// <param name="dllPath">
    /// Absolute path to <c>pborc.dll</c>; <c>null</c> or empty uses
    /// <see cref="DefaultDllPath"/>.
    /// </param>
    /// <param name="runtimePath">
    /// Absolute path to the PowerBuilder runtime folder that contains <c>pbvm.dll</c>;
    /// <c>null</c> or empty uses <see cref="DefaultRuntimePath"/>.
    /// </param>
    /// <exception cref="FileNotFoundException"><paramref name="dllPath"/> does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="runtimePath"/> does not exist.</exception>
    /// <exception cref="InvalidOperationException">Already initialized with different paths.</exception>
    public static void Initialize(string? dllPath = null, string? runtimePath = null)
    {
        string resolvedDll = Path.GetFullPath(
            string.IsNullOrWhiteSpace(dllPath) ? DefaultDllPath : dllPath);
        string resolvedRuntime = Path.GetFullPath(
            string.IsNullOrWhiteSpace(runtimePath) ? DefaultRuntimePath : runtimePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!File.Exists(resolvedDll))
        {
            throw new FileNotFoundException(
                $"pborc.dll not found at '{resolvedDll}'. Pass the correct path to " +
                "OrcaRuntime.Initialize(dllPath, runtimePath).", resolvedDll);
        }

        if (!Directory.Exists(resolvedRuntime))
        {
            throw new DirectoryNotFoundException(
                $"PowerBuilder runtime folder not found at '{resolvedRuntime}'. Pass the correct " +
                "path to OrcaRuntime.Initialize(dllPath, runtimePath).");
        }

        lock (Gate)
        {
            if (_initialized)
            {
                if (string.Equals(_dllPath, resolvedDll, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(_runtimePath, resolvedRuntime, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"ORCA is already initialized with '{_dllPath}' and '{_runtimePath}'. " +
                    "pborc.dll cannot be reloaded from a different path in the same process.");
            }

            // 1. Legacy + modern search-path configuration. The runtime folder is prepended to
            //    PATH as well, because some PowerBuilder DLLs are located by child processes or
            //    by LoadLibrary callers that only consult PATH.
            PrependToProcessPath(resolvedRuntime);
            SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            if (_dllDirectoryCookie == IntPtr.Zero)
            {
                _dllDirectoryCookie = AddDllDirectory(resolvedRuntime);
            }

            SetDllDirectory(resolvedRuntime);

            _dllPath = resolvedDll;
            _runtimePath = resolvedRuntime;

            // 2. Explicit resolver: always load the exact file the caller configured.
            if (!_resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(OrcaRuntime).Assembly,
                    static (name, assembly, searchPath) => Resolve(name, assembly));
                _resolverInstalled = true;
            }

            // 3. Load now so configuration errors surface at startup, not at the first call.
            _module = LoadOrcaModule(_dllPath);

            _initialized = true;
        }
    }

    /// <summary>Initializes with the default paths unless the caller already did.</summary>
    internal static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly)
    {
        string name = Path.GetFileName(libraryName);
        bool isOrca = name.Equals(NativeDllName, StringComparison.OrdinalIgnoreCase) ||
                      name.Equals("pborc", StringComparison.OrdinalIgnoreCase);
        if (!isOrca)
        {
            // 0 = "not handled", fall back to the default resolution.
            return IntPtr.Zero;
        }

        lock (Gate)
        {
            if (_module == IntPtr.Zero)
            {
                _module = LoadOrcaModule(_dllPath);
            }

            return _module;
        }
    }

    private static IntPtr LoadOrcaModule(string path)
    {
        try
        {
            return NativeLibrary.Load(path, typeof(OrcaRuntime).Assembly, null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw new InvalidOperationException(
                $"Could not load '{path}'. pborc.dll is a 32-bit DLL, so the calling process " +
                "must be 32-bit (x86) and the PowerBuilder runtime folder must contain its " +
                $"dependencies. Current process is {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}.",
                ex);
        }
    }

    private static void PrependToProcessPath(string directory)
    {
        string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string part in current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part.Trim(), directory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        Environment.SetEnvironmentVariable(
            "PATH", directory + Path.PathSeparator + current);
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
}
