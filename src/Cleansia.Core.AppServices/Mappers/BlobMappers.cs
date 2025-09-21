using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Mappers;

public static class BlobMapper
{
    public static IEnumerable<BlobFileDto> MapToDto(this IEnumerable<string> fileNames)
    {
        return fileNames.Select(MapToDto);
    }

    public static BlobFileDto MapToDto(this string fileName)
    {
        return new BlobFileDto(
            FileName: fileName,
            Base64Content: null,
            ContentType: null);
    }
}