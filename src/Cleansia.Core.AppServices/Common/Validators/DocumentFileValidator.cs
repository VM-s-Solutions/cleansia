using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

/// <summary>
/// The document intake rule — the sibling of <see cref="ImageFileValidator"/> over the same DTO, for
/// the paths that legitimately accept a PDF or an Office file and so cannot use an image sniff.
///
/// <para>ONE ordered chain, and the order is a cost decision as much as a message one. The size bound
/// runs first because every rule after it touches the bytes, and a bound placed after a decoding rule
/// returns the right answer having already paid the entire cost it exists to avoid. The signature sniff
/// runs before the decodability rule for the same reason one step down: it reads twelve bytes, while
/// decodability has to materialize the whole payload, so the payloads that were never documents are
/// refused without being decoded at all.</para>
/// </summary>
public class DocumentFileValidator : AbstractValidator<BlobFileDto>
{
    public DocumentFileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(file => !string.IsNullOrWhiteSpace(file.Base64Content))
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.Required)
            .Must(BlobFileSize.HasContentWithinLimit)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileSizeExceeded)
            .Must(file => SniffedContentType.FromContent(file.Base64Content, UploadIntake.EmployeeDocument) is not null)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileTypeNotAllowed)
            .Must(HasDecodableContent)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.InvalidFileType);
    }

    /// <summary>
    /// The signature rule reads the head only, so a payload can begin with a real signature and still be
    /// undecodable further in — which reaches the handler's <c>Convert.FromBase64String</c> as an
    /// unhandled <c>FormatException</c>, i.e. a 500 on a malformed upload.
    /// </summary>
    private static bool HasDecodableContent(BlobFileDto fileDto)
    {
        var base64Data = fileDto.Base64Content!.ExtractBase64Data();
        var buffer = new byte[base64Data.Length * 3 / 4];

        return Convert.TryFromBase64String(base64Data, buffer, out _);
    }
}
