using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

/// <summary>
/// Returns all <see cref="Cleansia.Core.Domain.Services.ServiceCategory"/>
/// rows for admin-side dropdowns (currently the Service create/edit form's
/// category picker). Sorted by <c>DisplayOrder</c> so admin sees them in the
/// same order customers do.
/// </summary>
public class GetServiceCategories
{
    public record Request : IRequest<IEnumerable<CategoryDto>>;

    public class Handler(IServiceCategoryRepository categoryRepository)
        : IRequestHandler<Request, IEnumerable<CategoryDto>>
    {
        public async Task<IEnumerable<CategoryDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var categories = await categoryRepository.GetAll()
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return categories.Select(c => new CategoryDto(
                Id: c.Id,
                Slug: c.Slug,
                Name: c.Name,
                Description: c.Description,
                DisplayOrder: c.DisplayOrder,
                Translations: null));
        }
    }
}
