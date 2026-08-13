namespace Cleansia.Core.Blobs.Abstractions.Extensions;

/// <summary>
/// Azure custom blob metadata — the <c>x-ms-meta-*</c> pairs stored beside a blob.
///
/// <para><b>Descriptive only. It has NO effect on how the blob is served.</b> Constants named like
/// <c>BlobHttpHeaders</c> used to live here and were routed into metadata, so three upload pipelines
/// computed a correct content type and handed it to a sink that discards it. They were deleted rather
/// than commented — the next developer reads the constant name, not the comment beside it. To control
/// what a reader receives, pin it on the read token. → /architecture/backend#content-sniffing</para>
/// </summary>
public sealed class Metadata(IReadOnlyDictionary<string, string> metadata)
{
    public static Metadata Empty => new Metadata(new Dictionary<string, string>());

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(metadata);
    }

    public static MetadataBuilder CreateBuilder() => new MetadataBuilder();
}