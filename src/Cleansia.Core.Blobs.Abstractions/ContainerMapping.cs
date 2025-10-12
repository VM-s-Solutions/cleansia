namespace Cleansia.Core.Blobs.Abstractions;

public class ContainerMapping
{
    public string ContainerName { get; set; }

    public List<VirtualBlobDirectory> VirtualBlobDirectories { get; set; }
}