using Cleansia.Core.AppServices.Common.Media;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// The JPEG half of ADR-0043 D2: drop every <c>APP1</c> (EXIF and XMP ride the same marker) and every
/// <c>APP13</c> (IPTC), re-emit the rest byte-identically, and carry orientation across in a segment the
/// server synthesizes rather than one it edits.
///
/// <para>The output is asserted by searching the emitted bytes, not by parsing them: a reader written
/// for these tests would share the walker's assumptions and go green on the same mistake. "No
/// <c>FF E1</c> remains" and "these exact 36 bytes are present" hold independently of any parser, which
/// is what makes them worth writing.</para>
/// </summary>
public class JpegMetadataScrubTests
{
    /// <summary>
    /// The emitted segment, written out here rather than obtained from the code under test: a fixed
    /// one-entry big-endian IFD whose only variable is one value validated into 2–8. Nothing of the
    /// uploader's reaches it, which is the property that separates this design from rewriting the
    /// caller's own EXIF (ADR-0043 A4).
    /// </summary>
    private static byte[] ExpectedOrientationSegment(byte orientation) =>
    [
        0xFF, 0xE1, 0x00, 0x22,
        0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
        0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
        0x00, 0x01,
        0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    ];

    private static readonly byte[] App1Marker = [0xFF, 0xE1];
    private static readonly byte[] App13Marker = [0xFF, 0xED];

    [Fact]
    public void A_Photograph_Of_A_Customers_Home_Loses_Its_Coordinates_And_Its_Camera()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var scrubbed = ImageMetadata.Scrub(photo).Bytes;

