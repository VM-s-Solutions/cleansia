using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// The avatar intake rule. The 10 MiB figure is not a preference — it is the promise the customer web
/// app already prints ("Use a square image up to 10 MB"), so it is written here as a literal rather
/// than read back off the production constant: a change to the constant alone must redden this file,
/// because it silently breaks that promise in one direction or the other.
///
/// The ordering tests are the load-bearing ones. The decodability rule materializes the whole payload,
/// so a size rule that runs after it has already paid the cost it exists to avoid.
/// </summary>
public class ImageFileValidatorTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;

    private readonly ImageFileValidator _validator = new();

    /// <summary>
    /// The FULL eight-byte PNG signature, not the four a reader recognises. The trailing
    /// <c>0D 0A 1A 0A</c> is what a real encoder writes and what the sniff requires, so a four-byte
    /// fixture would be an input no client produces — green here and refused in production.
    /// </summary>
    private static byte[] PngBytes(long size) => Headed([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], size);

    private static byte[] Headed(byte[] header, long size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// A RIFF container of the given four-character format — the shape a real WAV, AVI or WebP has:
    /// <c>RIFF</c>, a little-endian size, then the format tag at offset 8.
    /// </summary>
    private static byte[] Riff(string format, long size)
    {
        var bytes = new byte[size];
        "RIFF"u8.ToArray().CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)(size - 8)).CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes(format).CopyTo(bytes, 8);
        return bytes;
    }

    private static byte[] BytesMatchingNoImageSignature(long size) => new byte[size];

    private static BlobFileDto Upload(byte[] content) =>
        new(FileName: "avatar.png", Base64Content: Convert.ToBase64String(content), ContentType: "image/png");

    [Fact]
    public void Image_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var result = _validator.Validate(Upload(PngBytes(TenMebibytes + 1024)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    [Fact]
    public void Image_Under_TenMebibytes_Passes()
    {
        var result = _validator.Validate(Upload(PngBytes(TenMebibytes - 1024)));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Sized one byte under the limit on purpose: the browser clients send the payload with a 22-char
    /// <c>data:</c> prefix, so measuring the raw string instead of the extracted data pushes exactly
    /// this upload over. Any smaller image would pass either way and pin nothing.
    /// </summary>
    [Fact]
    public void DataUriPrefixed_Image_Is_Measured_On_The_Extracted_Data()
    {
        var atLimit = Convert.ToBase64String(PngBytes(TenMebibytes - 1));
        var dto = new BlobFileDto("avatar.png", $"data:image/png;base64,{atLimit}", "image/png");

        var result = _validator.Validate(dto);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DataUriPrefixed_Image_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var oversized = Convert.ToBase64String(PngBytes(TenMebibytes + 1024));
        var dto = new BlobFileDto("avatar.png", $"data:image/png;base64,{oversized}", "image/png");

        var result = _validator.Validate(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    [Fact]
    public void Payload_That_Is_Neither_An_Image_Nor_Within_The_Limit_Reports_Size_First()
    {
        var oversizedNonImage = Upload(BytesMatchingNoImageSignature(TenMebibytes + 1024));

        var result = _validator.Validate(oversizedNonImage);

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileSizeExceeded, failure.ErrorMessage);
    }

    /// <summary>
    /// The ordering proof that reading the rule chain cannot give you: an oversized payload must be
    /// refused without the decodability rule ever materializing it. That rule allocates the whole
    /// payload, so if it ran the delta would be ~10 MB rather than the few KB FluentValidation costs.
    ///
    /// <para>The payload is a genuine oversized PNG, not garbage. Garbage fails the SNIFF too — which is
    /// also cheap — so the size rule could be moved to the foot of the chain and this assertion would
    /// still hold: a fixture no single mutation can falsify. Only a payload the sniff ACCEPTS leaves the
    /// size bound as the one rule standing between the caller and the decode.</para>
    /// </summary>
    [Fact]
    public void Oversized_Payload_Is_Refused_Without_Being_Decoded()
    {
        var oversized = Upload(PngBytes(TenMebibytes + 1024));
        _validator.Validate(Upload(PngBytes(64)));

        var before = GC.GetAllocatedBytesForCurrentThread();
        _validator.Validate(oversized);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            allocated < 1_000_000,
            $"Rejecting an oversized upload allocated {allocated:N0} bytes — the payload was decoded before the size was checked.");
    }

    [Fact]
    public void Within_Limit_Payload_That_Is_Not_An_Image_Fails_With_ContentTypeMismatch()
    {
        var result = _validator.Validate(Upload(BytesMatchingNoImageSignature(1024)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }

    /// <summary>
    /// <c>RIFF</c> is a generic container, not a format: WAV and AVI open with the same four bytes a
    /// WebP does. Matching on it alone stored an audio file as <c>image/webp</c> — a type this system
    /// then pins onto a served <c>Content-Type</c> header. The format tag at offset 8 is the whole
    /// difference between the two cases below.
    /// </summary>
    [Theory]
    [InlineData("WAVE")]
    [InlineData("AVI ")]
    [InlineData("WEBQ")]
    public void A_Riff_Container_That_Is_Not_Webp_Is_Refused(string format)
    {
        var result = _validator.Validate(Upload(Riff(format, 2048)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }

    [Fact]
    public void A_Genuine_Webp_Passes()
    {
        var result = _validator.Validate(Upload(Riff("WEBP", 2048)));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// A WebP whose payload stops before the format tag: the head has room for the RIFF magic and the
    /// size field and nothing else, so the sniff cannot see the evidence and must refuse rather than
    /// assume. Twelve bytes is exactly the window, so eleven is the boundary.
    /// </summary>
    [Fact]
    public void A_Riff_Header_Truncated_Before_The_Format_Tag_Is_Refused()
    {
        var result = _validator.Validate(Upload(Riff("WEBP", 2048).Take(11).ToArray()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }

    /// <summary>
    /// BMP and TIFF were accepted by the old table and can never be SERVED: <c>ServedContentType</c>
    /// hands both back as <c>application/octet-stream</c> and no browser renders a TIFF in an
    /// <c>&lt;img&gt;</c>, so accepting one produced an upload that appeared to succeed and an avatar
    /// that never appeared. Nothing in any client offers either.
    /// </summary>
    [Fact]
    public void A_Bmp_Is_Refused()
    {
        var result = _validator.Validate(Upload(Headed("BM"u8.ToArray(), 2048)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }

    [Theory]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00 })]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })]
    public void A_Tiff_Is_Refused(byte[] magic)
    {
        var result = _validator.Validate(Upload(Headed(magic, 2048)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }

    /// <summary>
    /// Every content type the avatar's accepted set names, reached through a genuine payload of that
    /// format. A set member with no matching row in the signature table is unreachable — the intake would
    /// refuse a format it claims to accept — and nothing but this pairing would say so.
    /// </summary>
    public static TheoryData<string, byte[]> AcceptedFormats() => new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "image/gif", "GIF8"u8.ToArray() },
        { "image/webp", Riff("WEBP", 16) }
    };

    [Theory]
    [MemberData(nameof(AcceptedFormats))]
    public void Every_Format_The_Clients_Offer_Is_Accepted(string contentType, byte[] header)
    {
        var result = _validator.Validate(Upload(Headed(header, 2048)));

        Assert.True(result.IsValid, $"{contentType} was refused, but the avatar picker offers it.");
    }

    /// <summary>
    /// The sniff reads the head only, so a payload can begin with a real PNG signature and still be
    /// undecodable further in — which reaches <c>UpdateCurrentUser</c>'s <c>Convert.FromBase64String</c>
    /// as an unhandled <c>FormatException</c>, i.e. a 500 on a malformed avatar. Twelve bytes encode to
    /// exactly sixteen unpadded characters — the whole sniffed head — so the garbage lands strictly
    /// after it and only the full decode can see it.
    /// </summary>
    [Fact]
    public void Image_Header_With_Undecodable_Content_Is_Refused()
    {
        var headerThenGarbage = Convert.ToBase64String(PngBytes(12)) + "!!!!";

        var result = _validator.Validate(new BlobFileDto("avatar.png", headerThenGarbage, "image/png"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileNotMatchContentType);
    }
}
