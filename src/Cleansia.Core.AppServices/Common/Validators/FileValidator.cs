using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public class FileValidator : AbstractValidator<BlobFileDto>
{
    private const int MaxFileSizeInMB = 10;
    private const long MaxFileSizeInBytes = MaxFileSizeInMB * 1024 * 1024;

    private static readonly string[] AllowedFileTypes =
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    public FileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(HaveValidSize)
            .WithMessage(BusinessErrorMessage.FileSizeExceeded)
            .Must(HaveValidFileType)
            .WithMessage(BusinessErrorMessage.InvalidFileType);
    }

    private static bool HaveValidSize(BlobFileDto fileDto)
    {
        if (string.IsNullOrWhiteSpace(fileDto.Base64Content))
        {
            return false;
        }

        var base64Data = fileDto.Base64Content.ExtractBase64Data();
        var fileSizeInBytes = (base64Data.Length * 3L) / 4L;

        return fileSizeInBytes <= MaxFileSizeInBytes;
    }

    private static bool HaveValidFileType(BlobFileDto fileDto)
    {
        if (string.IsNullOrWhiteSpace(fileDto.ContentType))
        {
            return false;
        }

        return AllowedFileTypes.Contains(fileDto.ContentType.ToLowerInvariant());
    }
}