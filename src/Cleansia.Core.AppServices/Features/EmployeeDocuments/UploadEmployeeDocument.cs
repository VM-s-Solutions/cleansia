using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class UploadEmployeeDocument
{
    public class Command : ICommand<Response>
    {
        public string EmployeeId { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string FilePath { get; init; } = default!;
        public string ContentType { get; init; } = default!;
        public long FileSizeBytes { get; init; }
        public DocumentType DocumentType { get; init; }
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
            RuleFor(x => x.EmployeeId)
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
                .WithMessage(BusinessErrorMessage.FileSizeExceeded10MB);

            RuleFor(x => x.ContentType)
                .Must(contentType => IsAllowedContentType(contentType))
                .WithMessage(BusinessErrorMessage.FileTypeNotAllowed);

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
        IEmployeeRepository employeeRepository,
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

            var employee = await employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.EmployeeId),
                    BusinessErrorMessage.NotFound));
            }

            var existingDocument = await documentRepository.GetLatestByFileNameAsync(
                request.EmployeeId,
                request.FileName,
                cancellationToken);

            EmployeeDocument document;
            if (existingDocument is not null)
            {
                document = EmployeeDocument.CreateNewVersion(
                    previousVersion: existingDocument,
                    fileName: request.FileName,
                    filePath: request.FilePath,
                    contentType: request.ContentType,
                    fileSizeBytes: request.FileSizeBytes,
                    documentType: request.DocumentType,
                    description: request.Description,
                    createdBy: user.Id
                );
            }
            else
            {
                // Create new document (V1)
                document = EmployeeDocument.Create(
                    employeeId: request.EmployeeId,
                    fileName: request.FileName,
                    filePath: request.FilePath,
                    contentType: request.ContentType,
                    fileSizeBytes: request.FileSizeBytes,
                    documentType: request.DocumentType,
                    description: request.Description,
                    createdBy: user.Id
                );
            }

            documentRepository.Add(document);

            var documentItem = document.MapToDto();

            return BusinessResult.Success(new Response
            {
                DocumentId = document.Id,
                Document = documentItem
            });
        }
    }
}
