namespace Cleansia.Core.AppServices.Common.Media;

/// <summary>
/// Removes the <c>EXIF</c> and <c>XMP </c> chunks from a RIFF/WebP container. Two bookkeeping fields have
/// to move with them or the file describes something it no longer is: the <c>VP8X</c> flags advertise
/// which optional chunks exist, and the RIFF size covers everything after it (ADR-0043 D2).
///
/// <para>A simple <c>VP8 </c>/<c>VP8L</c> file has neither chunk and comes out byte-identical, which is
/// the shape both mobile clients already produce.</para>
/// </summary>
internal static class WebPMetadata
{
    private const int ChunkHeader = 8;
    private const byte ExifFlag = 0x08;
    private const byte XmpFlag = 0x04;

    public static bool Identifies(ReadOnlySpan<byte> content) =>
        content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8);

    public static byte[]? WithoutMetadata(byte[] content)
    {
        var declaredSize = ReadUInt32(content.AsSpan(4));
        var riffEnd = 8L + declaredSize;
        if (riffEnd > content.Length)
        {
            return null;
        }

        var output = new MemoryStream(content.Length);
        output.Write(content, 0, 12);

        var position = 12;
        var walked = 0;

        while (position < riffEnd)
        {
            if (position + ChunkHeader > riffEnd)
            {
                return null;
            }

            var chunkSize = ReadUInt32(content.AsSpan(position + 4));
            if (chunkSize > int.MaxValue - ChunkHeader - 1)
            {
                return null;
            }

            // An odd-sized chunk is followed by a pad byte that belongs to the container, not the chunk.
            var padded = (int)chunkSize + ((int)chunkSize & 1);
            if (position + (long)ChunkHeader + padded > riffEnd)
            {
                return null;
            }

            var fourCc = content.AsSpan(position, 4);
            walked++;

            if (fourCc.SequenceEqual("VP8X"u8) && chunkSize >= 1)
            {
                output.Write(content, position, ChunkHeader);
                output.WriteByte((byte)(content[position + ChunkHeader] & ~(ExifFlag | XmpFlag)));
                output.Write(content, position + ChunkHeader + 1, padded - 1);
            }
            else if (!fourCc.SequenceEqual("EXIF"u8) && !fourCc.SequenceEqual("XMP "u8))
            {
                output.Write(content, position, ChunkHeader + padded);
            }

            position += ChunkHeader + padded;
        }

        if (walked == 0)
        {
            return null;
        }

        var riffSize = (uint)(output.Length - 8);

        if (riffEnd < content.Length)
        {
            output.Write(content, (int)riffEnd, content.Length - (int)riffEnd);
        }

        var rewritten = output.ToArray();
        WriteUInt32(rewritten.AsSpan(4), riffSize);

        return rewritten;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> source) =>
        source[0] | ((uint)source[1] << 8) | ((uint)source[2] << 16) | ((uint)source[3] << 24);

    private static void WriteUInt32(Span<byte> destination, uint value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
    }
}
