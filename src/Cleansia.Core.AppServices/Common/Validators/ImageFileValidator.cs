using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public class ImageFileValidator : AbstractValidator<BlobFileDto>
{
    public ImageFileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(FileMatchesImageContentType)
            .WithErrorCode(nameof(BlobFileDto))
            .WithMessage(BusinessErrorMessage.FileNotMatchContentType);
    }

    private static bool FileMatchesImageContentType(BlobFileDto fileDto)
    {
        if (string.IsNullOrWhiteSpace(fileDto.Base64Content))
        {
            return false;
        }

        var base64Data = fileDto.Base64Content.ExtractBase64Data();

        var buffer = new byte[base64Data.Length * 3 / 4];
        if (!Convert.TryFromBase64String(base64Data, buffer, out var bytesWritten))
        {
            return false;
        }

        var imageData = new byte[bytesWritten];
        Array.Copy(buffer, imageData, bytesWritten);

        var validImage = Constants.ImageSignatures.Any(signature =>
            imageData.Length >= signature.Signature.Length &&
            imageData.Take(signature.Signature.Length).SequenceEqual(signature.Signature));

        return validImage;
    }
}