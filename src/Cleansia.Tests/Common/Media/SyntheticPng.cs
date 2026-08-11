using System.Text;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// A synthetic PNG corpus. Chunks carry their own CRC, so a scrub that only removes them recomputes
/// nothing — which is only true if the fixtures carry real CRCs, or a test would pass over a container
/// no decoder would accept.
/// </summary>
internal static class SyntheticPng
{
    public static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static readonly byte[] LocationSentinel = "50.0755N 14.4378E"u8.ToArray();

    public static readonly byte[] DeviceSentinel = "SentinelCam"u8.ToArray();

    public static byte[] Image(params byte[][] extraChunks) =>
    [
        .. Signature,
        .. Ihdr,
        .. extraChunks.SelectMany(chunk => chunk),
        .. Idat,
        .. Iend
    ];

    public static byte[] Chunk(string type, ReadOnlySpan<byte> data)
    {
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(typeAndData, 0);
        data.CopyTo(typeAndData.AsSpan(4));

        return [.. BigEndian((uint)data.Length), .. typeAndData, .. BigEndian(Crc32(typeAndData))];
    }

    /// <summary>A chunk whose declared length is a lie — the walk must refuse rather than read past the
    /// buffer or trust the number.</summary>
    public static byte[] ChunkWithDeclaredLength(string type, ReadOnlySpan<byte> data, uint declaredLength)
    {
        var honest = Chunk(type, data);
        BigEndian(declaredLength).CopyTo(honest, 0);
        return honest;
    }

    public static byte[] TextChunk(string keyword, ReadOnlySpan<byte> value) =>
        Chunk("tEXt", [.. Encoding.ASCII.GetBytes(keyword), 0x00, .. value]);

    public static byte[] ExifChunk() =>
        Chunk("eXIf", [.. "MM\0*"u8, .. LocationSentinel, .. DeviceSentinel]);

    public static byte[] TimeChunk() => Chunk("tIME", [0x07, 0xEA, 0x08, 0x06, 0x0C, 0x1E, 0x00]);

    public static byte[] CompressedTextChunk() =>
        Chunk("zTXt", [.. "Comment"u8, 0x00, 0x00, .. DeviceSentinel]);

    public static byte[] InternationalTextChunk() =>
        Chunk("iTXt", [.. "Location"u8, 0x00, 0x00, 0x00, 0x00, 0x00, .. LocationSentinel]);

    public static byte[] ColourProfileChunk() => Chunk("gAMA", [0x00, 0x00, 0xB1, 0x8F]);

    private static readonly byte[] Ihdr = Chunk(
        "IHDR", [0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x08, 0x08, 0x02, 0x00, 0x00, 0x00]);

    private static readonly byte[] Idat = Chunk("IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01]);

    private static readonly byte[] Iend = Chunk("IEND", []);

    private static byte[] BigEndian(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var current in data)
        {
            crc ^= current;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
