using Azure.Identity;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;

namespace Cleansia.Infra.Azure.Storage.Blobs;

public class BlobContainerClient : IBlobContainerClient
{
    private readonly bool _createContainerIfNotExists;
    private readonly global::Azure.Storage.Blobs.BlobContainerClient _container;

    public BlobContainerClient(string connectionString, string container, bool useDefaultAzureCredential = false, bool createContainerIfNotExists = false)
    {
        _createContainerIfNotExists = createContainerIfNotExists;
        _container = useDefaultAzureCredential
            ? new global::Azure.Storage.Blobs.BlobContainerClient(new Uri(connectionString + container), new DefaultAzureCredential())
            : new global::Azure.Storage.Blobs.BlobContainerClient(connectionString, container);
    }

    public BlobContainerClient(global::Azure.Storage.Blobs.BlobContainerClient container, bool createContainerIfNotExists = false)
    {
        _container = container;
        _createContainerIfNotExists = createContainerIfNotExists;
    }

    public async Task<IEnumerable<string>> GetFilesAsync(string path, CancellationToken cancellationToken)
    {
        var blobs = _container.GetBlobsAsync(prefix: path, cancellationToken: cancellationToken);
        var fileNames = new List<string>();

        await foreach (var blob in blobs)
        {
            fileNames.Add(blob.Name);
        }

        return fileNames;
    }

    public async Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        var client = _container.GetBlobClient(blobName);

        return await client.ExistsAsync(cancellationToken);
    }

    public async Task<BlobFile> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        var client = _container.GetBlobClient(blobName);

        var streamingResult = await client.DownloadStreamingAsync(new BlobDownloadOptions(), cancellationToken);
        return new BlobFile(streamingResult.Value.Content, streamingResult.Value.Details.ContentType);
    }

    public async Task UploadAsync(string blobName, Stream content, Metadata? metadata = null, CancellationToken cancellationToken = new CancellationToken())
    {
        await CreateContainerAsync(cancellationToken);

        var client = _container.GetBlobClient(blobName);
        await client.UploadAsync(content, cancellationToken);

        if (metadata is not null)
        {
            await client.SetMetadataAsync(metadata.ToDictionary(), cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        var client = _container.GetBlobClient(blobName);

        await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> CreateFileForWritingAsync(string blobName, CancellationToken cancellationToken)
    {
        await CreateContainerAsync(cancellationToken);
        var client = _container.GetBlobClient(blobName);
        return await client.OpenWriteAsync(true, cancellationToken: cancellationToken);
    }

    public Uri GetBlobUri(string blobName) => _container.GetBlobClient(blobName).Uri;

    public async Task CopyAsync(Uri sourceUri, string targetBlob, CancellationToken cancellationToken)
    {
        await CreateContainerAsync(cancellationToken);

        var client = _container.GetBlobClient(targetBlob);
        if (!sourceUri.ToString().StartsWith(_container.GetParentBlobServiceClient().Uri.ToString()))
        {
            throw new ArgumentException("Copy can only be used when source and target are on the storage account", nameof(sourceUri));
        }

        var operation = await client.StartCopyFromUriAsync(sourceUri, cancellationToken: cancellationToken);
        await operation.WaitForCompletionAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
    }

    public Task CopyAsync(string sourceBlob, string targetBlob, CancellationToken cancellationToken) =>
        CopyAsync(GetBlobUri(sourceBlob), targetBlob, cancellationToken);

    private async Task CreateContainerAsync(CancellationToken cancellationToken)
    {
        if (_createContainerIfNotExists)
        {
            await _container.CreateIfNotExistsAsync(PublicAccessType.BlobContainer, cancellationToken: cancellationToken);
        }
    }
}