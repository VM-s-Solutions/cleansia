namespace Cleansia.Core.Blobs.Abstractions.Extensions;

public sealed class Metadata(IReadOnlyDictionary<string, string> metadata)
{
    public static Metadata Empty => new Metadata(new Dictionary<string, string>());

    public static Metadata CacheMetadata =>
        Metadata.CreateBuilder()
            .WithMetadata(MetadataName.CacheControl, "public, max-age=31536000") // 1 year cache
            .Build();

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(metadata);
    }

    public static MetadataBuilder CreateBuilder() => new MetadataBuilder();
}

public static class MetadataName
{
    public const string ContentType = nameof(ContentType);
    public const string ContentDisposition = nameof(ContentDisposition);
    public const string ContentEncoding = nameof(ContentEncoding);
    public const string ContentLanguage = nameof(ContentLanguage);
    public const string CacheControl = nameof(CacheControl);
}