using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Languages;

public class GetLanguageOverview
{
    public class Request : IRequest<IEnumerable<LanguageListItem>>;

    public class Handler(ILanguageRepository languageRepository) : IRequestHandler<Request, IEnumerable<LanguageListItem>>
    {
        public async Task<IEnumerable<LanguageListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            return await languageRepository.GetAll().Select(language => language.MapToDto()).ToListAsync(cancellationToken);
        }
    }
}