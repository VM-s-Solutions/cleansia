namespace Cleansia.Core.AppServices.Shared.DTOs.Files;

public record BlobFile(
    string FileName,
    string? Base64Content,
    string? ContentType);