using System.Text;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// A synthetic WebP corpus. Two things here that neither JPEG nor PNG has: an odd-sized chunk carries a
/// pad byte that is part of the container but not of the chunk, and the extended header (<c>VP8X</c>)
/// carries FLAGS that must stop claiming metadata the file no longer has.
/// </summary>
internal static class SyntheticWebP
{
    public const byte ExifFlag = 0x08;
    public const byte XmpFlag = 0x04;
    public const byte AlphaFlag = 0x10;
    public const byte IccFlag = 0x20;

    public static readonly byte[] LocationSentinel = "50.0755N 14.4378E"u8.ToArray();

    public static byte[] File(params byte[][] chunks)
    {
        var body = chunks.SelectMany(chunk => chunk).ToArray();
        return [.. "RIFF"u8, .. LittleEndian((uint)(4 + body.Length)), .. "WEBP"u8, .. body];
    }

    /// <summary>A container whose declared RIFF size does not describe what follows it.</summary>
    public static byte[] FileWithDeclaredSize(uint declaredSize, params byte[][] chunks)
    {
        var file = File(chunks);
        LittleEndian(declaredSize).CopyTo(file, 4);
        return file;
    }

    public static byte[] Chunk(string fourCc, ReadOnlySpan<byte> data)
    {
        var padding = data.Length % 2;
        return
        [
            .. Encoding.ASCII.GetBytes(fourCc),
            .. LittleEndian((uint)data.Length),
            .. data,
            .. new byte[padding]
        ];
    }

    public static byte[] Vp8x(int flags) =>
        Chunk("VP8X", [(byte)flags, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x07, 0x00, 0x00]);

    /// <summary>A chunk whose declared size does not describe what follows it.</summary>
    public static byte[] ChunkWithDeclaredSize(string fourCc, byte[] data, uint declaredSize)
    {
        var honest = Chunk(fourCc, data);
        LittleEndian(declaredSize).CopyTo(honest, 4);
        return honest;
    }

    public static byte[] ExifChunk() => Chunk("EXIF", [.. "MM\0*"u8, .. LocationSentinel]);

    public static byte[] XmpChunk() =>
        Chunk("XMP ", [.. "<x:xmpmeta><gps>"u8, .. LocationSentinel, .. "</gps></x:xmpmeta>"u8]);

    public static byte[] ImageData() => Chunk("VP8 ", [0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A, 0x08, 0x00, 0x08]);

    public static byte[] AlphaData() => Chunk("ALPH", [0x01, 0x02, 0x03]);

    private static byte[] LittleEndian(uint value) =>
        [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
}
