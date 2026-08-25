using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// Stop asking for a document type in a country.
///
/// <para><b>Removing a row un-gates the type; it does not un-approve anybody.</b> Approval is decided
/// at the moment an admin approves, and the requirements are an input to that decision rather than a
/// standing property of the cleaner. Editing them never reaches back and re-judges people already
/// approved.</para>
/// </summary>
public class DeleteDocumentRequirement
{
    public record Command(string RequirementId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmployeeDocumentRequirementRepository repository)
        {
            RuleFor(x => x.RequirementId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(repository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);
        }
    }

    public class Handler(IEmployeeDocumentRequirementRepository repository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var requirement = await repository.GetByIdAsync(command.RequirementId, cancellationToken);

            if (requirement is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RequirementId), BusinessErrorMessage.NotFound));
            }

            // A hard delete: this is configuration, not a record of anything that happened. A
            // deactivated requirement would still have to be filtered out of every read, which is a
            // second way to get the answer wrong.
            repository.Remove(requirement);

            return BusinessResult.Success(new Response(true));
        }
    }
}
