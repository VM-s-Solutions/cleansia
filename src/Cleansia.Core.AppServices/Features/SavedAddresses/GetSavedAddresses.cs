using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.AppServices.Features.SavedAddresses.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class GetSavedAddresses
{
    public record Query : IQuery<IReadOnlyList<SavedAddressDto>>;

    public class Handler(
        ISavedAddressRepository savedAddressRepository,
        IUserSessionProvider userSessionProvider,
        ILogger<Handler> logger)
        : IQueryHandler<Query, IReadOnlyList<SavedAddressDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<SavedAddressDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var items = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);

            var dtos = new List<SavedAddressDto>(items.Count);
            foreach (var s in items)
            {
                if (s.Address is null)
                {
                    // A null shared-Address FK is a data defect; log it so the orphan is observable
                    // rather than silently disappearing from the user's list.
                    logger.LogWarning(
                        "SavedAddress {SavedAddressId} has a null Address (orphaned); excluded from the user list",
                        s.Id);
                    continue;
                }

                dtos.Add(s.MapToDto());
            }

            return BusinessResult.Success<IReadOnlyList<SavedAddressDto>>(dtos);
        }
    }
}
