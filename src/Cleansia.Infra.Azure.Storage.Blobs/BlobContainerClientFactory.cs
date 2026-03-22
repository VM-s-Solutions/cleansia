using Azure.Identity;
using Cleansia.Core.Blobs.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Azure.Storage.Blobs;

public class BlobContainerClientFactory : IBlobContainerClientFactory
{
    private readonly BlobContainerConfiguration _config;
    private readonly IConfiguration _configuration;

    public BlobContainerClientFactory(IConfiguration configuration, BlobContainerConfiguration config)
    {
        _configuration = configuration;
        _config = config;
    }

    public IBlobContainerClient GetBlobContainerClient(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentNullException(nameof(containerName));
        }

        if (_config.UseManagedIdentity)
        {
            var accountUrl = _config.AccountUrl!.TrimEnd('/');
            var containerUri = new Uri($"{accountUrl}/{containerName}");
            var azureClient = new global::Azure.Storage.Blobs.BlobContainerClient(containerUri, new DefaultAzureCredential());
            return new BlobContainerClient(azureClient);
        }

        var connectionString = _configuration.GetConnectionString(_config.ConnectionStringName);
        return new BlobContainerClient(connectionString, containerName);
    }
}
