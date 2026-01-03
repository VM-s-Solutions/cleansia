using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class GetMyDocuments
{
    public class Query : IQuery<Response>
    {
    }

    public class Response
    {
        public List<MyDocumentDto> Documents { get; init; } = new();
    }

    public class MyDocumentDto
    {
        public string DocumentId { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string BlobUrl { get; init; } = default!;
        public DocumentType DocumentType { get; init; }
        public DocumentStatus Status { get; init; }
        public int Version { get; init; }
        public long FileSizeBytes { get; init; }
        public string ContentType { get; init; } = default!;
        public DateTime UploadedAt { get; init; }
        public string? Description { get; init; }
        public string? ReviewNotes { get; init; }
    }

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(userEmail!, cancellationToken);

            if (user is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication",
                    BusinessErrorMessage.UserNotFound));
            }

            var employee = await employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Employee",
                    BusinessErrorMessage.NotFound));
            }

            // Get only active documents (soft-deleted ones are excluded)
            var documents = await documentRepository.GetByEmployeeIdAsync(employee.Id, includeInactive: false, cancellationToken);

            // Group by filename and get only latest version of each
            var latestDocuments = documents
                .GroupBy(d => d.FileName)
                .Select(g => g.OrderByDescending(d => d.Version).First())
                .OrderByDescending(d => d.CreatedOn)
                .Select(d => new MyDocumentDto
                {
                    DocumentId = d.Id,
                    FileName = d.FileName,
                    BlobUrl = d.FilePath, // Will be converted to full URL by blob service
                    DocumentType = d.DocumentType,
                    Status = d.Status,
                    Version = d.Version,
                    FileSizeBytes = d.FileSizeBytes,
                    ContentType = d.ContentType,
                    UploadedAt = d.CreatedOn.UtcDateTime,
                    Description = d.Description,
                    ReviewNotes = d.ReviewNotes
                })
                .ToList();

            return BusinessResult.Success(new Response
            {
                Documents = latestDocuments
            });
        }
    }
}
