namespace Cleansia.Core.Blobs.Abstractions.Extensions;

public sealed class MetadataBuilder
{
    private readonly Dictionary<string, string> _metadata = new();

    public MetadataBuilder WithMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata key cannot be empty", nameof(key));
        }

        _metadata[key] = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

    public Metadata Build()
    {
        return new Metadata(_metadata.AsReadOnly());
    }
}