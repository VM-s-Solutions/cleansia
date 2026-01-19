using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class UploadNewDocumentVersion
{
    public class Command : ICommand<Response>
    {
        public string PreviousDocumentId { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string FilePath { get; init; } = default!;
        public string ContentType { get; init; } = default!;
        public long FileSizeBytes { get; init; }
        public string? Description { get; init; }
    }

    public class Response
    {
        public string DocumentId { get; init; } = default!;
        public EmployeeDocumentItem Document { get; init; } = default!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PreviousDocumentId)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.FilePath)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.ContentType)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.FileSizeBytes)
                .GreaterThan(0).WithMessage(BusinessErrorMessage.Required)
                .LessThanOrEqualTo(10 * 1024 * 1024) // 10 MB
                .WithMessage("File size must not exceed 10 MB");

            RuleFor(x => x.ContentType)
                .Must(contentType => IsAllowedContentType(contentType))
                .WithMessage("File type not allowed. Allowed types: PDF, Images (JPEG, PNG), Word documents");

            When(x => !string.IsNullOrEmpty(x.Description), () =>
            {
                RuleFor(x => x.Description)
                    .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }

        private static bool IsAllowedContentType(string contentType)
        {
            var allowedTypes = new[]
            {
                "application/pdf",
                "image/jpeg",
                "image/jpg",
                "image/png",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };
            return allowedTypes.Contains(contentType.ToLower());
        }
    }

    public class Handler(
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(userEmail!, cancellationToken);

            if (user is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication",
                    BusinessErrorMessage.UserNotFound));
            }

            var previousDocument = await documentRepository.GetByIdAsync(request.PreviousDocumentId, cancellationToken);
            if (previousDocument is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.PreviousDocumentId),
                    BusinessErrorMessage.NotFound));
            }

            var newVersion = EmployeeDocument.CreateNewVersion(
                previousVersion: previousDocument,
                fileName: request.FileName,
                filePath: request.FilePath,
                contentType: request.ContentType,
                fileSizeBytes: request.FileSizeBytes,
                description: request.Description,
                createdBy: user.Id
            );

            documentRepository.Add(newVersion);

            var documentItem = newVersion.MapToDto();

            return BusinessResult.Success(new Response
            {
                DocumentId = newVersion.Id,
                Document = documentItem
            });
        }
    }
}
