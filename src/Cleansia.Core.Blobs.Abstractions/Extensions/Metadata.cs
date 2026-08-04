namespace Cleansia.Core.Blobs.Abstractions.Extensions;

/// <summary>
/// Azure <b>custom</b> blob metadata — the <c>x-ms-meta-*</c> key/value pairs stored beside a blob.
///
/// <para><b>This is descriptive data only. It has no effect on how the blob is served.</b> The type
/// previously exposed <c>MetadataName.ContentType</c>/<c>.ContentDisposition</c>/<c>.ContentEncoding</c>/
/// <c>.ContentLanguage</c>/<c>.CacheControl</c>, named exactly like <c>BlobHttpHeaders</c> and routed by
/// <see cref="IBlobContainerClient.UploadAsync"/> into <c>SetMetadataAsync</c>, so three upload pipelines
/// computed a correct content type and handed it to a sink that discards it. Those constants are deleted
/// rather than commented: the next developer reads the constant name, not the comment beside it.</para>
///
/// <para>To control what a reader receives, pin it on the read token —
/// <see cref="IBlobContainerClient.GenerateSasUri(string, TimeSpan, ServedContentType)"/>.</para>
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