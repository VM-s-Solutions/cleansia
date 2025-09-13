namespace Cleansia.Core.Blobs.Abstractions;

public class VirtualBlobDirectory
{
    /// <summary>
    /// Virtual path to the directory
    /// </summary>
    /// <remarks>
    /// The BlobName consists out of:
    /// - container
    /// - virtual path
    /// - filename
    /// </remarks>>
    public string VirtualPath { get; set; }
    /// <summary>
    /// Prefix is used to determine the virtual directory
    /// </summary>
    public string Prefix { get; set; }
    /// <summary>
    /// File transfer task Id, internally used by OnPrem File Transfer
    /// </summary>
    public string? TaskName { get; set; }
}