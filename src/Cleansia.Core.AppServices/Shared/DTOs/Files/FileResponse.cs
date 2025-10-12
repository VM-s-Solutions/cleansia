namespace Cleansia.Core.AppServices.Shared.DTOs.Files;

public record FileResponse(
    byte[] Data,
    string FileName,
    string ContentType = "application/pdf");