        Assert.True(ByteSequence.Contains(photo, SyntheticJpeg.GpsCoordinateBytes()), "the fixture carries no GPS");
        Assert.False(ByteSequence.Contains(scrubbed, SyntheticJpeg.GpsCoordinateBytes()));
        Assert.False(ByteSequence.Contains(scrubbed, SyntheticJpeg.DeviceSentinel));
    }

    [Fact]
    public void The_Compressed_Image_Survives_Byte_For_Byte()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6), SyntheticJpeg.IptcSegment());

        var scrubbed = ImageMetadata.Scrub(photo).Bytes;

        Assert.True(ByteSequence.Contains(scrubbed, SyntheticJpeg.ImageBody));
        Assert.Equal([0xFF, 0xD8], scrubbed[..2]);
        Assert.Equal([0xFF, 0xD9], scrubbed[^2..]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void An_Orientation_That_Reads_Unambiguously_Into_2_To_8_Is_Carried_Across(byte orientation)
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation));

        var scrubbed = ImageMetadata.Scrub(photo).Bytes;

        Assert.True(
            ByteSequence.Contains(scrubbed, ExpectedOrientationSegment(orientation)),
            $"orientation {orientation} was not re-emitted, so every portrait photograph ships rotated");
        Assert.Equal(1, ByteSequence.Count(scrubbed, App1Marker));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Orientation_Is_Read_In_Both_Tiff_Byte_Orders(bool bigEndian)
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6, bigEndian: bigEndian));

        var scrubbed = ImageMetadata.Scrub(photo).Bytes;

        Assert.True(ByteSequence.Contains(scrubbed, ExpectedOrientationSegment(6)));
    }

    /// <summary>
    /// The whole degradation rule in one place. Orientation 1 is "no rotation", so emitting nothing says
    /// the same thing; everything else here is a source we could not read unambiguously, and ADR-0043
    /// D2.1 spends the rotation rather than guessing at it. A guessed value is indistinguishable from a
    /// read one once it is in the file.
    /// </summary>
    public static TheoryData<string, byte[]> SourcesThatEarnNoExif()
    {
        var canonical = SyntheticJpeg.ExifSegment(orientation: 6);

        return new TheoryData<string, byte[]>
        {
            { "orientation 1, the identity", SyntheticJpeg.ExifSegment(orientation: 1) },
            { "orientation 0, below the range", SyntheticJpeg.ExifSegment(orientation: 0) },
            { "orientation 9, above the range", SyntheticJpeg.ExifSegment(orientation: 9) },
            { "orientation 65535", SyntheticJpeg.ExifSegment(orientation: ushort.MaxValue) },
            { "no orientation tag at all", SyntheticJpeg.ExifSegment(orientation: null) },
            {
                "a byte-order mark that is neither II nor MM",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.ByteOrderOffset, 0x58, 0x58)
            },
            {
                "a TIFF magic that is not 42",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.MagicOffset, 0x00, 0x2B)
            },
            {
                "an IFD0 pointer past the end of the segment",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.Ifd0PointerOffset, 0xFF, 0xFF, 0xFF, 0xFF)
            },
            {
                "an IFD0 pointer inside the TIFF header",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.Ifd0PointerOffset, 0x00, 0x00, 0x00, 0x03)
            },
            {
                "an entry count the entry table cannot hold",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.Ifd0EntryCountOffset, 0xFF, 0xFF)
            },
            {
                "an orientation stored as LONG rather than SHORT",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.OrientationTypeOffset, 0x00, 0x04)
            },
            {
                "an orientation of count 2",
                SyntheticJpeg.Patched(canonical, SyntheticJpeg.OrientationCountOffset, 0x00, 0x00, 0x00, 0x02)
            },
            {
                "two orientation entries in one IFD",
                SyntheticJpeg.Patched(
                    canonical,
                    SyntheticJpeg.MakeEntryOffset,
                    0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x03, 0x00, 0x00)
            }
        };
    }

    [Theory]
    [MemberData(nameof(SourcesThatEarnNoExif))]
    public void A_Source_That_Does_Not_Read_Unambiguously_Emits_No_Exif_At_All(string reason, byte[] exifSegment)
    {
        var photo = SyntheticJpeg.Photo(exifSegment);

        var result = ImageMetadata.Scrub(photo);

        Assert.True(result.Scrubbed);
        Assert.False(
            ByteSequence.Contains(result.Bytes, App1Marker),
            $"{reason}: an unreadable orientation was repaired into a guess instead of dropped");
        Assert.False(ByteSequence.Contains(result.Bytes, SyntheticJpeg.DeviceSentinel));
        Assert.True(ByteSequence.Contains(result.Bytes, SyntheticJpeg.ImageBody));
    }

    /// <summary>
    /// Two EXIF segments disagreeing about orientation is the same failure as an unreadable one: there is
    /// no answer, and picking either is a guess.
    /// </summary>
    [Fact]
    public void Two_Disagreeing_Exif_Segments_Emit_No_Exif_At_All()
    {
        var photo = SyntheticJpeg.Photo(
            SyntheticJpeg.ExifSegment(orientation: 6),
            SyntheticJpeg.ExifSegment(orientation: 3, withGps: false));

        var result = ImageMetadata.Scrub(photo);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, App1Marker));
    }

    [Fact]
    public void Xmp_Rides_Its_Own_App1_And_Goes_With_It()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.XmpSegment());

        var result = ImageMetadata.Scrub(photo);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, SyntheticJpeg.XmpMarker));
        Assert.False(ByteSequence.Contains(result.Bytes, App1Marker));
    }

    [Fact]
    public void Iptc_Rides_App13_And_Goes_Too()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.IptcSegment());

        var result = ImageMetadata.Scrub(photo);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, SyntheticJpeg.IptcMarker));
        Assert.False(ByteSequence.Contains(result.Bytes, App13Marker));
        Assert.False(ByteSequence.Contains(result.Bytes, SyntheticJpeg.DeviceSentinel));
    }

    [Fact]
    public void A_Photograph_Carrying_No_Metadata_Comes_Out_Byte_Identical()
    {
        var photo = SyntheticJpeg.Photo();

        var result = ImageMetadata.Scrub(photo);

        Assert.True(result.Scrubbed);
        Assert.Equal(photo, result.Bytes);
    }

    /// <summary>
    /// Every way the container itself can be malformed. The walk never repairs one: it refuses, hands
    /// back the bytes it was given and reports them unscrubbed, because a partially rewritten JPEG is a
    /// destroyed photograph while an unscrubbed one is a disclosure we can still see in the report.
    /// </summary>
    public static TheoryData<string, byte[]> ContainersTheWalkRefuses()
    {
        var canonical = SyntheticJpeg.ExifSegment(orientation: 6);
        var photo = SyntheticJpeg.Photo(canonical);

        // The two garbage lengths carry NO payload, so a walk that "repaired" the length to the smallest
        // legal one would land on the next real marker and complete. With a payload behind them the file
        // is unwalkable either way, and the fixture stops telling refusal apart from repair.
        var emptyApp1 = SyntheticJpeg.Segment(SyntheticJpeg.App1, []);

        return new TheoryData<string, byte[]>
        {
            { "a declared segment length of 0", SyntheticJpeg.Photo(SyntheticJpeg.WithDeclaredLength(emptyApp1, 0)) },
            { "a declared segment length of 1", SyntheticJpeg.Photo(SyntheticJpeg.WithDeclaredLength(emptyApp1, 1)) },
            {
                "a declared segment length past the end of the file",
                SyntheticJpeg.Photo(SyntheticJpeg.WithDeclaredLength(canonical, 60000))
            },
            { "truncated inside a segment", photo[..(photo.Length / 2)] },
            { "nothing after the start-of-image marker", [0xFF, 0xD8, 0xFF] },
            { "a byte where a marker must start", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0x12, 0x34] }
        };
    }

    [Theory]
    [MemberData(nameof(ContainersTheWalkRefuses))]
    public void A_Malformed_Container_Is_Refused_Not_Repaired(string reason, byte[] malformed)
    {
        var result = ImageMetadata.Scrub(malformed);

        Assert.False(result.Scrubbed, $"{reason}: reported as scrubbed");
        Assert.Same(malformed, result.Bytes);
    }
}
