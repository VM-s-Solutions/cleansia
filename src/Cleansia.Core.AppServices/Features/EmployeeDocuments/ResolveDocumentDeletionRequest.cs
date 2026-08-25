using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// An admin answering a cleaner's request to remove a document. The ONLY thing that deletes one.
///
/// <para><b>Approving is what performs the deletion.</b> The request never touched the document, so
/// until this runs the cleaner is exactly as they were. That is the whole reason the flow was split:
/// a request that nobody answers costs them nothing, where the old delete button cost them their
/// access to work the moment it was tapped.</para>
///
/// <para><b>Rejecting is a real answer and is why the notes matter.</b> Approval speaks for itself —
/// the document is gone. A refusal without a reason tells the cleaner only that somebody said no, so
/// they either ask again or stop trusting the queue.</para>
/// </summary>
[AuditAction("employee_document.deletion_request.resolve", ResourceType = "User")]
public class ResolveDocumentDeletionRequest
{
    public record Request(bool Approve, string? Notes);

    public record Command(string RequestId, bool Approve, string? Notes = null) : ICommand<Response>;

    public record Response(string RequestId, DocumentDeletionRequestStatus Status);

    /// <summary>
    /// Keyed on the EMPLOYEE's user id — the audited subject an employee drill-in filters on, matching
    /// the other employee actions. The cleaner's free-text reason is deliberately absent: it is their
    /// words about their own paperwork, and the audit trail needs the decision, not the plea.
    /// </summary>
    public record DeletionSnapshot(string EmployeeId, string DocumentId, DocumentDeletionRequestStatus Status);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IDocumentDeletionRequestRepository requestRepository)
        {
            RuleFor(x => x.RequestId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(requestRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound)
                .MustAsync(async (requestId, cancellationToken) =>
                {
                    var request = await requestRepository.GetByIdAsync(requestId, cancellationToken);
                    return request?.IsOpen ?? false;
                })
                .WithMessage(BusinessErrorMessage.DocumentDeletionAlreadyResolved);

            When(x => !x.Approve, () =>
            {
                // A refusal has to say why. The cleaner cannot see the queue, so this note is the
                // only thing that reaches them.
                RuleFor(x => x.Notes)
                    .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                    .MaximumLength(1000).WithMessage(BusinessErrorMessage.MaxLength);
            });

            When(x => x.Approve, () =>
            {
                RuleFor(x => x.Notes)
                    .MaximumLength(1000).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public class Handler(
        IDocumentDeletionRequestRepository requestRepository,
        IEmployeeDocumentRepository documentRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail();
            var adminUser = await userRepository.GetByEmailAsync(adminEmail!, cancellationToken);

            if (adminUser is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication", BusinessErrorMessage.UserNotFound));
            }

            var request = await requestRepository.GetByIdAsync(command.RequestId, cancellationToken);

            if (request is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RequestId), BusinessErrorMessage.NotFound));
            }

            var before = new DeletionSnapshot(request.EmployeeId, request.DocumentId, request.Status);

            if (command.Approve)
            {
                var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);

                // Approving a request whose document has already gone is not an error — the outcome
                // the cleaner asked for is the outcome they have. Answering the request anyway is what
                // closes it out of the queue.
                document?.SoftDelete(adminUser.Id);

                request.Approve(adminUser.Id, command.Notes);
            }
            else
            {
                request.Reject(adminUser.Id, command.Notes);
            }

            var after = new DeletionSnapshot(request.EmployeeId, request.DocumentId, request.Status);
            auditContext.RecordChange("User", request.EmployeeId, before, after);

            return BusinessResult.Success(new Response(request.Id, request.Status));
        }
    }
}
