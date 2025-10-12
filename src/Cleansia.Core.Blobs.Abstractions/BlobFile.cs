namespace Cleansia.Core.Blobs.Abstractions;

public class BlobFile(Stream content, string contentType)
{
    public Stream Content { get; } = content;

    public string ContentType { get; } = contentType;
}