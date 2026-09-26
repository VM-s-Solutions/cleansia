namespace Cleansia.Core.AppServices.Common;

public static class OrderPhotoBlobName
{
    /// <summary>
    /// The container-relative blob name inside a stored order-photo URL. The container segment is found by
    /// NAME: Azure serves <c>/&lt;container&gt;/&lt;blob&gt;</c> and Azurite
    /// <c>/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>, so a positional skip leaves the container in
    /// the name on one of them.
    /// </summary>
    public static string FromUrl(string blobUrl)
    {
        var pathSegments = new Uri(blobUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var containerIndex = Array.IndexOf(pathSegments, Constants.BlobContainers.OrderPhotos);
        return containerIndex >= 0 && containerIndex + 1 < pathSegments.Length
            ? string.Join("/", pathSegments.Skip(containerIndex + 1))
            : string.Join("/", pathSegments.Skip(1));
    }
}
