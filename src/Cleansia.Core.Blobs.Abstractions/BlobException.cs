namespace Cleansia.Core.Blobs.Abstractions;

public class UnsupportedBlobPrefixException(string prefix) : Exception($"Blob prefix '{prefix}' is not supported");

public class UnsupportedBlobContainerException : Exception
{
    public UnsupportedBlobContainerException(string name) : base($"Blob container '{name}' is not supported") { }

    public UnsupportedBlobContainerException(string name, string virtualPath) : base($"Blob container '{name}' with virtual path '{virtualPath}' is not supported") { }
}