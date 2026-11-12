namespace Orca;

/// <summary>Return codes defined in <c>pborca.h</c>. Zero is success; everything else is negative.</summary>
public enum OrcaResult
{
    Ok = 0,
    InvalidParameters = -1,
    DuplicateOperation = -2,
    ObjectNotFound = -3,
    BadLibrary = -4,
    LibraryListNotSet = -5,
    LibraryNotInList = -6,
    LibraryIoError = -7,
    ObjectExists = -8,
    InvalidName = -9,
    BufferTooSmall = -10,
    CompileError = -11,
    LinkError = -12,
    CurrentApplicationNotSet = -13,
    ObjectHasNoAncestors = -14,
    ObjectHasNoReferences = -15,
    PbdCountError = -16,
    PbdCreateError = -17,
    CheckoutError = -18,
    ComponentBuilderCreateError = -19,
    ComponentBuilderInitError = -20,
    ComponentBuilderBuildError = -21,
    SccFailure = -22,
    RegistryReadError = -23,
    SccLoadDllFailed = -24,
    SccInitFailed = -25,
    OpenProjectFailed = -26,
    TargetNotFound = -27,
    TargetReadError = -28,
    GetInterfaceError = -29,
    ImportOnlyRequired = -30,
    GetConnectPropertiesRequired = -31,
    PbcFileRequired = -32,
    DbcsError = -33,
}

/// <summary>Thrown when an ORCA call returns a non-zero code.</summary>
public sealed class OrcaException : Exception
{
    public OrcaException(OrcaResult code, string operation, string? detail)
        : base(BuildMessage(code, operation, detail))
    {
        Code = code;
        Operation = operation;
        Detail = detail;
    }

    /// <summary>The raw <c>PBORCA_*</c> return code.</summary>
    public OrcaResult Code { get; }

    /// <summary>Name of the wrapper operation that failed, e.g. <c>LibraryEntryExport</c>.</summary>
    public string Operation { get; }

    /// <summary>Message text from <c>PBORCA_SessionGetError</c>, when available.</summary>
    public string? Detail { get; }

    private static string BuildMessage(OrcaResult code, string operation, string? detail)
    {
        string text = $"ORCA {operation} failed: {(int)code} ({code})";
        return string.IsNullOrWhiteSpace(detail) ? text : text + " — " + detail;
    }
}
