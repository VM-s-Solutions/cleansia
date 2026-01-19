using Cleansia.Core.AppServices.Features.EmailTemplates.DTOs;
using Cleansia.Core.AppServices.Features.EmailTemplates.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmailTemplates;

public class GetPagedEmailTemplates
{
    public class Request : DataRangeRequest, IRequest<PagedData<EmailTemplateTranslationListItem>>
    {
        public EmailTemplateFilter? Filter { get; init; }
    }

    internal class Handler(IEmailTemplateTranslationRepository emailTemplateRepository)
        : IRequestHandler<Request, PagedData<EmailTemplateTranslationListItem>>
    {
        public async Task<PagedData<EmailTemplateTranslationListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = EmailTemplateTranslationSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                emailType: request.Filter?.EmailType,
                languageId: request.Filter?.LanguageId
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await emailTemplateRepository.GetCountAsync(filter, cancellationToken);
            var items = await emailTemplateRepository
                .GetPagedSort<EmailTemplateTranslationSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(t => t.Language)
                .AsNoTracking()
                .Select(template => template.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}