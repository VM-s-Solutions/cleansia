namespace Cleansia.Core.AppServices.Common.Media;

/// <summary>
/// Removes EXIF, XMP and IPTC from a JPEG by walking its segment structure and re-emitting everything
/// else byte for byte. Nothing here decodes: the entropy-coded scan is copied, never read, so the cost is
/// bounded by an input that is already bounded and no bitmap is ever allocated (ADR-0043 D2).
///
/// <para><b>Orientation is the one thing carried across</b>, and it is carried by SYNTHESIS, not by
/// editing: the source is read only far enough to answer "which of 2–8", and what is written is a fixed
/// segment built here. No byte the uploader chose reaches the output, which is what separates this from
/// rewriting their EXIF with corrected offsets — offset arithmetic over attacker-chosen values is where a
/// hand-rolled parser becomes the defect it was written to prevent.</para>
///
/// <para><b>The walk refuses rather than repairs.</b> A length that does not fit, a marker that is not
/// where one must be, a segment that runs off the end — each ends the walk with <c>null</c>, and the
/// caller stores the original bytes and reports them unscrubbed. A photograph that keeps its metadata is
/// a disclosure we can still see; a photograph this platform half-rewrote is destroyed.</para>
/// </summary>
internal static class JpegMetadata
{
    private const byte MarkerPrefix = 0xFF;
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;
    private const byte TemporaryMarker = 0x01;
    private const byte FirstRestartMarker = 0xD0;
    private const byte LastRestartMarker = 0xD7;

    /// <summary>EXIF and XMP both ride APP1; IPTC/Photoshop rides APP13.</summary>
    private const byte App1 = 0xE1;

    private const byte App13 = 0xED;

    private const ushort OrientationTag = 0x0112;
    private const ushort ShortType = 3;
    private const ushort TiffMagic = 42;

    public static bool Identifies(ReadOnlySpan<byte> content) =>
        content.Length >= 3
        && content[0] == MarkerPrefix
        && content[1] == StartOfImage
        && content[2] == MarkerPrefix;

    public static byte[]? WithoutMetadata(byte[] content)
    {
        var kept = new List<(int Start, int Length)>();
        var exifSegments = 0;
        ushort? orientation = null;
        var position = 2;

        while (true)
        {
            if (position + 1 >= content.Length || content[position] != MarkerPrefix)
            {
                return null;
            }

            var marker = content[position + 1];

            if (marker == MarkerPrefix)
            {
                position++;
                continue;
            }

            if (marker == StartOfScan || marker == EndOfImage)
            {
                // Past here the bytes are the compressed image (and, after EOI, whatever a camera
                // appended). They are copied without being parsed at all: a marker-shaped byte pair
                // inside entropy-coded data is data, and treating it as structure is how a stripper
                // corrupts a photograph.
                kept.Add((position, content.Length - position));
                break;
            }

            if (marker == TemporaryMarker || marker is >= FirstRestartMarker and <= LastRestartMarker)
            {
                kept.Add((position, 2));
                position += 2;
                continue;
            }

            if (position + 4 > content.Length)
            {
                return null;
            }

            var declaredLength = (content[position + 2] << 8) | content[position + 3];
            if (declaredLength < 2 || position + 2 + declaredLength > content.Length)
            {
                return null;
            }

            var payload = content.AsSpan(position + 4, declaredLength - 2);

            if (marker == App1 && IsExif(payload))
            {
                exifSegments++;
                orientation = exifSegments == 1 ? ReadOrientation(payload[6..]) : null;
            }
            else if (marker != App1 && marker != App13)
            {
                kept.Add((position, 2 + declaredLength));
            }

            position += 2 + declaredLength;
        }

        return Assemble(content, kept, orientation);
    }

    private static bool IsExif(ReadOnlySpan<byte> payload) =>
        payload.Length >= 6 && payload[..6].SequenceEqual("Exif\0\0"u8);

    /// <summary>
    /// Reads tag <c>0x0112</c> out of IFD0 and nothing else. Every path that cannot answer with certainty
    /// answers <c>null</c>: an unexpected byte order, a magic that is not 42, an IFD pointer or entry
    /// table that does not fit, a tag stored as anything but a single SHORT, a second copy of the tag, or
    /// a value outside 2–8 (1 is the identity, so it needs no segment either).
    /// </summary>
    private static ushort? ReadOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return null;
        }

        bool bigEndian;
        if (tiff[0] == 0x4D && tiff[1] == 0x4D)
        {
            bigEndian = true;
        }
        else if (tiff[0] == 0x49 && tiff[1] == 0x49)
        {
            bigEndian = false;
        }
        else
        {
            return null;
        }

        if (ReadUInt16(tiff[2..], bigEndian) != TiffMagic)
        {
            return null;
        }

        var directoryOffset = ReadUInt32(tiff[4..], bigEndian);
        if (directoryOffset < 8 || directoryOffset + 2L > tiff.Length)
        {
            return null;
        }

        var entries = ReadUInt16(tiff[(int)directoryOffset..], bigEndian);
        if (directoryOffset + 2L + (entries * 12L) > tiff.Length)
        {
            return null;
        }

        ushort? found = null;

        for (var index = 0; index < entries; index++)
        {
            var entry = tiff.Slice((int)directoryOffset + 2 + (index * 12), 12);

            if (ReadUInt16(entry, bigEndian) != OrientationTag)
            {
                continue;
            }

            if (found.HasValue
                || ReadUInt16(entry[2..], bigEndian) != ShortType
                || ReadUInt32(entry[4..], bigEndian) != 1)
            {
                return null;
            }

            found = ReadUInt16(entry[8..], bigEndian);
        }

        return found is >= 2 and <= 8 ? found : null;
    }

    private static byte[] Assemble(byte[] content, List<(int Start, int Length)> kept, ushort? orientation)
    {
        byte[] orientationSegment = orientation.HasValue ? OrientationSegment((byte)orientation.Value) : [];

        var total = 2 + orientationSegment.Length + kept.Sum(segment => segment.Length);
        var output = new byte[total];

        content.AsSpan(0, 2).CopyTo(output);
        var written = 2;

        orientationSegment.AsSpan().CopyTo(output.AsSpan(written));
        written += orientationSegment.Length;

        foreach (var (start, length) in kept)
        {
            content.AsSpan(start, length).CopyTo(output.AsSpan(written));
            written += length;
        }

        return output;
    }

    /// <summary>
    /// A complete EXIF APP1 carrying one big-endian IFD0 entry: orientation, SHORT, count 1, value inline.
    /// Every offset in it is a constant computed here, so there is nothing to get wrong per input.
    /// </summary>
    private static byte[] OrientationSegment(byte orientation) =>
    [
        MarkerPrefix, App1, 0x00, 0x22,
        0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
        0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
        0x00, 0x01,
        0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    ];

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, bool bigEndian) =>
        bigEndian
            ? (ushort)((source[0] << 8) | source[1])
            : (ushort)((source[1] << 8) | source[0]);

    private static uint ReadUInt32(ReadOnlySpan<byte> source, bool bigEndian) =>
        bigEndian
            ? ((uint)source[0] << 24) | ((uint)source[1] << 16) | ((uint)source[2] << 8) | source[3]
            : ((uint)source[3] << 24) | ((uint)source[2] << 16) | ((uint)source[1] << 8) | source[0];
}
