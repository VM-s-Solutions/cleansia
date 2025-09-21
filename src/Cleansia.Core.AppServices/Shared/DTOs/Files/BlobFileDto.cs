namespace Cleansia.Core.AppServices.Shared.DTOs.Files;

public record BlobFileDto(
    string FileName,
    string? Base64Content,
    string? ContentType);