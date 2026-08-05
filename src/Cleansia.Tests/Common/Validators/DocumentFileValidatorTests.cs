using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// The document intake rule, the non-image sibling of <see cref="ImageFileValidatorTests"/>.
///
/// <para>The 10 MiB figure is written here as a literal rather than read off the production constant,
/// for the same reason the avatar tests do: it is the promise all three clients already print (web
/// <c>maxSizeInMB: 10</c>, iOS <c>DocumentPresentation.maxDocumentBytes</c>, and the translated
/// <c>file.size_exceeded_10mb</c> string), so a change to the constant alone must redden this file.</para>
///
/// <para>The accepted-format theory is the one that stops the server becoming quietly stricter than the
/// UI: the web picker offers <c>.pdf .doc .docx .jpg .jpeg .png</c> and the five-locale
/// <c>file.type_not_allowed</c> string names the same set.</para>
/// </summary>
public class DocumentFileValidatorTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;

    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Ole2CompoundFile = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipContainer = [0x50, 0x4B, 0x03, 0x04];

    private readonly DocumentFileValidator _validator = new();

    public static TheoryData<string, byte[]> AcceptedFormats() => new()
    {
        { ".pdf", Pdf },
        { ".jpg", Jpeg },
        { ".png", Png },
        { ".doc", Ole2CompoundFile },
        { ".docx", ZipContainer }
    };

    private static byte[] Headed(byte[] header, long size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] MatchingNoDocumentSignature(long size) => Headed("<html>"u8.ToArray(), size);

    private static BlobFileDto Upload(byte[] content, string? declaredContentType = "application/pdf") =>
        new(FileName: "contract.pdf", Base64Content: Convert.ToBase64String(content), ContentType: declaredContentType);

    [Theory]
    [MemberData(nameof(AcceptedFormats))]
    public void Every_Format_The_Clients_Offer_Is_Accepted(string extension, byte[] header)
    {
        var result = _validator.Validate(Upload(Headed(header, 2048)));

        Assert.True(result.IsValid, $"{extension} was refused, but the upload UI offers it.");
    }

    [Fact]
    public void Document_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var result = _validator.Validate(Upload(Headed(Pdf, TenMebibytes + 1024)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    /// <summary>
    /// The ordering proof that reading the chain cannot give you (AC1). The decodability rule
    /// materializes the whole payload, so a size bound placed after it answers correctly having already
    /// paid the cost it exists to avoid — measured at 20,979,352 bytes on the unfixed avatar validator.
    /// The signature rule between them reads nine bytes and is invisible at this scale.
    /// </summary>
    [Fact]
    public void Oversized_Payload_Is_Refused_Without_Being_Decoded()
    {
        var oversized = Upload(Headed(Pdf, TenMebibytes + 1024));
        _validator.Validate(Upload(Headed(Pdf, 64)));

        var before = GC.GetAllocatedBytesForCurrentThread();
        _validator.Validate(oversized);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            allocated < 1_000_000,
            $"Rejecting an oversized upload allocated {allocated:N0} bytes — the payload was decoded before the size was checked.");
    }

    [Fact]
    public void Payload_That_Is_Not_A_Permitted_Document_Fails_With_FileTypeNotAllowed()
    {
        var result = _validator.Validate(Upload(MatchingNoDocumentSignature(2048)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileTypeNotAllowed);
    }

    /// <summary>
    /// The signature rule reads the head only, so this payload passes it and is caught by nothing else
    /// if the decodability rule goes: the handler's <c>Convert.FromBase64String</c> then throws, which
    /// the client sees as a 500 rather than a rejected upload.
    /// </summary>
    [Fact]
    public void Document_Header_With_Undecodable_Content_Fails_With_InvalidFileType()
    {
        // Nine bytes encode to exactly twelve unpadded characters — the whole of the sniffed head — so
        // the garbage lands strictly after it and only the full decode can see it.
        var headerThenGarbage = Convert.ToBase64String(Headed(Pdf, 9)) + "!!!!";

        var result = _validator.Validate(new BlobFileDto("contract.pdf", headerThenGarbage, "application/pdf"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidFileType);
    }

    /// <summary>
    /// The web client sends a full <c>data:</c> URI while both mobile clients send bare base64, so the
    /// prefix has to be stripped before the head is read — otherwise every browser upload sniffs as
    /// <c>data:applic</c> and the whole partner web surface is refused.
    /// </summary>
    [Fact]
    public void DataUriPrefixed_Document_Is_Sniffed_On_The_Extracted_Data()
    {
        var pdf = Convert.ToBase64String(Headed(Pdf, 2048));

        var result = _validator.Validate(new BlobFileDto("contract.pdf", $"data:application/pdf;base64,{pdf}", "application/pdf"));

        Assert.True(result.IsValid);
    }
}
