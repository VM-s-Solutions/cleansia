using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Languages;

public class GetLanguageById
{
    public record Query(string LanguageId) : IQuery<LanguageDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ILanguageRepository languageRepository)
        {
            RuleFor(x => x.LanguageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await languageRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.LanguageNotFound);
        }
    }

    internal class Handler(ILanguageRepository languageRepository)
        : IQueryHandler<Query, LanguageDetailDto>
    {
        public async Task<BusinessResult<LanguageDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var language = await languageRepository.GetByIdAsync(query.LanguageId, cancellationToken);

            if (language is null)
            {
                return BusinessResult.Failure<LanguageDetailDto>(new Error(nameof(query.LanguageId), BusinessErrorMessage.LanguageNotFound));
            }

            return BusinessResult.Success(language.MapToDetailDto());
        }
    }
}