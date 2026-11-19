using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;

namespace Cleansia.TestUtilities;

/// <summary>
/// A blob container that refuses a second write to one name, as Azure's create-only upload does
/// (409 BlobAlreadyExists), so a test sees the overwrite a mock would silently accept.
/// </summary>
public sealed class InMemoryBlobContainer(string container) : IBlobContainerClient
{
    public Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    public Task<IEnumerable<string>> GetFilesAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<string>>(Blobs.Keys.Where(n => n.StartsWith(path, StringComparison.Ordinal)).ToList());

    public Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.ContainsKey(blobName));

    public Task<BlobFile> DownloadAsync(string blobName, CancellationToken cancellationToken) =>
        Task.FromResult(new BlobFile(new MemoryStream(Blobs[blobName], writable: false), "application/octet-stream"));

    public async Task UploadAsync(string blobName, Stream stream, Metadata? metadata = null, CancellationToken cancellationToken = default)
    {
        if (!await UploadIfAbsentAsync(blobName, stream, cancellationToken))
        {
            throw new InvalidOperationException($"409 BlobAlreadyExists: {blobName}");
        }
    }

    public async Task<bool> UploadIfAbsentAsync(string blobName, Stream stream, CancellationToken cancellationToken)
    {
        if (Blobs.ContainsKey(blobName))
        {
            return false;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        Blobs[blobName] = buffer.ToArray();
        return true;
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        Blobs.Remove(blobName);
        return Task.CompletedTask;
    }

    public Task<Stream> CreateFileForWritingAsync(string blobName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Uri GetBlobUri(string blobName) => new($"https://blobs.test/{container}/{blobName}");

    public Uri GenerateSasUri(string blobName, TimeSpan expiry) => GetBlobUri(blobName);

    public Uri GenerateSasUri(string blobName, TimeSpan expiry, ServedContentType servedAs) => GetBlobUri(blobName);

    public Task CopyAsync(Uri sourceUri, string targetBlob, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task CopyAsync(string sourceBlob, string targetBlob, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
