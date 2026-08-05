using Cleansia.Core.AppServices.Shared.DTOs.Files;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public class FileValidator : AbstractValidator<BlobFileDto>
{
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
            .Must(BlobFileSize.HasContentWithinLimit)
            .WithMessage(BusinessErrorMessage.FileSizeExceeded)
            .Must(HaveValidFileType)
            .WithMessage(BusinessErrorMessage.InvalidFileType);
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
