using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class UploadDisputeEvidence
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "application/pdf"
    };

    public record Command(
        string DisputeId,
        string FileName,
        string ContentType,
        byte[] FileData) : ICommand<Response>;

    public record Response(
        string EvidenceId,
        string FileName,
        string? BlobUrl,
        DateTimeOffset UploadedOn);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IDisputeRepository disputeRepository)
        {
            RuleFor(x => x.DisputeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(disputeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.DisputeNotFound);

            RuleFor(x => x.FileName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(255)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ContentType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(ct => AllowedContentTypes.Contains(ct.ToLowerInvariant()))
                .WithMessage(BusinessErrorMessage.InvalidFileType);

            RuleFor(x => x.FileData)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.FileRequired)
                .Must(data => data.Length <= MaxFileSizeBytes)
                .WithMessage(BusinessErrorMessage.FileSizeExceeded);
        }
    }

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        IBlobContainerClientFactory blobClientFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var dispute = await disputeRepository.GetQueryable()
                .FirstOrDefaultAsync(d => d.Id == command.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            if (dispute.UserId != userId)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.DisputeId), BusinessErrorMessage.DisputeNotOwnedByUser));
            }

            var fileExtension = Path.GetExtension(command.FileName);
            var blobName = $"{command.DisputeId}/{Guid.NewGuid():N}{fileExtension}";

            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.DisputeEvidence);
            using var stream = new MemoryStream(command.FileData);
            var metadata = Metadata.CreateBuilder()
                .WithMetadata(MetadataName.ContentType, command.ContentType)
                .Build();
            await blobClient.UploadAsync(blobName, stream, metadata, cancellationToken);

            dispute.AddEvidence(command.FileName, blobName, userId);

            var addedEvidence = dispute.Evidence.LastOrDefault(e => e.FilePath == blobName);

            // The evidence is already persisted to blob storage and recorded on the dispute; a failure
            // generating the read SAS must NOT fail the upload. Degrade to a null URL (the caller can
            // re-resolve it later) but never swallow silently (S6) — log it for diagnosis.
            string? blobUrl = null;
            try
            {
                blobUrl = blobClient.GenerateSasUri(blobName, TimeSpan.FromHours(1)).ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to generate SAS URI for dispute evidence {BlobName} on dispute {DisputeId}; returning null URL",
                    blobName, command.DisputeId);
            }

            return BusinessResult.Success(new Response(
                EvidenceId: addedEvidence?.Id ?? string.Empty,
                FileName: command.FileName,
                BlobUrl: blobUrl,
                UploadedOn: addedEvidence?.UploadedOn ?? DateTimeOffset.UtcNow));
        }
    }
}
