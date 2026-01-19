using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class GetEmailTemplateById
{
    public record Query(string EmailTemplateId) : IQuery<EmailTemplateTranslationDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmailTemplateTranslationRepository emailTemplateRepository)
        {
            RuleFor(x => x.EmailTemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(emailTemplateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmailTemplateNotFound);
        }
    }

    internal class Handler(IEmailTemplateTranslationRepository emailTemplateRepository)
        : IQueryHandler<Query, EmailTemplateTranslationDetailDto>
    {
        public async Task<BusinessResult<EmailTemplateTranslationDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var template = await emailTemplateRepository
                .GetQueryable()
                .Include(t => t.Language)
                .FirstOrDefaultAsync(t => t.Id == query.EmailTemplateId, cancellationToken);

            return BusinessResult.Success(template!.MapToDetailDto());
        }
    }
}