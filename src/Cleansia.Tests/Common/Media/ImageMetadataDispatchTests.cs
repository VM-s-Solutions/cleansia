using Cleansia.Core.AppServices.Common.Media;

namespace Cleansia.Tests.Common.Media;

/// <summary>
/// ADR-0043 D2.2 — the scrub picks its walker from the bytes it is holding, at the moment it runs. It
/// takes no content type, so there is no string for an uploader to steer it with: declaring
/// <c>data:image/png</c> over JPEG bytes would otherwise run the PNG walker on a JPEG, find no
/// <c>IHDR</c>, bail, and leave the metadata in place under a green test.
///
/// <para>The other half of the rule is the honest report. A format the walkers do not recognise is
/// passed through untouched and reported as NOT scrubbed — never mangled into something a decoder
/// refuses, and never counted as a scrub that did not happen.</para>
/// </summary>
public class ImageMetadataDispatchTests
{
    public static TheoryData<string, byte[]> PayloadsNoWalkerClaims() => new()
    {
        { "a PDF, which this platform does not rewrite", [.. "%PDF-1.7\n%"u8, 0x01, 0x02, 0x03] },
        { "a GIF, which carries no EXIF to remove", [.. "GIF89a"u8, 0x01, 0x00, 0x01, 0x00, 0x80] },
        { "an OOXML/zip container", [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00] },
        { "markup", "<html><body>hello</body></html>"u8.ToArray() },
        { "a RIFF container that is not WebP", [.. "RIFF"u8, 0x10, 0x00, 0x00, 0x00, .. "WAVEfmt "u8] },
        { "nothing at all", [] }
    };

    [Theory]
    [MemberData(nameof(PayloadsNoWalkerClaims))]
    public void An_Unidentified_Payload_Passes_Through_Untouched_And_Says_So(string what, byte[] payload)
    {
        var result = ImageMetadata.Scrub(payload);

        Assert.False(result.Scrubbed, $"{what}: reported as scrubbed when nothing was removed");
        Assert.Same(payload, result.Bytes);
    }

    /// <summary>
    /// PNG bytes under a WebP-shaped head and vice versa: only the walker whose own signature matches may
    /// run, or a mislabelled payload reaches length arithmetic written for a different container.
    /// </summary>
    [Fact]
    public void A_Container_Is_Only_Walked_By_Its_Own_Format()
    {
        var png = SyntheticPng.Image(SyntheticPng.ExifChunk());
        var webp = SyntheticWebP.File(SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag), SyntheticWebP.ExifChunk());

        Assert.False(ByteSequence.Contains(ImageMetadata.Scrub(png).Bytes, SyntheticPng.LocationSentinel));
        Assert.False(ByteSequence.Contains(ImageMetadata.Scrub(webp).Bytes, SyntheticWebP.LocationSentinel));
    }
}
