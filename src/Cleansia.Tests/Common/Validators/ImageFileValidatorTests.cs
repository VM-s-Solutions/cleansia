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
/// The ordering tests are the load-bearing ones. The signature rule decodes the payload into two
/// full-size arrays, so a size rule that runs after it has already paid the cost it exists to avoid.
/// </summary>
public class ImageFileValidatorTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;

    private readonly ImageFileValidator _validator = new();

    private static byte[] PngBytes(long size)
    {
        var bytes = new byte[size];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
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
    /// refused without the signature rule ever decoding it. Decoding allocates the payload twice, so
    /// if this rule ran the delta would be ~21 MB rather than the few KB FluentValidation itself costs.
    /// </summary>
    [Fact]
    public void Oversized_Payload_Is_Refused_Without_Being_Decoded()
    {
        var oversized = Upload(BytesMatchingNoImageSignature(TenMebibytes + 1024));
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
}
