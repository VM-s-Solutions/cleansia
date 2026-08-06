using Cleansia.Core.AppServices.Common.Media;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// The WebP half of ADR-0043 D2: drop the <c>EXIF</c> and <c>XMP </c> RIFF chunks, clear the
/// corresponding <c>VP8X</c> flag bits and fix the RIFF size field. The two bookkeeping fields are not
/// cosmetic — a file that still advertises metadata it no longer carries, under a size that no longer
/// describes it, is a file a decoder may refuse.
/// </summary>
public class WebPMetadataScrubTests
{
    [Fact]
    public void The_Metadata_Chunks_Are_Dropped_And_The_Image_Chunk_Is_Kept()
    {
        var file = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag | SyntheticWebP.XmpFlag | SyntheticWebP.AlphaFlag),
            SyntheticWebP.AlphaData(),
            SyntheticWebP.ImageData(),
            SyntheticWebP.ExifChunk(),
            SyntheticWebP.XmpChunk());

        var result = ImageMetadata.Scrub(file);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, SyntheticWebP.LocationSentinel));
        Assert.False(ByteSequence.Contains(result.Bytes, "EXIF"u8));
        Assert.False(ByteSequence.Contains(result.Bytes, "XMP "u8));
        Assert.True(ByteSequence.Contains(result.Bytes, SyntheticWebP.ImageData()));
        Assert.True(ByteSequence.Contains(result.Bytes, SyntheticWebP.AlphaData()));
    }

    /// <summary>
    /// The flags say what the file contains. Leaving the EXIF and XMP bits set after removing the chunks
    /// they point at is the container equivalent of a dangling pointer.
    /// </summary>
    [Fact]
    public void The_Vp8x_Flags_Stop_Claiming_Metadata_The_File_No_Longer_Carries()
    {
        var file = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag | SyntheticWebP.XmpFlag | SyntheticWebP.IccFlag),
            SyntheticWebP.ImageData(),
            SyntheticWebP.ExifChunk());

        var scrubbed = ImageMetadata.Scrub(file).Bytes;

        var flags = scrubbed[ByteSequence.IndexOf(scrubbed, "VP8X"u8) + 8];
        Assert.Equal(0, flags & (SyntheticWebP.ExifFlag | SyntheticWebP.XmpFlag));
        Assert.Equal(SyntheticWebP.IccFlag, (byte)(flags & SyntheticWebP.IccFlag));
    }

    [Fact]
    public void The_Riff_Size_Describes_What_Is_Left()
    {
        var file = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag),
            SyntheticWebP.ImageData(),
            SyntheticWebP.ExifChunk());

        var scrubbed = ImageMetadata.Scrub(file).Bytes;

        var declared = scrubbed[4] | (scrubbed[5] << 8) | (scrubbed[6] << 16) | (scrubbed[7] << 24);
        Assert.Equal(scrubbed.Length - 8, declared);
    }

    /// <summary>An odd-sized chunk is followed by a pad byte that belongs to the container and not to the
    /// chunk. Miscounting it walks the rest of the file at a one-byte offset.</summary>
    [Fact]
    public void An_Odd_Sized_Chunk_Is_Walked_Past_Its_Pad_Byte()
    {
        var file = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag),
            SyntheticWebP.Chunk("ICCP", [0x01, 0x02, 0x03]),
            SyntheticWebP.ExifChunk(),
            SyntheticWebP.ImageData());

        var result = ImageMetadata.Scrub(file);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, "EXIF"u8));
        Assert.True(ByteSequence.Contains(result.Bytes, SyntheticWebP.ImageData()));
    }

    [Fact]
    public void A_Simple_File_With_No_Metadata_Comes_Out_Byte_Identical()
    {
        var file = SyntheticWebP.File(SyntheticWebP.ImageData());

        var result = ImageMetadata.Scrub(file);

        Assert.True(result.Scrubbed);
        Assert.Equal(file, result.Bytes);
    }

    public static TheoryData<string, byte[]> ContainersTheWalkRefuses() => new()
    {
        {
            "a RIFF size past the end of the file",
            SyntheticWebP.FileWithDeclaredSize(60000, SyntheticWebP.ImageData())
        },
        {
            "a chunk size past the end of the file",
            SyntheticWebP.File(SyntheticWebP.ChunkWithDeclaredSize("VP8 ", [0x01, 0x02], 60000))
        },
        { "nothing after the WEBP tag", SyntheticWebP.File() },
        { "a truncated chunk header", SyntheticWebP.File(SyntheticWebP.ImageData())[..15] }
    };

    [Theory]
    [MemberData(nameof(ContainersTheWalkRefuses))]
    public void A_Malformed_Container_Is_Refused_Not_Repaired(string reason, byte[] malformed)
    {
        var result = ImageMetadata.Scrub(malformed);

        Assert.False(result.Scrubbed, $"{reason}: reported as scrubbed");
        Assert.Same(malformed, result.Bytes);
    }
}
