namespace OrcaCli;

/// <summary>
/// Writes a minimal valid Windows icon. ORCA's <c>ExecutableCreate</c> rejects a NULL icon
/// file (<c>PBORCA_INVALIDPARMS</c>), so headless builds need *some* icon; this produces a
/// plain 32×32 placeholder instead of requiring the user to find one.
/// </summary>
internal static class IconFile
{
    public static void WritePlaceholder(string path)
    {
        const int width = 32;
        const int height = 32;
        const int xorRowBytes = 4;  // 32 bits at 1 bpp, already 4-byte aligned
        const int xorBytes = xorRowBytes * height;
        const int andBytes = xorRowBytes * height;
        const int imageBytes = 40 + 8 + xorBytes + andBytes; // header + palette + bitmaps

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        // ICONDIR
        writer.Write((ushort)0);          // reserved
        writer.Write((ushort)1);          // type: icon
        writer.Write((ushort)1);          // image count

        // ICONDIRENTRY
        writer.Write((byte)width);
        writer.Write((byte)height);
        writer.Write((byte)0);            // palette colours
        writer.Write((byte)0);            // reserved
        writer.Write((ushort)1);          // planes
        writer.Write((ushort)1);          // bits per pixel
        writer.Write(imageBytes);
        writer.Write(22);                 // offset: 6 + 16

        // BITMAPINFOHEADER — height is doubled to cover the XOR and AND bitmaps
        writer.Write(40);
        writer.Write(width);
        writer.Write(height * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(0);
        writer.Write(xorBytes + andBytes);
        writer.Write(0);
        writer.Write(0);
        writer.Write(2);                  // colours used
        writer.Write(2);

        // palette: black, white (BGRA)
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write((byte)255);
        writer.Write((byte)255);
        writer.Write((byte)255);
        writer.Write((byte)0);

        // XOR bitmap: solid black
        writer.Write(new byte[xorBytes]);

        // AND mask: all zero = every pixel opaque
        writer.Write(new byte[andBytes]);
    }
}
