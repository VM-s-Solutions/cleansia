using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class GetDisputeDetails
{
    public record Query(string DisputeId) : IQuery<DisputeDetails>;

    public class Handler : IQueryHandler<Query, DisputeDetails>
    {
        private readonly IDisputeRepository _disputeRepository;
        private readonly IBlobContainerClientFactory _blobClientFactory;

        public Handler(IDisputeRepository disputeRepository, IBlobContainerClientFactory blobClientFactory)
        {
            _disputeRepository = disputeRepository;
            _blobClientFactory = blobClientFactory;
        }

        public async Task<BusinessResult<DisputeDetails>> Handle(Query request, CancellationToken cancellationToken)
        {
            var dispute = await _disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId);

            if (dispute == null)
            {
                return BusinessResult.Failure<DisputeDetails>(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var evidenceBlobClient = _blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.DisputeEvidence);
            return BusinessResult.Success(dispute.MapToDetails(evidenceBlobClient));
        }
    }
}
