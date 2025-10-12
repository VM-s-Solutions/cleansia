namespace Cleansia.Core.Blobs.Abstractions;

public class BlobContainerConfiguration
{
    public string ConnectionStringName { get; set; }
    public List<ContainerMapping> Containers { get; set; }

    public string GetContainerNameByPrefix(string prefix)
    {
        var containerName = Containers.SingleOrDefault(x => x.VirtualBlobDirectories.Any(key => key.Prefix == prefix))?.ContainerName;

        if (string.IsNullOrEmpty(containerName))
        {
            throw new UnsupportedBlobPrefixException(prefix);
        }

        return containerName;
    }

    public (string Prefix, string? TaskName) GetPrefixAndTaskName(string container, string virtualPath)
    {
        var mapping = GetContainerMapping(container);

        var virtualDirectory = mapping.VirtualBlobDirectories.SingleOrDefault(x => virtualPath.StartsWith(x.VirtualPath));

        if (virtualDirectory is null)
        {
            throw new UnsupportedBlobContainerException(container, virtualPath);
        }

        return (virtualDirectory.Prefix, virtualDirectory.TaskName);
    }

    public string GetBlobName(string prefix, string fileName)
    {
        var virtualDirectory = GetVirtualDirectoryByPrefix(prefix);

        return $"{virtualDirectory.VirtualPath}/{fileName}";
    }

    private ContainerMapping GetContainerMapping(string container)
    {
        var mappings = Containers.SingleOrDefault(x => x.ContainerName == container);

        if(mappings is null)
        {
            throw new UnsupportedBlobContainerException(container);
        }

        return mappings;
    }

    private VirtualBlobDirectory GetVirtualDirectoryByPrefix(string prefix)
    {
        var virtualDirectory = Containers
            .SelectMany(x => x.VirtualBlobDirectories)
            .SingleOrDefault(x => x.Prefix == prefix);

        if (virtualDirectory == null)
        {
            throw new UnsupportedBlobPrefixException(prefix);
        }

        return virtualDirectory;
    }

    public bool IsValid(string prefix)
    {
        try
        {
            GetVirtualDirectoryByPrefix(prefix);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}