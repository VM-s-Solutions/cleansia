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
            // An admin reads their company's disputes through the filter; a customer reads their own in
            // every operating company, because a dispute is stamped with its ORDER's operator and the
            // customer may have booked across the border (S8: pinned by the caller's own id).
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            var isAdmin = role == UserProfile.Administrator.ToString();
            var userId = userSessionProvider.GetUserId();

            var dispute = isAdmin
                ? await disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId, cancellationToken)
                : string.IsNullOrEmpty(userId)
                    ? null
                    : await disputeRepository.GetDisputeWithDetailsForOwnerAsync(request.DisputeId, userId, cancellationToken);

            if (dispute == null || (!isAdmin && dispute.UserId != userId))
            {
                return BusinessResult.Failure<DisputeDetails>(new Error(
                    nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var evidenceBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.DisputeEvidence);
            return BusinessResult.Success(dispute.MapToDetails(evidenceBlobClient));
        }
    }
}
