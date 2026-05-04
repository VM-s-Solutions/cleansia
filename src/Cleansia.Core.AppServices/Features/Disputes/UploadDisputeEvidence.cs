using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class UploadDisputeEvidence
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

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
        byte[] FileData,
        string UserId = "") : ICommand<Response>;

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

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

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
        IBlobContainerClientFactory blobClientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetQueryable()
                .FirstOrDefaultAsync(d => d.Id == command.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            if (dispute.UserId != command.UserId)
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

            dispute.AddEvidence(command.FileName, blobName, command.UserId);

            // Re-resolve the just-added evidence so we can return its id and timestamp.
            var addedEvidence = dispute.Evidence.LastOrDefault(e => e.FilePath == blobName);

            string? blobUrl = null;
            try
            {
                blobUrl = blobClient.GenerateSasUri(blobName, TimeSpan.FromHours(1)).ToString();
            }
            catch
            {
                // Swallow — UI handles null gracefully.
            }

            return BusinessResult.Success(new Response(
                EvidenceId: addedEvidence?.Id ?? string.Empty,
                FileName: command.FileName,
                BlobUrl: blobUrl,
                UploadedOn: addedEvidence?.UploadedOn ?? DateTimeOffset.UtcNow));
        }
    }
}
