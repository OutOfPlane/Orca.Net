namespace OrcaCli;

internal enum Verbosity
{
    Quiet,
    Normal,
    Verbose,
}

/// <summary>
/// All human output goes through this type: <c>--quiet</c> silences progress, <c>--verbose</c>
/// adds per-object detail, and <c>--json</c> moves the chatter to stderr so stdout stays
/// machine-readable.
/// </summary>
internal sealed class Log
{
    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    /// <summary>True when stdout is reserved for a JSON document.</summary>
    public bool MachineReadable { get; set; }

    public void Info(string message) => Write(message, null);

    /// <summary>Detail shown only with <c>--verbose</c>.</summary>
    public void Detail(string message) => Write(message, null, detail: true);

    public void Warn(string message) => Write("warning: " + message, ConsoleColor.Yellow);

    public void Error(string message) => Write("error: " + message, ConsoleColor.Red);

    /// <summary>Writes the machine-readable payload to stdout, always.</summary>
    public void Json(string json) => Console.Out.WriteLine(json);

    private void Write(string message, ConsoleColor? color, bool detail = false)
    {
        if (detail && Verbosity != Verbosity.Verbose)
        {
            return;
        }

        if (!detail && Verbosity == Verbosity.Quiet)
        {
            return;
        }

        TextWriter writer = MachineReadable ? Console.Error : Console.Out;
        bool redirected = MachineReadable ? Console.IsErrorRedirected : Console.IsOutputRedirected;

        if (color is { } value && !redirected)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = value;
            writer.WriteLine(message);
            Console.ForegroundColor = previous;
        }
        else
        {
            writer.WriteLine(message);
        }
    }
}
