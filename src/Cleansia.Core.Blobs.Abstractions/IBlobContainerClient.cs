using Cleansia.Core.Blobs.Abstractions.Extensions;

namespace Cleansia.Core.Blobs.Abstractions;

public interface IBlobContainerClient
{
    Task<IEnumerable<string>> GetFilesAsync(string path, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken);
    Task<BlobFile> DownloadAsync(string blobName, CancellationToken cancellationToken);
    Task UploadAsync(string blobName, Stream stream, Metadata? metadata = null, CancellationToken cancellationToken = new CancellationToken());
    Task DeleteAsync(string blobName, CancellationToken cancellationToken);
    Task<Stream> CreateFileForWritingAsync(string blobName, CancellationToken cancellationToken);

    /// <summary>
    /// Get Uri of the given blob
    /// </summary>
    /// <param name="blobName">Name of blob</param>
    /// <returns></returns>
    Uri GetBlobUri(string blobName);

    /// <summary>
    /// Generate a SAS URI for the given blob with read-only access
    /// </summary>
    /// <param name="blobName">Name of blob</param>
    /// <param name="expiry">How long the SAS token should be valid</param>
    /// <returns>A URI with a SAS token appended</returns>
    Uri GenerateSasUri(string blobName, TimeSpan expiry);

    /// <summary>
    /// Copy contents blob to another blob
    /// Only works when source and target blobs are in the same storage account
    /// </summary>
    /// <param name="sourceUri">Uri of the source blob</param>
    /// <param name="targetBlob">Name of the target blob</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CopyAsync(Uri sourceUri, string targetBlob, CancellationToken cancellationToken);
    /// <summary>
    /// Copy contents blob to another blob within the same container
    /// </summary>
    /// <param name="sourceBlob">Uri of the source blob</param>
    /// <param name="targetBlob">Name of the target blob</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CopyAsync(string sourceBlob, string targetBlob, CancellationToken cancellationToken);
}