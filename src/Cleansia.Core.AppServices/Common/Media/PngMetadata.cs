namespace Cleansia.Core.AppServices.Common.Media;

/// <summary>
/// Removes the metadata chunks from a PNG. Every chunk is length-prefixed and carries its own CRC over
/// its own type and data, so dropping one recomputes nothing and rewrites nothing that stays — which is
/// why a container walk works here and a decoder is not needed (ADR-0043 D2).
///
/// <para>The drop list is closed and short: <c>eXIf</c>, <c>tEXt</c>, <c>iTXt</c>, <c>zTXt</c>,
/// <c>tIME</c>. Everything else is the image or the colour it is meant to render in — widening this list
/// by one is how a scrub starts destroying photographs instead of anonymizing them.</para>
/// </summary>
internal static class PngMetadata
{
    private const int ChunkOverhead = 12;

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool Identifies(ReadOnlySpan<byte> content) =>
        content.Length >= 8 && content[..8].SequenceEqual(Signature);

    public static byte[]? WithoutMetadata(byte[] content)
    {
        var kept = new List<(int Start, int Length)> { (0, 8) };
        var position = 8;
        var walked = 0;

        while (position < content.Length)
        {
            if (position + ChunkOverhead > content.Length)
            {
                return null;
            }

            var declaredLength = ReadUInt32(content.AsSpan(position));
            if (declaredLength > int.MaxValue - ChunkOverhead)
            {
                return null;
            }

            var total = ChunkOverhead + (int)declaredLength;
            if (position + (long)total > content.Length)
            {
                return null;
            }

            var type = content.AsSpan(position + 4, 4);

            // IHDR first is what makes this a PNG rather than eight familiar bytes in front of anything.
            if (walked == 0 && !type.SequenceEqual("IHDR"u8))
            {
                return null;
            }

            walked++;

            if (!IsMetadata(type))
            {
                kept.Add((position, total));
            }

            position += total;

            if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        if (walked == 0)
        {
            return null;
        }

        if (position < content.Length)
        {
            kept.Add((position, content.Length - position));
        }

        return Assemble(content, kept);
    }

    private static bool IsMetadata(ReadOnlySpan<byte> type) =>
        type.SequenceEqual("eXIf"u8)
        || type.SequenceEqual("tEXt"u8)
        || type.SequenceEqual("iTXt"u8)
        || type.SequenceEqual("zTXt"u8)
        || type.SequenceEqual("tIME"u8);

    private static byte[] Assemble(byte[] content, List<(int Start, int Length)> kept)
    {
        var output = new byte[kept.Sum(chunk => chunk.Length)];
        var written = 0;

        foreach (var (start, length) in kept)
        {
            content.AsSpan(start, length).CopyTo(output.AsSpan(written));
            written += length;
        }

        return output;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> source) =>
        ((uint)source[0] << 24) | ((uint)source[1] << 16) | ((uint)source[2] << 8) | source[3];
}
