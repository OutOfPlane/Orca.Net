using System.Runtime.InteropServices;
using System.Text;

namespace Orca.Interop;

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void ListProc(IntPtr dirEntry, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void ErrProc(IntPtr compErr, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void HierProc(IntPtr hierarchy, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void RefProc(IntPtr reference, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void LnkProc(IntPtr linkErr, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void SetTgtProc(IntPtr setTarget, IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
internal delegate void DotNetProc(IntPtr message, IntPtr userData);

/// <summary>
/// Raw P/Invoke declarations for pborc.dll. All entry points are Unicode ("W") sessions:
/// the header maps PBORCA_SessionOpen to PBORCA_SessionOpenA for ANSI builds, so the
/// unsuffixed name is the Unicode one.
/// </summary>
internal static class NativeMethods
{
    internal const string Dll = "pborc.dll";

    // ---- session ---------------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern IntPtr PBORCA_SessionOpen();

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void PBORCA_SessionClose(IntPtr hORCASession);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern void PBORCA_SessionGetError(
        IntPtr hORCASession, StringBuilder lpszErrorBuffer, int iErrorBufferSize);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SessionSetLibraryList(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pLibNames,
        int iNumberOfLibs);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SessionSetCurrentAppl(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszApplLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszApplName);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ConfigureSession(
        IntPtr hORCASession, ref PBORCA_CONFIG_SESSION pSessionConfig);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SetDebug(IntPtr hORCASession, int bDebug);

    // ---- library ---------------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryCreate(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibComments);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryDelete(
        IntPtr hORCASession, [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryCommentModify(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibComments);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryDirectory(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        StringBuilder lpszLibComments,
        int iCmntsBuffLen,
        ListProc pListProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryInformation(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        out PBORCA_ENTRYINFO pEntryInfo);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryCopy(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszSourceLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszDestLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryMove(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszSourceLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszDestLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryDelete(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryExport(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        IntPtr lpszExportBuffer,
        int lExportBufferSize);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_LibraryEntryExportEx(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        IntPtr lpszExportBuffer,
        int lExportBufferSize,
        out int plReturnSize);

    // ---- compile ---------------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_CompileEntryImport(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszComments,
        IntPtr lpszEntrySyntax,
        int lEntrySyntaxBuffSize,
        IntPtr pCompErrProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_CompileEntryImportList(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pLibraryNames,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pEntryNames,
        int[] otEntryTypes,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pComments,
        IntPtr[] pEntrySyntaxBuffers,
        int[] pEntrySyntaxBuffSizes,
        int iNumberOfEntries,
        IntPtr pCompErrProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_CompileEntryRegenerate(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        IntPtr pCompErrProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ApplicationRebuild(
        IntPtr hORCASession, int eRebldType, IntPtr pCompErrProc, IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ApplicationRebuildEx(
        IntPtr hORCASession, int eRebldType, int bEnableDebugSymbol, IntPtr pCompErrProc, IntPtr pUserData);

    // ---- query -----------------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ObjectQueryHierarchy(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        HierProc pHierarchyProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ObjectQueryReference(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        RefProc pRefProc,
        IntPtr pUserData);

    // ---- build -----------------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SetExeInfo(
        IntPtr hORCASession, ref PBORCA_EXEINFO pExeInfo);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_DynamicLibraryCreate(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszPBRName,
        int lFlags,
        IntPtr pbcPara);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_ExecutableCreate(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszExeName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszIconName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszPBRName,
        IntPtr pLinkErrProc,
        IntPtr pUserData,
        int[] iPBDFlags,
        int iNumberOfPBDFlags,
        int lFlags,
        IntPtr pbcPara);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_DeployWinFormProject(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszProjectName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszIconFile,
        DotNetProc? pDotNetProc,
        IntPtr pUserData);

    // ---- source control --------------------------------------------------

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccGetConnectProperties(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string pWorkspaceFile,
        ref PBORCA_SCC pConfig);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccConnect(IntPtr hORCASession, ref PBORCA_SCC pConfig);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccConnectOffline(IntPtr hORCASession, ref PBORCA_SCC pConfig);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccSetPassword(
        IntPtr hORCASession, [MarshalAs(UnmanagedType.LPWStr)] string pPassword);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccSetTarget(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string pTargetFileName,
        int lFlags,
        SetTgtProc? pSetTgtProc,
        IntPtr pUserData);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccExcludeLibraryList(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] pLibNames,
        int iNumLibs);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccRefreshTarget(IntPtr hORCASession, int eRebldType);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccGetLatestVer(
        IntPtr hORCASession, int nFiles,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] ppFileNames);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccResetRevisionNumber(
        IntPtr hORCASession,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszLibraryName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszEntryName,
        int otEntryType,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszRevisionNum);

    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    internal static extern int PBORCA_SccClose(IntPtr hORCASession);
}
