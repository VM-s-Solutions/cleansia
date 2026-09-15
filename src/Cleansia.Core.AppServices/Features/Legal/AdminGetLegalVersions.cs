using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Legal;

public class AdminGetLegalVersions
{
    public record Query(
        LegalDocumentAudience? Audience = null,
        LegalDocumentType? Type = null,
        string? CountryId = null) : IQuery<IReadOnlyList<LegalDocumentVersionDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Audience).IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue).When(x => x.Audience is not null);
            RuleFor(x => x.Type).IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue).When(x => x.Type is not null);
            RuleFor(x => x.CountryId).MaximumLength(26).WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    internal class Handler(ILegalDocumentRepository legalDocumentRepository)
        : IQueryHandler<Query, IReadOnlyList<LegalDocumentVersionDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<LegalDocumentVersionDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var versions = await legalDocumentRepository.GetVersionsAsync(
                request.Audience, request.Type, request.CountryId, cancellationToken);

            IReadOnlyList<LegalDocumentVersionDto> items = versions.Select(v => v.MapToVersionDto(today)).ToList();
            return BusinessResult.Success(items);
        }
    }
}
