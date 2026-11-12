using System.Runtime.InteropServices;

namespace Orca.Interop;

// Layouts mirror pborca.h exactly as shipped with PowerBuilder 25.0 (x86).
// TCHAR = wchar_t because the wrapper opens a Unicode session (PBORCA_SessionOpen).

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_CONFIG_SESSION
{
    public int eClobber;
    public int eExportEncoding;
    public int bExportHeaders;
    public int bExportIncludeBinary;
    public int bExportCreateFile;
    public IntPtr pExportDirectory;
    public int eImportEncoding;
    public int bDebug;
    public IntPtr filler2;
    public IntPtr filler3;
    public IntPtr filler4;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_ENTRYINFO
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szComments;

    public int lCreateTime;
    public int lObjectSize;
    public int lSourceSize;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_DIRENTRY
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szComments;

    public int lCreateTime;
    public int lEntrySize;
    public IntPtr lpszEntryName;
    public int otEntryType;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_COMPERR
{
    public int iLevel;
    public IntPtr lpszMessageNumber;
    public IntPtr lpszMessageText;
    public uint iColumnNumber;
    public uint iLineNumber;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_HIERARCHY
{
    public IntPtr lpszAncestorName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_REFERENCE
{
    public IntPtr lpszLibraryName;
    public IntPtr lpszEntryName;
    public int otEntryType;
    public int otEntryRefType;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_LINKERR
{
    public IntPtr lpszMessageText;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_BLDERR
{
    public IntPtr lpszMessageText;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_DOTNET_MESSAGE
{
    public int eMessageType;
    public IntPtr lpszMessageText;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_EXEINFO
{
    public IntPtr lpszCompanyName;
    public IntPtr lpszProductName;
    public IntPtr lpszDescription;
    public IntPtr lpszCopyright;
    public IntPtr lpszFileVersion;
    public IntPtr lpszFileVersionNum;
    public IntPtr lpszProductVersion;
    public IntPtr lpszProductVersionNum;
    public IntPtr lpszManifestInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_SETTARGET
{
    public IntPtr lpszLibraryName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct PBORCA_SCC
{
    public IntPtr hWnd;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szProviderName;

    public int lCapabilities;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szUserID;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 301)]
    public string szProject;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 301)]
    public string szLocalProjPath;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 301)]
    public string szAuxPath;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 301)]
    public string szLogFile;

    public IntPtr fpSccMsgHandler;
    public IntPtr fpOrcaMsgHandler;
    public int lCommentLen;
    public int lAppend;
    public IntPtr pCommBlk;
    public int lDeleteTempFiles;
    public int bDeletePblFlag;
}

/// <summary>
/// Guards the hand-written interop layouts against a PowerBuilder version whose
/// <c>pborca.h</c> changed. Sizes are for x86 (4-byte pointers, TCHAR = 2 bytes).
/// </summary>
internal static class InteropLayoutCheck
{
    internal static void Verify()
    {
        Expect<PBORCA_CONFIG_SESSION>(44);
        Expect<PBORCA_ENTRYINFO>(524);
        Expect<PBORCA_DIRENTRY>(528);
        Expect<PBORCA_COMPERR>(20);
        Expect<PBORCA_HIERARCHY>(4);
        Expect<PBORCA_REFERENCE>(16);
        Expect<PBORCA_LINKERR>(4);
        Expect<PBORCA_DOTNET_MESSAGE>(8);
        Expect<PBORCA_EXEINFO>(36);
        Expect<PBORCA_SETTARGET>(4);
        Expect<PBORCA_SCC>(2572);
    }

    private static void Expect<T>(int expected)
    {
        int actual = Marshal.SizeOf<T>();
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Interop layout mismatch for {typeof(T).Name}: expected {expected} bytes, got " +
                $"{actual}. The installed PowerBuilder pborca.h differs from the one this " +
                "wrapper was built against.");
        }
    }
}
