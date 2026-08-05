using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// Characterization of the document intake rule, pinned so that folding its size check onto the seam
/// the avatar rule now shares stays behavior-preserving. Blank content failing the SIZE rule rather
/// than the type rule is part of the pinned contract, not an accident to tidy: it is the error code
/// UpdateEmployee returns today for the empty-file placeholder its clients send.
/// </summary>
public class FileValidatorTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;

    private readonly FileValidator _validator = new();

    private static BlobFileDto Document(long size, string contentType = "application/pdf") =>
        new(FileName: "contract.pdf", Base64Content: Convert.ToBase64String(new byte[size]), ContentType: contentType);

    [Fact]
    public void Document_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var result = _validator.Validate(Document(TenMebibytes + 1024));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    [Fact]
    public void Document_Under_TenMebibytes_Passes()
    {
        var result = _validator.Validate(Document(TenMebibytes - 1024));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Disallowed_ContentType_Fails_With_InvalidFileType()
    {
        var result = _validator.Validate(Document(1024, contentType: "application/x-msdownload"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidFileType);
    }

    [Fact]
    public void Blank_Content_Fails_With_FileSizeExceeded()
    {
        var result = _validator.Validate(new BlobFileDto("contract.pdf", string.Empty, "application/pdf"));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileSizeExceeded, failure.ErrorMessage);
    }

    [Fact]
    public void Oversized_Document_Reports_Size_Before_Type()
    {
        var result = _validator.Validate(Document(TenMebibytes + 1024, contentType: "application/x-msdownload"));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileSizeExceeded, failure.ErrorMessage);
    }
}
