using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class SaveMyDocuments
{
    public class Command : ICommand<Response>
    {
        public List<DocumentToSave> Documents { get; init; } = new();
    }

    public class DocumentToSave
    {
        public DocumentType DocumentType { get; init; }
        public BlobFileDto File { get; init; } = default!;
        public string? Description { get; init; }
    }

    public class Response
    {
        public List<SavedDocument> Documents { get; init; } = new();
    }

    public class SavedDocument
    {
        public string DocumentId { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string BlobUrl { get; init; } = default!;
        public DocumentType DocumentType { get; init; }
        public int Version { get; init; }
        public DateTime UploadedAt { get; init; }
    }

    public class Validator : UserEmailValidator<Command>
    {
        /// <summary>
        /// Ten is above any real batch — there are ten document types — and the number matters more than
        /// it looks: the per-document size cap bounds one item, not the list, and the host body limit
        /// buys thousands of SMALL ones. Without this, one request within that limit is tens of
        /// thousands of blob uploads and rows.
        /// </summary>
        private const int MaxDocumentsPerRequest = 10;

        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository) : base(userRepository, userSessionProvider)
        {
            _employeeRepository = employeeRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x)
                .MustAsync(EmployeeExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.Documents)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .Must(documents => documents.Count <= MaxDocumentsPerRequest)
                .WithMessage(BusinessErrorMessage.FileCountExceeded);

            RuleForEach(x => x.Documents).ChildRules(document =>
            {
                document.RuleFor(d => d.File)
                    .Cascade(CascadeMode.Stop)
                    .NotNull().WithMessage(BusinessErrorMessage.Required)
                    .SetValidator(new DocumentFileValidator());

                document.When(d => d.File is not null, () =>
                {
                    document.RuleFor(d => d.File.FileName)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                        .MaximumLength(255).WithMessage(BusinessErrorMessage.MaxLength);
                });

                document.When(d => !string.IsNullOrEmpty(d.Description), () =>
                {
                    document.RuleFor(d => d.Description)
                        .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
                });
            })
            // Without this the per-item rules still decode every item of a list already refused for
            // being too long, which is the cost the count cap exists to refuse.
            .When(x => x.Documents.Count <= MaxDocumentsPerRequest);
        }

        private async Task<bool> EmployeeExistsAsync(Command command, CancellationToken cancellationToken)
        {
            var userEmail = _userSessionProvider.GetUserEmail();
            var employee = await _employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
            return employee is not null;
        }
    }

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory clientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(userEmail!, cancellationToken);
            var employee = await employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);

            var savedDocuments = new List<SavedDocument>();
            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            var employeeDocumentsPath = string.Format(Constants.VirtualDirectories.EmployeeDocuments, employee.Id);

            foreach (var doc in command.Documents)
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var randomGuid = Guid.NewGuid().ToString("N")[..8];
                var fileExtension = Path.GetExtension(doc.File.FileName);
                var uniqueFileName = $"{employee.Id}_{doc.DocumentType}_{timestamp}_{randomGuid}{fileExtension}";
                var fullBlobPath = $"{employeeDocumentsPath}/{uniqueFileName}";

                var base64Data = doc.File.Base64Content!.ExtractBase64Data();
                var contentType = DocumentContentType.FromContent(doc.File.Base64Content)!;

                await using var stream = new MemoryStream(Convert.FromBase64String(base64Data));

                var metadata = MetadataExtensions.CreateDocumentMetadata(
                    doc.File.FileName,
                    contentType,
                    user.Id);

                await client.UploadAsync(fullBlobPath, stream, metadata, cancellationToken);
                var blobUrl = client.GetBlobUri(fullBlobPath).ToString();

                // Check for existing document with same filename (auto-versioning)
                var existingDocument = await documentRepository.GetLatestByFileNameAsync(
                    employee.Id,
                    doc.File.FileName,
                    cancellationToken);

                EmployeeDocument employeeDocument;
                if (existingDocument is not null)
                {
                    employeeDocument = EmployeeDocument.CreateNewVersion(
                        previousVersion: existingDocument,
                        fileName: doc.File.FileName,
                        filePath: fullBlobPath,
                        contentType: contentType,
                        fileSizeBytes: stream.Length,
                        documentType: doc.DocumentType,
                        description: doc.Description,
                        createdBy: user.Id
                    );
                }
                else
                {
                    // Create new document (V1)
                    employeeDocument = EmployeeDocument.Create(
                        employeeId: employee.Id,
                        fileName: doc.File.FileName,
                        filePath: fullBlobPath,
                        contentType: contentType,
                        fileSizeBytes: stream.Length,
                        documentType: doc.DocumentType,
                        description: doc.Description,
                        createdBy: user.Id
                    );
                }

                documentRepository.Add(employeeDocument);

                savedDocuments.Add(new SavedDocument
                {
                    DocumentId = employeeDocument.Id,
                    FileName = doc.File.FileName,
                    BlobUrl = blobUrl,
                    DocumentType = doc.DocumentType,
                    Version = employeeDocument.Version,
                    UploadedAt = employeeDocument.CreatedOn.UtcDateTime
                });
            }

            return BusinessResult.Success(new Response
            {
                Documents = savedDocuments
            });
        }
    }
}
