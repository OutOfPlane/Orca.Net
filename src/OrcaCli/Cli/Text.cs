using System.Text;
using Orca;

namespace OrcaCli;

/// <summary>Formatting helpers shared by the commands.</summary>
internal static class Text
{
    public static string Pad(string value, int width) =>
        value.Length >= width ? value : value + new string(' ', width - value.Length);

    public static byte[] AddUnicodeBom(byte[] bytes)
    {
        if (HasUnicodeBom(bytes))
        {
            return bytes;
        }

        byte[] result = new byte[bytes.Length + 2];
        result[0] = 0xFF;
        result[1] = 0xFE;
        Buffer.BlockCopy(bytes, 0, result, 2, bytes.Length);
        return result;
    }

    public static bool HasUnicodeBom(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;

    public static string DescribeDifference(byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
        {
            return $"expected {expected.Length} bytes, got {actual.Length}";
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                return $"first difference at byte {i}";
            }
        }

        return "unknown";
    }

    public static string SafeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return builder.ToString();
    }

    public static string LogLevel(int level) => level switch
    {
        0 => "context",
        1 => "info",
        2 => "obsolete",
        3 => "warning",
        4 => "error",
        5 => "fatal",
        6 => "datawindow warning",
        _ => level.ToString(),
    };

    public static void CompilerMessages(Log log, IEnumerable<OrcaCompileError> messages, int limit = 10)
    {
        var list = messages.ToList();
        if (list.Count == 0)
        {
            return;
        }

        log.Info($"{list.Count} compiler message(s):");
        foreach (OrcaCompileError message in list.Take(limit))
        {
            string location = message.Line > 0 ? $" (line {message.Line})" : string.Empty;
            log.Info($"  {LogLevel(message.Level)}: {message.Message}{location}");
        }

        if (list.Count > limit)
        {
            log.Info($"  … {list.Count - limit} more");
        }
    }
}
