namespace Cleansia.Tests.Common.Media;

/// <summary>
/// A synthetic JPEG corpus. Real photographs are not committed: the branch under test is the malformed
/// one — truncated segments, garbage lengths, both TIFF byte orders, an orientation outside 2–8 — and a
/// camera will not produce any of it. Every fixture here is assembled from marker syntax, so a case can
/// be named after the exact defect it carries.
///
/// <para>No byte of any fixture is <c>0xFF</c> except a marker prefix, which is what lets a test assert
/// "no APP1 survives" by searching for <c>FF E1</c> rather than by parsing the output.</para>
/// </summary>
internal static class SyntheticJpeg
{
    public const byte App1 = 0xE1;
    public const byte App13 = 0xED;

    /// <summary>Offsets INTO THE TIFF BLOCK of an <see cref="ExifSegment"/> built in its canonical shape
    /// (Make, Orientation, GPS pointer), for the patch-one-field malformed fixtures.</summary>
    public const int ByteOrderOffset = 0;

    public const int MagicOffset = 2;
    public const int Ifd0PointerOffset = 4;
    public const int Ifd0EntryCountOffset = 8;
    public const int MakeEntryOffset = 10;
    public const int OrientationEntryOffset = 22;
    public const int OrientationTypeOffset = OrientationEntryOffset + 2;
    public const int OrientationCountOffset = OrientationEntryOffset + 4;

    private const int TiffOffsetInSegment = 4 + 6;

    /// <summary>The camera model — a stable cross-order correlation key, which is the disclosure the
    /// scrub exists to stop, and an ASCII run no rewritten container has any reason to carry.</summary>
    public static readonly byte[] DeviceSentinel = "SentinelCam"u8.ToArray();

    public static readonly byte[] XmpMarker = "http://ns.adobe.com/xap/1.0/"u8.ToArray();

    public static readonly byte[] IptcMarker = "Photoshop 3.0"u8.ToArray();

    private static readonly byte[] Soi = [0xFF, 0xD8];

