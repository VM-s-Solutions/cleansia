using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;

namespace Cleansia.Core.AppServices.Common.Validators;

internal static class BlobFileSize
{
    private const int MaxFileSizeInMB = 10;
    private const long MaxFileSizeInBytes = MaxFileSizeInMB * 1024 * 1024;

    /// <summary>
    /// Derives the size from the ENCODED length instead of decoding, because decoding a payload that
    /// is about to be rejected allocates it twice — which is the cost this rule exists to prevent. Any
    /// validator using it must therefore place it BEFORE every rule that touches the bytes. The
    /// derivation rounds up by at most two bytes, so it never under-reports.
    /// </summary>
    public static bool HasContentWithinLimit(BlobFileDto fileDto)
    {
        if (string.IsNullOrWhiteSpace(fileDto.Base64Content))
        {
            return false;
        }

        var base64Data = fileDto.Base64Content.ExtractBase64Data();
        var fileSizeInBytes = (base64Data.Length * 3L) / 4L;

        return fileSizeInBytes <= MaxFileSizeInBytes;
    }
}
