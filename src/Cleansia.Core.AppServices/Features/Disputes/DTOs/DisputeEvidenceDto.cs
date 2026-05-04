namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

public record DisputeEvidenceDto(
    string Id,
    string FileName,
    string FilePath,
    string? BlobUrl,
    string UploadedBy,
    DateTimeOffset UploadedOn
);
