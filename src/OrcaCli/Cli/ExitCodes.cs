namespace OrcaCli;

/// <summary>Process exit codes, documented in README.md and in every command's help.</summary>
internal static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>Usage error, missing configuration, or an ORCA failure.</summary>
    public const int Error = 1;

    /// <summary><c>verify</c> found differences between a library and its sources.</summary>
    public const int Differences = 2;

    /// <summary><c>build</c> failed with compile or link errors.</summary>
    public const int BuildFailed = 3;
}

/// <summary>An expected, user-facing failure. Caught in <c>Program</c> and printed without a stack trace.</summary>
internal sealed class CliException : Exception
{
    public CliException(string message, int exitCode = ExitCodes.Error)
        : base(message) => ExitCode = exitCode;

    public int ExitCode { get; }
}
