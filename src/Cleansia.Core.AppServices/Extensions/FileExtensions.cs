namespace Cleansia.Core.AppServices.Extensions;

public static class FileExtensions
{

    /// <summary>
    /// Extracts the Base64 content by removing any data URI prefix if present.
    /// </summary>
    /// <param name="base64Content">The original Base64 string (possibly with a prefix).</param>
    /// <returns>The raw Base64 string.</returns>
    public static string ExtractBase64Data(this string base64Content)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return base64Content;
        }

        var parts = base64Content.Split(',');
        return parts.Length > 1 ? parts[1] : parts[0];
    }
}