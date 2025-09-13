using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Mappers;

public static class BlobMapper
{
    public static IEnumerable<BlobFile> MapToDto(this IEnumerable<string> fileNames)
    {
        return fileNames.Select(MapToDto);
    }

    public static BlobFile MapToDto(this string fileName)
    {
        return new BlobFile(
            FileName: fileName,
            Base64Content: null,
            ContentType: null);
    }
}