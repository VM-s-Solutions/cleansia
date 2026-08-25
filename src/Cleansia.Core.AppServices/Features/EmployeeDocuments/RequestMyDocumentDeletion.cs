using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// A cleaner asking for one of their documents to be removed. It does not remove anything.
///
/// <para><b>Why asking replaced doing.</b> The delete button removed the document immediately, with no
/// confirmation on either mobile platform, and the soft-delete flipped <c>AreDocumentsUploaded</c> —
/// which re-engaged the registration lock. One tap cost a cleaner their access to work. And we need
/// some of those documents as the employer: the person least placed to judge whether one can go was
/// the only one who could remove it.</para>
///
/// <para><b>The document stays ACTIVE.</b> A pending request is a message, not a state change, so a
/// request nobody answers leaves the cleaner exactly as they were rather than locked out. Only
/// <see cref="ResolveDocumentDeletionRequest"/> actually removes anything.</para>
///
/// <para><b>Replacing is the other door.</b> <see cref="ReplaceMyDocument"/> needs no permission
/// because the slot never empties — a newer version supersedes the old one and the count never drops.
/// This command is for the case where nothing should be there at all, which is the case an employer
/// has to agree with.</para>
/// </summary>
public class RequestMyDocumentDeletion
{
    public record Request(string Reason);

    public record Command(string DocumentId, string Reason) : ICommand<Response>;

    public record Response(string RequestId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IEmployeeDocumentRepository _documentRepository;
        private readonly IDocumentDeletionRequestRepository _requestRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository,
            IEmployeeDocumentRepository documentRepository,
            IDocumentDeletionRequestRepository requestRepository)
            : base(userRepository, userSessionProvider)
        {
            _employeeRepository = employeeRepository;
            _documentRepository = documentRepository;
            _requestRepository = requestRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.DocumentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(documentRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(BeOwnedByCurrentEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentNotOwned)
                // One open request per document. A second is not more urgent, it is the same ask
                // twice — and it would give an admin two rows to answer for one decision.
                .MustAsync(HaveNoOpenRequestAsync)
                .WithMessage(BusinessErrorMessage.DocumentDeletionAlreadyRequested);

            // The reason IS the request. Without one an admin is being asked to rule on nothing, and
            // the whole point of routing this through a person is that they have something to judge.
            RuleFor(x => x.Reason)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(1000).WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> HaveNoOpenRequestAsync(string documentId, CancellationToken cancellationToken)
        {
            var open = await _requestRepository.GetOpenForDocumentAsync(documentId, cancellationToken);
            return open is null;
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
        IDocumentDeletionRequestRepository requestRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail();
            var employee = await employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication", BusinessErrorMessage.EmployeeNotFound));
            }

            var document = await documentRepository.GetByIdAsync(command.DocumentId, cancellationToken);

            // The validator proved this, but a load that diverges from its check is how a 500 replaces
            // a business error.
            if (document is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.DocumentId), BusinessErrorMessage.NotFound));
            }

            var request = DocumentDeletionRequest.Create(
                documentId: document.Id,
                employeeId: employee.Id,
                reason: command.Reason,
                createdBy: employee.UserId);

            requestRepository.Add(request);

            return BusinessResult.Success(new Response(request.Id));
        }
    }
}
