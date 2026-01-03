using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class DownloadMyDocument
{
    public record Query(string DocumentId) : IQuery<Response>;

    public record Response(
        byte[] FileBytes,
        string FileName,
        string ContentType);

    public class Validator : AbstractValidator<Query>
    {
        private readonly IEmployeeDocumentRepository _documentRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IEmployeeDocumentRepository documentRepository,
            IEmployeeRepository employeeRepository,
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            _documentRepository = documentRepository;
            _employeeRepository = employeeRepository;
            _userRepository = userRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.DocumentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(DocumentExistsAndIsActiveAsync)
                .WithMessage(BusinessErrorMessage.DocumentNotFound)
                .MustAsync(UserOwnsDocumentAsync)
                .WithMessage(BusinessErrorMessage.Unauthorized);
        }

        private async Task<bool> DocumentExistsAndIsActiveAsync(string documentId, CancellationToken cancellationToken)
        {
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            return document is { IsActive: true };
        }

        private async Task<bool> UserOwnsDocumentAsync(string documentId, CancellationToken cancellationToken)
        {
            var userEmail = _userSessionProvider.GetUserEmail();
            if (string.IsNullOrEmpty(userEmail))
            {
                return false;
            }

            var user = await _userRepository.GetByEmailAsync(userEmail, cancellationToken);
            if (user == null)
            {
                return false;
            }

            var employee = await _employeeRepository.GetByUserEmailAsync(userEmail, cancellationToken);
            if (employee == null)
            {
                return false;
            }

            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            return document?.EmployeeId == employee.Id;
        }
    }

    public class Handler(
        IEmployeeDocumentRepository documentRepository,
        IBlobContainerClientFactory blobContainerClientFactory) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var document = await documentRepository.GetByIdAsync(query.DocumentId, cancellationToken);

            var blobClient = blobContainerClientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            var blobFile = await blobClient.DownloadAsync(document!.FilePath, cancellationToken);

            using var memoryStream = new MemoryStream();
            await blobFile.Content.CopyToAsync(memoryStream, cancellationToken);
            var fileBytes = memoryStream.ToArray();

            return BusinessResult.Success(new Response(
                FileBytes: fileBytes,
                FileName: document.FileName,
                ContentType: document.ContentType
            ));
        }
    }
}
