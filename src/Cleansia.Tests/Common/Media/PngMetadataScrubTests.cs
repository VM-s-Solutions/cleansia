using Cleansia.Core.AppServices.Common.Media;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// The PNG half of ADR-0043 D2: drop <c>eXIf</c>, <c>tEXt</c>, <c>iTXt</c>, <c>zTXt</c> and <c>tIME</c>,
/// keep everything else. Chunks carry their own CRC, so removal recomputes nothing — and the chunks that
/// are NOT on that list are the image itself, so a walk that drops one is worse than one that drops none.
/// </summary>
public class PngMetadataScrubTests
{
    public static TheoryData<string, byte[]> MetadataChunks() => new()
    {
        { "eXIf", SyntheticPng.ExifChunk() },
        { "tEXt", SyntheticPng.TextChunk("Model", SyntheticPng.DeviceSentinel) },
        { "zTXt", SyntheticPng.CompressedTextChunk() },
        { "iTXt", SyntheticPng.InternationalTextChunk() },
        { "tIME", SyntheticPng.TimeChunk() }
    };

    [Theory]
    [MemberData(nameof(MetadataChunks))]
    public void A_Metadata_Chunk_Is_Dropped(string type, byte[] chunk)
    {
        var image = SyntheticPng.Image(chunk);

        var result = ImageMetadata.Scrub(image);

        Assert.True(result.Scrubbed);
        Assert.False(ByteSequence.Contains(result.Bytes, chunk), $"{type} survived the scrub");
    }

    [Fact]
    public void The_Coordinates_And_The_Camera_Go_With_Them()
    {
        var image = SyntheticPng.Image(
            SyntheticPng.ExifChunk(),
            SyntheticPng.TextChunk("Model", SyntheticPng.DeviceSentinel),
            SyntheticPng.InternationalTextChunk());

        var scrubbed = ImageMetadata.Scrub(image).Bytes;

        Assert.False(ByteSequence.Contains(scrubbed, SyntheticPng.LocationSentinel));
        Assert.False(ByteSequence.Contains(scrubbed, SyntheticPng.DeviceSentinel));
    }

    /// <summary>
    /// The other direction, and the one that destroys images rather than leaking them: a walk that
    /// widened its drop list by one critical chunk would pass every assertion above.
    /// </summary>
    [Fact]
    public void The_Critical_Chunks_And_The_Colour_Chunks_Are_Kept()
    {
        var image = SyntheticPng.Image(SyntheticPng.ColourProfileChunk(), SyntheticPng.ExifChunk());

        var scrubbed = ImageMetadata.Scrub(image).Bytes;

        Assert.Equal(SyntheticPng.Signature, scrubbed[..8]);
        Assert.True(ByteSequence.Contains(scrubbed, "IHDR"u8));
        Assert.True(ByteSequence.Contains(scrubbed, "IDAT"u8));
        Assert.True(ByteSequence.Contains(scrubbed, "IEND"u8));
        Assert.True(ByteSequence.Contains(scrubbed, SyntheticPng.ColourProfileChunk()));
    }

    [Fact]
    public void An_Image_Carrying_No_Metadata_Comes_Out_Byte_Identical()
    {
        var image = SyntheticPng.Image();

        var result = ImageMetadata.Scrub(image);

        Assert.True(result.Scrubbed);
        Assert.Equal(image, result.Bytes);
    }

    public static TheoryData<string, byte[]> ContainersTheWalkRefuses() => new()
    {
        {
            "a chunk length past the end of the file",
            SyntheticPng.Image(SyntheticPng.ChunkWithDeclaredLength("tEXt", "Model"u8, 60000))
        },
        {
            "a chunk length with the high bit set",
            SyntheticPng.Image(SyntheticPng.ChunkWithDeclaredLength("tEXt", "Model"u8, 0x80000000))
        },
        { "a truncated chunk header", SyntheticPng.Image()[..12] },
        { "nothing after the signature", SyntheticPng.Signature },
        {
            "a chunk stream that does not open with IHDR",
            [.. SyntheticPng.Signature, .. SyntheticPng.TextChunk("Model", SyntheticPng.DeviceSentinel)]
        }
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
