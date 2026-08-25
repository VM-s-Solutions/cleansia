using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// Supersede one of the cleaner's own documents with a newer file.
///
/// <para><b>This is the door that needs no permission, and that is the point.</b> Replacing never
/// empties the slot: the new version is created before anything else changes, so
/// <c>AreDocumentsUploaded</c> never dips and the registration lock never re-engages. A cleaner whose
/// ID has expired can act on it themselves at 9pm without waiting for an admin.
/// <see cref="RequestMyDocumentDeletion"/> is for the other case — nothing should be there at all —
/// and that one an employer has to agree with.</para>
///
/// <para><b>Approved documents may be replaced.</b> Removing one takes proof we are required to hold,
/// which is why removal now goes past an admin at all. Replacing SUPPLIES newer proof, so the same
/// objection does not apply — and refusing here would mean the only way to update an expiring approved
/// document was to ask an admin to remove it first, leaving a gap on purpose.</para>
///
/// <para><b>Targets a document ID, not a filename.</b> <c>SaveMyDocuments</c> already auto-versions,
/// but it matches on file name — so a replacement photographed on a different phone lands as a second
/// unrelated document, and two files that happen to share a name collapse into one chain. An id says
/// exactly which document is being replaced.</para>
///
/// <para>The new version is <c>Pending</c>: it is new evidence and has not been looked at. That is
/// also why this cannot be used to dodge review — replacing an approved document costs its approved
/// status until an admin restores it.</para>
/// </summary>
public class ReplaceMyDocument
{
    public record Request(BlobFileDto File, string? Description);

    public record Command(string DocumentId, BlobFileDto File, string? Description = null)
        : ICommand<Response>;

    public record Response(string DocumentId, int Version);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IEmployeeDocumentRepository _documentRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository,
            IEmployeeDocumentRepository documentRepository)
            : base(userRepository, userSessionProvider)
        {
            _employeeRepository = employeeRepository;
            _documentRepository = documentRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.DocumentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(documentRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeOwnedByCurrentEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentNotOwned);

            // The same validator the upload path uses, so a replacement cannot smuggle in a file
            // shape a fresh upload would have refused.
            RuleFor(x => x.File)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(BusinessErrorMessage.Required)
                .SetValidator(new DocumentFileValidator());

            RuleFor(x => x.File.FileName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(255).WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BeOwnedByCurrentEmployeeAsync(string documentId, CancellationToken cancellationToken)
        {
            var userEmail = _userSessionProvider.GetUserEmail();
            var employee = await _employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
            if (employee is null) return false;

            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            return document is not null && document.EmployeeId == employee.Id;
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

            if (user is null || employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication", BusinessErrorMessage.EmployeeNotFound));
            }

            var previous = await documentRepository.GetByIdAsync(command.DocumentId, cancellationToken);

            if (previous is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.DocumentId), BusinessErrorMessage.NotFound));
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var randomGuid = Guid.NewGuid().ToString("N")[..8];
            var fileExtension = Path.GetExtension(command.File.FileName);
            var uniqueFileName =
                $"{employee.Id}_{previous.DocumentType}_{timestamp}_{randomGuid}{fileExtension}";

            var employeeDocumentsPath =
                string.Format(Constants.VirtualDirectories.EmployeeDocuments, employee.Id);
            var fullBlobPath = $"{employeeDocumentsPath}/{uniqueFileName}";

            var base64Data = command.File.Base64Content!.ExtractBase64Data();

            // Sniffed, never trusted from the client — the same rule the upload path follows. A
            // declared content type is a claim by whoever is uploading.
            var contentType = SniffedContentType.FromContent(
                command.File.Base64Content, UploadIntake.EmployeeDocument)!;

            await using var stream = new MemoryStream(Convert.FromBase64String(base64Data));

            var metadata = MetadataExtensions.CreateDocumentMetadata(
                command.File.FileName, contentType, user.Id);

            var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            await client.UploadAsync(fullBlobPath, stream, metadata, cancellationToken);

            // The DOCUMENT TYPE is carried over, never taken from the caller. Replacing is "here is a
            // newer one of these"; letting it change type would let a cleaner satisfy a requirement by
            // relabelling a document an admin already approved as something else.
            var replacement = EmployeeDocument.CreateNewVersion(
                previousVersion: previous,
                fileName: command.File.FileName,
                filePath: fullBlobPath,
                contentType: contentType,
                fileSizeBytes: stream.Length,
                documentType: previous.DocumentType,
                description: command.Description,
                createdBy: user.Id);

            documentRepository.Add(replacement);

            // Ordered: the new version exists before the old one is retired, so no read between the
            // two sees the cleaner with one fewer document. That ordering is the reason replacing
            // needs no admin — the count never dips, so the registration lock never re-engages.
            previous.SoftDelete(user.Id);

            return BusinessResult.Success(new Response(replacement.Id, replacement.Version));
        }
    }
}
