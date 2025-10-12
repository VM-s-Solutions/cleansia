using Cleansia.Core.Blobs.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Azure.Storage.Blobs;

public class BlobContainerClientFactory : IBlobContainerClientFactory
{
    private readonly string _azureStorageConnectionName;
    private readonly bool _useDefaultAzureCredential;
    private readonly IConfiguration _configuration;

    public BlobContainerClientFactory(IConfiguration configuration, string azureStorageConnectionName, bool useDefaultAzureCredential = false)
    {
        if (string.IsNullOrWhiteSpace(azureStorageConnectionName))
        {
            throw new ArgumentNullException(nameof(azureStorageConnectionName));
        }

        _azureStorageConnectionName = azureStorageConnectionName;
        _useDefaultAzureCredential = useDefaultAzureCredential;
        _configuration = configuration;
    }

    public IBlobContainerClient GetBlobContainerClient(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentNullException(nameof(containerName));
        }

        var connectionString = _configuration.GetConnectionString(_azureStorageConnectionName);
        var blobContainerName = new BlobContainerClient(connectionString, containerName, _useDefaultAzureCredential);

        return blobContainerName;
    }
}