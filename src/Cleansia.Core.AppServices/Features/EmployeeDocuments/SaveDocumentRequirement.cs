using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// Add or edit one country's requirement for a document type.
/// </summary>
public class SaveDocumentRequirement
{
    public record Request(string CountryId, DocumentType DocumentType, bool IsRequired, int SortOrder);

    public record Command(string CountryId, DocumentType DocumentType, bool IsRequired, int SortOrder)
        : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);

            RuleFor(x => x.DocumentType)
                .IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.SortOrder)
                .GreaterThanOrEqualTo(0).WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    /// <summary>
    /// An upsert, not an insert. The unique index on (CountryId, DocumentType) means a second row for
    /// the same pair is not a variant of the rule — it is two rules disagreeing, and whichever the
    /// query read first would win. Saving the same pair twice edits the flag rather than colliding.
    /// </summary>
    public class Handler(
        IEmployeeDocumentRequirementRepository repository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actor = userSessionProvider.GetUserId() ?? "system";

            var existing = (await repository.GetForCountryAsync(command.CountryId, cancellationToken))
                .FirstOrDefault(r => r.DocumentType == command.DocumentType);

            if (existing is not null)
            {
                existing.Update(command.IsRequired, command.SortOrder, actor);
                return BusinessResult.Success(new Response(existing.Id));
            }

            var requirement = EmployeeDocumentRequirement.Create(
                command.CountryId, command.DocumentType, command.IsRequired, command.SortOrder, actor);

            repository.Add(requirement);

            return BusinessResult.Success(new Response(requirement.Id));
        }
    }
}
