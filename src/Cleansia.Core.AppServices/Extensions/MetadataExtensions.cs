using Cleansia.Core.Blobs.Abstractions.Extensions;

namespace Cleansia.Core.AppServices.Extensions;

public static class MetadataExtensions
{
    public static Metadata CreateDocumentMetadata(string originalFileName, string contentType, string uploadedBy)
    {
        return Metadata.CreateBuilder()
            .WithMetadata("OriginalFileName", originalFileName ?? "unknown")
            .WithMetadata("ContentType", contentType ?? "application/octet-stream")
            .WithMetadata("UploadedBy", uploadedBy)
            .WithMetadata("UploadedAt", DateTimeOffset.UtcNow.ToString("O"))
            .Build();
    }
}