namespace Cleansia.Core.Blobs.Abstractions;

public interface IBlobContainerClientFactory
{
    IBlobContainerClient GetBlobContainerClient(string containerName);
}