    private static readonly byte[] Jfif = Segment(
        0xE0, [.. "JFIF\0"u8, 0x01, 0x02, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

    private static readonly byte[] Dqt = Segment(0xDB, [0x00, .. Enumerable.Repeat((byte)0x10, 64)]);

    private static readonly byte[] Sof0 = Segment(
        0xC0, [0x08, 0x00, 0x08, 0x00, 0x08, 0x01, 0x01, 0x11, 0x00]);

    private static readonly byte[] Dht = Segment(
        0xC4, [0x00, .. Enumerable.Repeat((byte)0x01, 16), .. Enumerable.Repeat((byte)0x02, 12)]);

    /// <summary>The quantization tables, the frame header, the Huffman tables and the entropy-coded scan
    /// — everything a decoder needs. The scrub must re-emit it byte-identically: rewriting one byte of it
    /// is the corruption the "never repair" rule refuses, and asserting it survives intact is how a test
    /// says "this still decodes" without a decoder.</summary>
    public static readonly byte[] ImageBody =
    [
        .. Dqt,
        .. Sof0,
        .. Dht,
        .. Segment(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]),
        .. Enumerable.Repeat((byte)0x11, 24),
        0xFF, 0xD9
    ];

    public static byte[] Photo(params byte[][] metadataSegments) =>
        [.. Soi, .. Jfif, .. metadataSegments.SelectMany(segment => segment), .. ImageBody];

    public static byte[] Segment(byte marker, ReadOnlySpan<byte> payload)
    {
        var declaredLength = payload.Length + 2;
        var segment = new byte[payload.Length + 4];

        segment[0] = 0xFF;
        segment[1] = marker;
        segment[2] = (byte)(declaredLength >> 8);
        segment[3] = (byte)declaredLength;
        payload.CopyTo(segment.AsSpan(4));

        return segment;
    }

    public static byte[] ExifSegment(ushort? orientation, bool withGps = true, bool bigEndian = true) =>
        Segment(App1, [.. "Exif\0\0"u8, .. Tiff(orientation, withGps, bigEndian)]);

    public static byte[] XmpSegment() =>
        Segment(App1, [.. XmpMarker, 0x00, .. "<x:xmpmeta><gps>50.0755,14.4378</gps></x:xmpmeta>"u8]);

    public static byte[] IptcSegment() =>
        Segment(App13, [.. IptcMarker, 0x00, .. "8BIM"u8, .. DeviceSentinel]);

    /// <summary>Replaces bytes inside an EXIF segment's TIFF block, so a malformed fixture differs from
    /// the well-formed one in exactly the field it is named after.</summary>
    public static byte[] Patched(byte[] exifSegment, int tiffOffset, params byte[] replacement)
    {
        var patched = (byte[])exifSegment.Clone();
        replacement.CopyTo(patched, TiffOffsetInSegment + tiffOffset);
        return patched;
    }

    /// <summary>Rewrites a segment's declared length without changing what follows it.</summary>
    public static byte[] WithDeclaredLength(byte[] segment, ushort declaredLength)
    {
        var rewritten = (byte[])segment.Clone();
        rewritten[2] = (byte)(declaredLength >> 8);
        rewritten[3] = (byte)declaredLength;
        return rewritten;
    }

    public static byte[] GpsCoordinateBytes(bool bigEndian = true)
    {
        var writer = new TiffWriter(bigEndian);
        foreach (var component in GpsRationals)
        {
            writer.UInt32(component);
        }

        return writer.ToArray();
    }

    private static readonly uint[] GpsRationals = [50, 1, 5, 1, 123456, 1000];

    private static byte[] Tiff(ushort? orientation, bool withGps, bool bigEndian)
    {
        var entryCount = 1 + (orientation.HasValue ? 1 : 0) + (withGps ? 1 : 0);
        var ifd0End = 8 + 2 + (12 * entryCount) + 4;
        var gpsIfdOffset = ifd0End;
        var heapOffset = withGps ? gpsIfdOffset + 30 : ifd0End;
        var makeOffset = heapOffset;
        var gpsValuesOffset = makeOffset + DeviceSentinel.Length + 1;

        var writer = new TiffWriter(bigEndian);
        writer.Raw(bigEndian ? "MM"u8 : "II"u8);
        writer.UInt16(42);
        writer.UInt32(8);

        writer.UInt16((ushort)entryCount);
        writer.Entry(0x010F, 2, (uint)DeviceSentinel.Length + 1, (uint)makeOffset);
        if (orientation.HasValue)
        {
            writer.ShortEntry(0x0112, orientation.Value);
        }

        if (withGps)
        {
            writer.Entry(0x8825, 4, 1, (uint)gpsIfdOffset);
        }

        writer.UInt32(0);

        if (withGps)
        {
            writer.UInt16(2);
            writer.AsciiEntry(0x0001, "N\0"u8);
            writer.Entry(0x0002, 5, 3, (uint)gpsValuesOffset);
            writer.UInt32(0);
        }

        writer.Raw(DeviceSentinel);
        writer.Raw([0x00]);

        if (withGps)
        {
            foreach (var component in GpsRationals)
            {
                writer.UInt32(component);
            }
        }

        return writer.ToArray();
    }

    private sealed class TiffWriter(bool bigEndian)
    {
        private readonly List<byte> _bytes = [];

        public void Raw(ReadOnlySpan<byte> value) => _bytes.AddRange(value);

        public void UInt16(ushort value) =>
            _bytes.AddRange(bigEndian ? [(byte)(value >> 8), (byte)value] : [(byte)value, (byte)(value >> 8)]);

        public void UInt32(uint value) =>
            _bytes.AddRange(bigEndian
                ? [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]
                : [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)]);

        public void Entry(ushort tag, ushort type, uint count, uint value)
        {
            UInt16(tag);
            UInt16(type);
            UInt32(count);
            UInt32(value);
        }

        /// <summary>An inline ASCII value occupies the value field verbatim, in neither byte order.
        /// </summary>
        public void AsciiEntry(ushort tag, ReadOnlySpan<byte> inlineValue)
        {
            UInt16(tag);
            UInt16(2);
            UInt32((uint)inlineValue.Length);
            Span<byte> valueField = stackalloc byte[4];
            inlineValue.CopyTo(valueField);
            Raw(valueField);
        }

        /// <summary>A SHORT of count 1 sits left-justified in the 4-byte value field, which is the one
        /// piece of TIFF layout an orientation reader has to get right in both byte orders.</summary>
        public void ShortEntry(ushort tag, ushort value)
        {
            UInt16(tag);
            UInt16(3);
            UInt32(1);
            UInt16(value);
            UInt16(0);
        }

        public byte[] ToArray() => [.. _bytes];
    }
}
