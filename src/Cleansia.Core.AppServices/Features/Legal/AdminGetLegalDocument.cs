using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Legal;

public class AdminGetLegalDocument
{
    public record Query(string Id, string? Language = null) : IQuery<AdminLegalDocumentDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ILegalDocumentRepository legalDocumentRepository)
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(legalDocumentRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.LegalDocumentNotFound);

            RuleFor(x => x.Language).MaximumLength(10).WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    internal class Handler(ILegalDocumentRepository legalDocumentRepository)
        : IQueryHandler<Query, AdminLegalDocumentDto>
    {
        public async Task<BusinessResult<AdminLegalDocumentDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var document = await legalDocumentRepository.GetWithTextsAsync(request.Id, cancellationToken);
            var text = document?.TextForOrFallback(request.Language);
            if (document is null || text is null)
            {
                return BusinessResult.Failure<AdminLegalDocumentDto>(
                    new Error(nameof(request.Id), BusinessErrorMessage.LegalDocumentNotFound));
            }

            return BusinessResult.Success(document.MapToAdminDto(text, DateOnly.FromDateTime(DateTime.UtcNow)));
        }
    }
}
