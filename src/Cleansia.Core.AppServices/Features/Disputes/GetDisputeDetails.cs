using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class GetDisputeDetails
{
    public record Query(string DisputeId) : IQuery<DisputeDetails>;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory blobClientFactory) : IQueryHandler<Query, DisputeDetails>
    {
        public async Task<BusinessResult<DisputeDetails>> Handle(Query request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure<DisputeDetails>(new Error(
                    nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString())
            {
                var userId = userSessionProvider.GetUserId();
                if (string.IsNullOrEmpty(userId) || dispute.UserId != userId)
                {
                    return BusinessResult.Failure<DisputeDetails>(new Error(
                        nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
                }
            }

            var evidenceBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.DisputeEvidence);
            return BusinessResult.Success(dispute.MapToDetails(evidenceBlobClient));
        }
    }
}
