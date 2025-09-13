using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public class ImageFileValidator : AbstractValidator<BlobFile>
{
    public ImageFileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(FileMatchesImageContentType)
            .WithErrorCode(nameof(BlobFile))
            .WithMessage(BusinessErrorMessage.FileNotMatchContentType);
    }

    private static bool FileMatchesImageContentType(BlobFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Base64Content))
        {
            return false;
        }

        var base64Data = file.Base64Content.ExtractBase64Data();

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