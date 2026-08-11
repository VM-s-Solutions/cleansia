using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

/// <summary>
/// The avatar intake rule — the image sibling of <see cref="DocumentFileValidator"/> over the same DTO,
/// and the same ordered chain for the same reasons: the size bound first because every rule after it
/// touches the bytes, then the head sniff, then decodability, which is the only rule that has to
/// materialize the whole payload.
///
/// <para>The decodability rule is not optional here. The sniff reads the head, so a payload can begin
/// with a real PNG signature and still be undecodable further in — which reaches
/// <c>UpdateCurrentUser</c>'s <c>Convert.FromBase64String</c> as an unhandled <c>FormatException</c>,
/// i.e. a 500 on a malformed avatar.</para>
/// </summary>
public class ImageFileValidator : AbstractValidator<BlobFileDto>
{
    public ImageFileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(BlobFileSize.HasContentWithinLimit)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileSizeExceeded)
            .Must(file => SniffedContentType.FromContent(file.Base64Content, UploadIntake.Avatar) is not null)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileNotMatchContentType)
            .Must(HasDecodableContent)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileNotMatchContentType);
    }

    private static bool HasDecodableContent(BlobFileDto fileDto)
    {
        var base64Data = fileDto.Base64Content!.ExtractBase64Data();
        var buffer = new byte[base64Data.Length * 3 / 4];

        return Convert.TryFromBase64String(base64Data, buffer, out _);
    }
}
