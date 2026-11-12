using System.Text;
using Orca;

namespace OrcaCli;

/// <summary>
/// One exported PowerBuilder source file (<c>.sr?</c>): its object name, type, comments and
/// raw bytes, with helpers to strip the encoding mark and to split an embedded binary section.
/// </summary>
internal sealed record SourceFile(
    string Path,
    string EntryName,
    OrcaEntryType Type,
    string Comments,
    byte[] Content)
{
    private const string HeaderPrefix = "$PBExportHeader$";
    private const string CommentsPrefix = "$PBExportComments$";
    private const string BinarySeparator = "Start of PowerBuilder Binary Data Section";

    /// <summary>Reads every <c>.sr?</c> file in <paramref name="directory"/>.</summary>
    public static IReadOnlyList<SourceFile> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {directory}");
        }

        var result = new List<SourceFile>();
        foreach (string path in Directory.EnumerateFiles(directory).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            OrcaEntryType? type = TypeFromExtension(System.IO.Path.GetExtension(path));
            if (type is null)
            {
                continue;
            }

            byte[] content = File.ReadAllBytes(path);
            (string text, _, _) = Decode(content);
            string? headerName = ReadHeaderValue(text, HeaderPrefix);
            string comments = ReadHeaderValue(text, CommentsPrefix) ?? string.Empty;

            string entryName = headerName is null
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : System.IO.Path.GetFileNameWithoutExtension(headerName);

            result.Add(new SourceFile(path, entryName, type.Value, comments, content));
        }

        return result;
    }

    /// <summary>
    /// Splits the file into the text component and, when present, the embedded binary
    /// component (OLE and similar), which ORCA imports with a second call and
    /// <c>PBORCA_BINARY</c> as the entry type.
    /// </summary>
    public (byte[] Source, byte[]? Binary) SplitBinary()
    {
        (string text, int bomLength, Encoding encoding) = Decode(Content);
        int index = text.IndexOf(BinarySeparator, StringComparison.Ordinal);
        if (index < 0)
        {
            return (GetImportBytes(), null);
        }

        int offset = bomLength + encoding.GetByteCount(text[..index]);
        return (Content[bomLength..offset], Content[offset..]);
    }

    /// <summary>Source bytes without the byte-order mark, ready for <c>CompileEntryImport</c>.</summary>
    public byte[] GetImportBytes()
    {
        (_, int bomLength, _) = Decode(Content);
        return bomLength == 0 ? Content : Content[bomLength..];
    }

    private static (string Text, int BomLength, Encoding Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), 2, Encoding.Unicode);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var utf8 = new UTF8Encoding(false);
            return (utf8.GetString(bytes, 3, bytes.Length - 3), 3, utf8);
        }

        return (Encoding.Latin1.GetString(bytes), 0, Encoding.Latin1);
    }

    private static string? ReadHeaderValue(string text, string prefix)
    {
        int start = 0;
        for (int line = 0; line < 3 && start < text.Length; line++)
        {
            int end = text.IndexOf('\n', start);
            string current = end < 0 ? text[start..] : text[start..end];
            current = current.TrimEnd('\r');

            if (current.StartsWith(prefix, StringComparison.Ordinal))
            {
                return current[prefix.Length..].Trim();
            }

            if (end < 0)
            {
                break;
            }

            start = end + 1;
        }

        return null;
    }

    public static OrcaEntryType? TypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".sra" => OrcaEntryType.Application,
        ".srd" => OrcaEntryType.DataWindow,
        ".srf" => OrcaEntryType.Function,
        ".srm" => OrcaEntryType.Menu,
        ".srq" => OrcaEntryType.Query,
        ".srs" => OrcaEntryType.Structure,
        ".sru" => OrcaEntryType.UserObject,
        ".srw" => OrcaEntryType.Window,
        ".srp" => OrcaEntryType.Pipeline,
        ".srj" => OrcaEntryType.Project,
        ".srx" => OrcaEntryType.ProxyObject,
        _ => null,
    };
}
