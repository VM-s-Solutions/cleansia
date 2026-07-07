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

        // Write paths must survive a storage account/emulator that starts empty — create the
        // container on first write, same contract the queue client honors on every send. Both
        // auth modes may create: the shared-key connection string has full rights and the
        // managed identities carry Storage Blob Data Contributor.
        if (_config.UseManagedIdentity)
        {
            var accountUrl = _config.AccountUrl!.TrimEnd('/');
            var containerUri = new Uri($"{accountUrl}/{containerName}");
            var azureClient = new global::Azure.Storage.Blobs.BlobContainerClient(containerUri, new DefaultAzureCredential());
            return new BlobContainerClient(azureClient, createContainerIfNotExists: true);
        }

        var connectionString = _configuration.GetConnectionString(_config.ConnectionStringName);
        return new BlobContainerClient(connectionString, containerName, createContainerIfNotExists: true);
    }
}
