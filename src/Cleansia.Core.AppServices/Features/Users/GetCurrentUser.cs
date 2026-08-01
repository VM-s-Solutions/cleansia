using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetCurrentUser
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            RuleFor(query => query)
                .SetValidator(new UserEmailValidator<Query>(userRepository, userSessionProvider));
        }

    }

    public record Query : IQuery<MyProfileDto>;

    public class Handler(
        IUserRepository userRepository,
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory blobClientFactory,
        ILogger<Handler> logger)
        : IQueryHandler<Query, MyProfileDto>
    {
        private static readonly TimeSpan ProfilePhotoSasLifetime = TimeSpan.FromHours(1);

        public async Task<BusinessResult<MyProfileDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailNoTrackingAsync(userSessionProvider.GetUserEmail()!, cancellationToken);
            var stats = await orderRepository.GetCustomerProfileStatsAsync(user!.Id, cancellationToken);
            return BusinessResult.Success(user.MapToMyProfileDto(stats, ResolveProfilePhotoUrl(user.ProfilePhotoName))!);
        }

        private string? ResolveProfilePhotoUrl(string? blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                return null;
            }

            // The avatar is decoration; the profile screen is the core action. A storage outage must
            // cost the image, never the read. Log the blob name only — the SAS itself is a credential.
            try
            {
                return blobClientFactory
                    .GetBlobContainerClient(Constants.BlobContainers.UserFiles)
                    .GenerateSasUri(blobName, ProfilePhotoSasLifetime)
                    .ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to generate SAS URI for profile photo {BlobName}; returning the profile without an image URL",
                    blobName);
                return null;
            }
        }
    }
}
